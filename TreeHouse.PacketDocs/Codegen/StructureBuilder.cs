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
        IntrinsicSpec? spec;
        if (type.StartsWith(':'))
        {
            string structTypeName = ConvertTypeName(type[1..]);
            spec = IntrinsicSpecs.GetIntrinsicFromStructure(structTypeName);
            SizeEstimateBuilder.AddContainedType(structTypeName, fieldName);
        }
        else if (IntrinsicSpecs.TryGetInrinsic(type, out spec))
        {
            SizeEstimateBuilder.AddIntrinsic(spec, fieldName);
        }
        else
        {
            return;
        }

        AppendMember(fieldName, spec.CsType);
        readBuilder.AppendLine(spec.Read(fieldName));
        writeBuilder.AppendLine(spec.Write(fieldName));
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
            membersBuilder.AppendLine($"    {ConvertFieldName(item.Value)} = {item.Key},");
        }

        membersBuilder.AppendLine("}");

        string fieldNameCast = $"(({spec.CsType}){fieldName})";

        readBuilder.AppendLine(IntrinsicSpecs.ReadAndCast(spec, fieldName, typeName));
        writeBuilder.AppendLine(spec.Write(fieldNameCast));
        SizeEstimateBuilder.AddIntrinsic(spec, fieldNameCast);
    }

    private void AppendArray(string fieldName, ArrayFieldType arrayType)
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
            SizeEstimateBuilder.AddRefencedArrayType(structTypeName, fieldName);
        }
        else if (IntrinsicSpecs.TryGetArrayInrinsic(arrayType.Type, out arraySpec))
        {
            SizeEstimateBuilder.AddIntrinsicArray(arraySpec, fieldName);
        }
        else
        {
            return;
        }

        AppendMember(fieldName, arraySpec.CsType);
        readBuilder.AppendLine(arraySpec.Read(fieldName, len));
        writeBuilder.AppendLine(arraySpec.Write(fieldName));
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
