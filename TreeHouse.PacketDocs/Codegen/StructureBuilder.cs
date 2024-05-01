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

    public SizeEstimateBuilder SizeEstimateBuilder { get; }

    public StructureBuilder(SizeResolver.SelfToken selfSize)
    {
        SizeEstimateBuilder = new SizeEstimateBuilder(selfSize);
    }

    public string GetMembers() => membersBuilder.ToString();
    public string GetRead() => readBuilder.ToString();
    public string GetWrite() => writeBuilder.ToString();

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
        else if (field.Type is ArrayFieldType arrayType)
            AppendArray(fieldName, arrayType);
    }

    private void AppendPrimitive(string fieldName, string type)
    {
        if (type.StartsWith(':'))
        {
            string structTypeName = ConvertTypeName(type[1..]);

            AppendMember(fieldName, structTypeName);
            readBuilder.AppendLine($"{fieldName}.Read(reader);");
            writeBuilder.AppendLine($"{fieldName}.Write(writer);");
            SizeEstimateBuilder.AddContainedType(structTypeName, fieldName);
        }
        else if (IntrinsicSpecs.TryGetInrinsic(type, out IntrinsicSpec? spec))
        {
            AppendMember(fieldName, spec.CsType);
            readBuilder.AppendLine($"{fieldName} = {spec.Read};");
            writeBuilder.AppendLine(spec.Write(fieldName));

            if (spec.Size == -1)
                SizeEstimateBuilder.AddExpression(spec.EstimateSize!(fieldName));
            else
                SizeEstimateBuilder.AddConstant(spec.Size);
        }
    }

    private void AppendEnum(string fieldName, EnumFieldType enumType)
    {
        if (!IntrinsicSpecs.TryGetInrinsic(enumType.Name, out IntrinsicSpec? spec))
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
        SizeEstimateBuilder.AddConstant(spec.Size);
    }

    private void AppendArray(string fieldName, ArrayFieldType arrayType)
    {
        string len;
        if (int.TryParse(arrayType.Len, out int intLen))
            len = intLen.ToString(CultureInfo.InvariantCulture);
        else
            len = ConvertFieldName(arrayType.Len);

        if (arrayType.Type.StartsWith(':'))
        {
            string structTypeName = ConvertTypeName(arrayType.Type[1..]);

            AppendMember(fieldName, $"{structTypeName}[]");
            readBuilder.AppendLine($"reader.ReadArrayStructure((int){len}, ref {fieldName});");
            writeBuilder.AppendLine($"writer.WriteArrayStructure({fieldName});");
            SizeEstimateBuilder.AddRefencedArrayType(structTypeName, fieldName);
        }
        else if (IntrinsicSpecs.TryGetArrayInrinsic(arrayType.Type, out IntrinsicArraySpec? arraySpec))
        {
            AppendMember(fieldName, arraySpec.CsType);
            readBuilder.AppendLine(arraySpec.Read(fieldName, len));
            writeBuilder.AppendLine(arraySpec.Write(fieldName));

            if (arraySpec.ElementSize == -1)
                SizeEstimateBuilder.AddExpression(arraySpec.EstimateSize!(fieldName));
            else
                SizeEstimateBuilder.AddExpression($"({fieldName}.Length * {arraySpec.ElementSize})");
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
}
