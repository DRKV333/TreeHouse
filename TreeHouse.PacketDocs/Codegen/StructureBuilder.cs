using System;
using System.Collections.Generic;
using System.Text;
using TreeHouse.Common;
using TreeHouse.PacketFormat;

namespace TreeHouse.PacketDocs.Codegen;

public class StructureBuilder
{
    public record class Enum(
        string FieldName,
        string BaseCsType,
        IEnumerable<(string name, int value)> Values
    );

    private record class IntrinsicSpec(
        string CsType,
        string Read,
        Func<string, string> Write,
        int Size,
        Func<string, string>? EstimateSize = null
    );

    private static readonly Dictionary<string, IntrinsicSpec> IntrinsicSpecs = new()
    {
        { "bool", new IntrinsicSpec(
            "bool",
            "(reader.ReadByte() != 0)",
            f => $"writer.WriteByte({f} ? (byte)1 : (byte)0);",
            1
        )},
        { "u8", new IntrinsicSpec(
            "byte",
            "reader.ReadByte()",
            f => $"writer.WriteByte({f});",
            1
        )},
        { "u16", new IntrinsicSpec(
            "ushort",
            "reader.ReadUInt16LE()",
            f => $"writer.WriteUInt16LE({f});",
            2
        )},
        { "u32", new IntrinsicSpec(
            "uint",
            "reader.ReadUInt32LE()",
            f => $"writer.WriteUInt32LE({f});",
            2
        )},
        { "u64", new IntrinsicSpec(
            "ulong",
            "reader.ReadUInt64LE()",
            f => $"writer.WriteUInt64LE({f});",
            4
        )},
        { "i8", new IntrinsicSpec(
            "sbyte",
            "reader.ReadSByte()",
            f => $"writer.WriteSByte({f});",
            1
        )},
        { "i16", new IntrinsicSpec(
            "short",
            "reader.ReadInt16LE()",
            f => $"writer.WriteInt16LE({f});",
            2
        )},
        { "i32", new IntrinsicSpec(
            "int",
            "reader.ReadInt32LE()",
            f => $"writer.WriteInt32LE({f});",
            2
        )},
        { "i64", new IntrinsicSpec(
            "long",
            "reader.ReadInt64LE()",
            f => $"writer.WriteInt64LE({f});",
            4
        )},
        { "f32", new IntrinsicSpec(
            "float",
            "reader.ReadSingleLE()",
            f => $"writer.WriteSingleLE({f});",
            4
        )},
        { "f64", new IntrinsicSpec(
            "double",
            "reader.ReadDoubleLE()",
            f => $"writer.WriteDoubleLE({f});",
            4
        )},
        { "cstring", new IntrinsicSpec(
            "string",
            "reader.ReadCString()",
            f => $"writer.WriteCString({f});",
            -1,
            f => $"StringIntrinsics.EstimateCString({f})"
        )},
        { "wstring", new IntrinsicSpec(
            "string",
            "reader.ReadCString()",
            f => $"writer.WriteCString({f});",
            -1,
            f => $"StringIntrinsics.EstimateWString({f})"
        )},
        { "uuid", new IntrinsicSpec(
            "global::System.Guid",
            "reader.ReadGuidLE()",
            f => $"writer.WriteGuidLE({f});",
            16
        )}
    };

    private readonly StringBuilder membersBuilder = new();
    private readonly StringBuilder readBuilder = new();
    private readonly StringBuilder writeBuilder = new();

    private int sizeConstant = 0;
    private List<string> sizeExpression = new();

    private readonly HashSet<string> members = new();

    private readonly Dictionary<string, string> enumMemberBaseTypes = new();

    public string GetMembers() => membersBuilder.ToString();
    public string GetRead() => readBuilder.ToString();
    public string GetWrite() => writeBuilder.ToString();
    
    public string GetSizeEstimate()
    {
        StringBuilder builder = new();

        if (sizeConstant > 0 || sizeExpression.Count == 0)
        {
            builder.Append(sizeConstant);
            if (sizeExpression.Count > 0)
                builder.Append(" + ");
        }

        builder.AppendJoin(" + ", sizeExpression);

        return builder.ToString();
    }

    public static string ConvertTypeName(string type) => type.Capitalize();

    public static string ConvertFieldName(string field) => field.Capitalize();

    public void AppendFieldsList(FieldsList fields)
    {
        foreach (var item in fields.Fields)
        {
            if (item is Branch branch)
            {
                string fieldName = ConvertFieldName(branch.Details.Field);

                string compareFrom;
                if (enumMemberBaseTypes.TryGetValue(fieldName, out string? enumTypeName))
                    compareFrom = $"(({enumTypeName}){fieldName})";
                else
                    compareFrom = fieldName;

                string condition;
                if (branch.Details.TestEqual != null)
                    condition = $"{compareFrom} == {branch.Details.TestEqual}";
                else if (branch.Details.TestFlag != null)
                    condition = $"({compareFrom} & {branch.Details.TestFlag}) == {branch.Details.TestFlag}";
                else
                    condition = compareFrom;

                AppendLineReadWrite($"if ({condition})");
                AppendLineReadWrite("{");

                if (branch.Details.IsTrue != null)
                    AppendFieldsList(branch.Details.IsTrue);

                AppendLineReadWrite("}");
                AppendLineReadWrite("else");
                AppendLineReadWrite("{");

                if (branch.Details.IsFalse != null)
                    AppendFieldsList(branch.Details.IsFalse);

                AppendLineReadWrite("}");
            }
            else if (item is Field field)
            {
                AppendField(field);
            }
        }
    }

    public void AppendField(Field field)
    {
        if (string.IsNullOrEmpty(field.Name))
            return;

        string fieldName = ConvertFieldName(field.Name);
        
        if (field.Type is PrimitiveFieldType primitive)
            AppendPrimitive(fieldName, primitive.Value);
        else if (field.Type is EnumFieldType enumType)
            AppendEnum(fieldName, enumType);
    }

    private void AppendPrimitive(string fieldName, string type)
    {
        if (type.StartsWith(':'))
        {
            AppendMember(fieldName, ConvertTypeName(type[1..]));
            readBuilder.AppendLine($"{fieldName}.Read(reader);");
            writeBuilder.AppendLine($"{fieldName}.Write(writer);");
            sizeExpression.Add($"{fieldName}.EstimateSize()");
        }
        else if (IntrinsicSpecs.TryGetValue(type, out IntrinsicSpec? spec))
        {
            AppendMember(fieldName, spec.CsType);
            readBuilder.AppendLine($"{fieldName} = {spec.Read};");
            writeBuilder.AppendLine(spec.Write(fieldName));

            if (spec.Size == -1)
                sizeExpression.Add(spec.EstimateSize!(fieldName));
            else
                sizeConstant += spec.Size;
        }
    }

    private void AppendEnum(string fieldName, EnumFieldType enumType)
    {
        if (!IntrinsicSpecs.TryGetValue(enumType.Name, out IntrinsicSpec? spec))
            return;

        string typeName = ConvertTypeName($"{fieldName}Type");

        AppendMember(fieldName, typeName);
        enumMemberBaseTypes.Add(fieldName, spec.CsType);

        membersBuilder.AppendLine($"public enum {typeName} : {spec.CsType}");
        membersBuilder.AppendLine("{");

        foreach (var item in enumType.Enum)
        {
            membersBuilder.Append($"    {ConvertFieldName(item.Value)} = {item.Key},");
        }

        membersBuilder.AppendLine("}");

        readBuilder.AppendLine($"{fieldName} = ({typeName}){spec.Read};");
        writeBuilder.AppendLine(spec.Write($"(({spec.CsType}){fieldName})"));
        sizeConstant += spec.Size;
    }

    private void AppendMember(string fieldName, string fieldType)
    {
        if (members.Add(fieldName))
            membersBuilder.AppendLine($"public {fieldType} {fieldName};");
    }

    private void AppendLineReadWrite(string str)
    {
        readBuilder.AppendLine(str);
        writeBuilder.AppendLine(str);
    }
}
