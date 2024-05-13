using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TreeHouse.Common;
using TreeHouse.PacketFormat;

namespace TreeHouse.PacketDocs.Codegen;

internal class StructureBuilder
{
    private readonly StringBuilder membersBuilder = new();
    private readonly StringBuilder readBuilder = new();
    private readonly StringBuilder writeBuilder = new();

    private readonly HashSet<string> members = new();

    private readonly Dictionary<string, string> enumMemberBaseTypes = new();

    private SizeBuilder? currentSkip = null;

    public string GetMembers() => membersBuilder.ToString();
    
    public string GetRead()
    {
        FinishSkip();
        return readBuilder.ToString();
    }

    public string GetWrite()
    {
        FinishSkip();
        return writeBuilder.ToString();
    }

    public static string ConvertTypeName(string type) => type.Capitalize();

    public static string ConvertFieldName(string field) => field.Capitalize();

    public void AppendFieldsList(FieldsList fields)
    {
        foreach (var item in fields.Fields)
        {
            if (item is Branch branch)
                AppendBranch(branch.Details);
            else if (item is Field field)
                AppendField(field);
        }
    }

    private void AppendBranch(BranchDetails branch)
    {
        string fieldName = ConvertFieldName(branch.Field);

        string compareFrom;
        if (enumMemberBaseTypes.TryGetValue(fieldName, out string? enumTypeName))
            compareFrom = $"(({enumTypeName}){fieldName})";
        else
            compareFrom = fieldName;

        string condition;
        if (branch.TestEqual != null)
            condition = $"{compareFrom} == {branch.TestEqual}";
        else if (branch.TestFlag != null)
            condition = $"({compareFrom} & {branch.TestFlag}) == {branch.TestFlag}";
        else
            condition = compareFrom;

        AppendLineReadWrite($"if ({condition})");
        AppendLineReadWrite("{");

        if (branch.IsTrue != null)
            AppendFieldsList(branch.IsTrue);

        AppendLineReadWrite("}");
        AppendLineReadWrite("else");
        AppendLineReadWrite("{");

        if (branch.IsFalse != null)
            AppendFieldsList(branch.IsFalse);

        AppendLineReadWrite("}");
    }

    public void AppendField(Field field)
    {
        string? fieldName = null;
        if (!string.IsNullOrEmpty(field.Name))
        {
            fieldName = ConvertFieldName(field.Name);
            FinishSkip();
        }

        if (field.Type is PrimitiveFieldType primitive)
            AppendPrimitive(fieldName, primitive.Value);
        else if (field.Type is EnumFieldType enumType)
            AppendEnum(fieldName, enumType);
        else if (field.Type is ArrayFieldType arrayType)
            AppendArray(fieldName, arrayType);
    }

    private void AppendPrimitive(string? fieldName, string type)
    {
        IntrinsicSpec? spec;
        if (type.StartsWith(':'))
        {
            string structTypeName = ConvertTypeName(type[1..]);
            spec = IntrinsicSpecs.GetIntrinsicFromStructure(structTypeName);
        }
        else if (!IntrinsicSpecs.TryGetInrinsic(type, out spec))
        {
            return;
        }

        if (fieldName == null)
        {
            if (spec.Size == -1)
            {
                FinishSkip();
                if (spec.SkipWriteSizeEstimate == -1)
                {
                    throw new InvalidOperationException($"Cant skip type {spec.CsType}");
                }
                else
                {
                    readBuilder.AppendLine(spec.SkipRead);
                    writeBuilder.AppendLine(spec.SkipWrite);
                }
            }
            else
            {
                if (currentSkip == null)
                    currentSkip = new SizeBuilder();
                currentSkip.AddConstant(spec.Size);
            }
        }
        else
        {
            AppendMember(fieldName, spec.CsType);
            readBuilder.AppendLine(spec.Read(fieldName));
            writeBuilder.AppendLine(spec.Write(fieldName));
        }
    }

    private void AppendEnum(string? fieldName, EnumFieldType enumType)
    {
        if (fieldName == null)
        {
            AppendPrimitive(fieldName, enumType.Name);
            return;
        }

        if (!IntrinsicSpecs.TryGetInrinsic(enumType.Name, out IntrinsicSpec? spec))
            return;

        string typeName = ConvertTypeName($"{fieldName}Type");

        AppendMember(fieldName, typeName);
        enumMemberBaseTypes.Add(fieldName, spec.CsType);

        membersBuilder.AppendLine($"public enum {typeName} : {spec.CsType}");
        membersBuilder.AppendLine("{");

        foreach (var item in enumType.Enum)
        {
            membersBuilder.AppendLine($"    {ConvertFieldName(item.Value)} = {item.Key},");
        }

        membersBuilder.AppendLine("}");

        string fieldNameCast = $"(({spec.CsType}){fieldName})";

        readBuilder.AppendLine(IntrinsicSpecs.ReadAndCast(spec, fieldName, typeName));
        writeBuilder.AppendLine(spec.Write(fieldNameCast));
    }

    private void AppendArray(string? fieldName, ArrayFieldType arrayType)
    {
        string len;
        if (int.TryParse(arrayType.Len, out int intLen))
            len = intLen.ToString(CultureInfo.InvariantCulture);
        else
            len = ConvertFieldName(arrayType.Len);

        IntrinsicArraySpec? arraySpec;
        if (arrayType.Type.StartsWith(':'))
        {
            string structTypeName = ConvertTypeName(arrayType.Type[1..]);
            arraySpec = IntrinsicSpecs.GetArrayFromStructure(structTypeName);
        }
        else if (!IntrinsicSpecs.TryGetArrayInrinsic(arrayType.Type, out arraySpec))
        {
            return;
        }

        if (fieldName == null)
        {
            if (arraySpec.ElementSize == -1)
            {
                FinishSkip();
                if (arraySpec.ElementSkipWriteSizeEstimate == -1)
                {
                    throw new InvalidOperationException($"Cant skip type {arraySpec.CsType}");
                }
                else
                {
                    readBuilder.AppendLine(arraySpec.SkipRead!(len));
                    writeBuilder.AppendLine(arraySpec.SkipWrite!(len));
                }
            }
            else
            {
                if (currentSkip == null)
                    currentSkip = new SizeBuilder();
                currentSkip.AddExpression($"({len} * {arraySpec.ElementSize})");
            }
        }
        else
        {
            AppendMember(fieldName, arraySpec.CsType);
            readBuilder.AppendLine(arraySpec.Read(fieldName, len));
            writeBuilder.AppendLine(arraySpec.Write(fieldName));
        }
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

    private void FinishSkip()
    {
        if (currentSkip != null)
        {
            Size skipSize = currentSkip.GetSize();

            readBuilder.AppendLine($"reader.Skip((int)({skipSize.ToString()}));");
            writeBuilder.AppendLine($"writer.WriteZeroes((int)({skipSize.ToString()}));");

            currentSkip = null;
        }
    }
}
