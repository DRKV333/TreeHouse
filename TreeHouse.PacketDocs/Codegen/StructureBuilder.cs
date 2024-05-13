using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

    private readonly StructureVisitor visitor;

    public StructureBuilder()
    {
        visitor = new StructureVisitor()
        {
            Builder = this
        };
    }

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

    [return: NotNullIfNotNull(nameof(type))]
    public static string? ConvertTypeName(string? type) => type.Capitalize();

    [return: NotNullIfNotNull(nameof(field))]
    public static string? ConvertFieldName(string? field) => field.Capitalize();

    public void AppendFieldsList(FieldsList fields) => visitor.VisitFieldsList(fields, null);

    public void AppendField(Field field) => visitor.VisitField(field);

    private class StructureVisitor : FieldsListVisitor<object?>
    {
        public required StructureBuilder Builder { get; init; }

        protected override void VisitBranch(BranchDetails branch, int index, object? param)
        {
            string fieldName = ConvertFieldName(branch.Field);

            string compareFrom;
            if (Builder.enumMemberBaseTypes.TryGetValue(fieldName, out string? enumTypeName))
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

            Builder.AppendLineReadWrite($"if ({condition})");
            Builder.AppendLineReadWrite("{");

            if (branch.IsTrue != null)
                VisitFieldsList(branch.IsTrue, param);

            Builder.AppendLineReadWrite("}");
            Builder.AppendLineReadWrite("else");
            Builder.AppendLineReadWrite("{");

            if (branch.IsFalse != null)
                VisitFieldsList(branch.IsFalse, param);

            Builder.AppendLineReadWrite("}");
        }

        public void VisitField(Field field) => VisitField(field, 0, null);

        protected override void VisitField(Field field, int index, object? param)
        {
            if (!string.IsNullOrEmpty(field.Name))
                Builder.FinishSkip();

            base.VisitField(field, index, param);
        }

        protected override void VisitPrimitive(Field field, int index, PrimitiveFieldType type, object? param)
        {
            string? fieldName = ConvertFieldName(field.Name);

            IntrinsicSpec? spec;
            if (type.Value.StartsWith(':'))
            {
                string structTypeName = ConvertTypeName(type.Value[1..]);
                spec = IntrinsicSpecs.GetIntrinsicFromStructure(structTypeName);
            }
            else if (!IntrinsicSpecs.TryGetInrinsic(type.Value, out spec))
            {
                return;
            }

            if (fieldName == null)
            {
                if (spec.Size == -1)
                {
                    Builder.FinishSkip();
                    if (spec.SkipWriteSizeEstimate == -1)
                    {
                        throw new InvalidOperationException($"Cant skip type {spec.CsType}");
                    }
                    else
                    {
                        Builder.readBuilder.AppendLine(spec.SkipRead);
                        Builder.writeBuilder.AppendLine(spec.SkipWrite);
                    }
                }
                else
                {
                    if (Builder.currentSkip == null)
                        Builder.currentSkip = new SizeBuilder();
                    Builder.currentSkip.AddConstant(spec.Size);
                }
            }
            else
            {
                Builder.AppendMember(fieldName, spec.CsType);
                Builder.readBuilder.AppendLine(spec.Read(fieldName));
                Builder.writeBuilder.AppendLine(spec.Write(fieldName));
            }

            base.VisitPrimitive(field, index, type, param);
        }

        protected override void VisitEnum(Field field, int index, EnumFieldType type, object? param)
        {
            string? fieldName = ConvertFieldName(field.Name);

            if (fieldName == null)
            {
                VisitPrimitive(field, index, new PrimitiveFieldType() { Value = type.Name }, param);
                return;
            }

            if (!IntrinsicSpecs.TryGetInrinsic(type.Name, out IntrinsicSpec? spec))
                return;

            string typeName = ConvertTypeName($"{fieldName}Type");

            Builder.AppendMember(fieldName, typeName);
            Builder.enumMemberBaseTypes.Add(fieldName, spec.CsType);

            Builder.membersBuilder.AppendLine($"public enum {typeName} : {spec.CsType}");
            Builder.membersBuilder.AppendLine("{");

            foreach (var item in type.Enum)
            {
                Builder.membersBuilder.AppendLine($"    {ConvertFieldName(item.Value)} = {item.Key},");
            }

            Builder.membersBuilder.AppendLine("}");

            string fieldNameCast = $"(({spec.CsType}){fieldName})";

            Builder.readBuilder.AppendLine(IntrinsicSpecs.ReadAndCast(spec, fieldName, typeName));
            Builder.writeBuilder.AppendLine(spec.Write(fieldNameCast));

            base.VisitEnum(field, index, type, param);
        }

        protected override void VisitArray(Field field, int index, ArrayFieldType type, object? param)
        {
            string? fieldName = ConvertFieldName(field.Name);

            string len;
            if (int.TryParse(type.Len, out int intLen))
                len = intLen.ToString(CultureInfo.InvariantCulture);
            else
                len = ConvertFieldName(type.Len);

            IntrinsicArraySpec? arraySpec;
            if (type.Type.StartsWith(':'))
            {
                string structTypeName = ConvertTypeName(type.Type[1..]);
                arraySpec = IntrinsicSpecs.GetArrayFromStructure(structTypeName);
            }
            else if (!IntrinsicSpecs.TryGetArrayInrinsic(type.Type, out arraySpec))
            {
                return;
            }

            if (fieldName == null)
            {
                if (arraySpec.ElementSize == -1)
                {
                    Builder.FinishSkip();
                    if (arraySpec.ElementSkipWriteSizeEstimate == -1)
                    {
                        throw new InvalidOperationException($"Cant skip type {arraySpec.CsType}");
                    }
                    else
                    {
                        Builder.readBuilder.AppendLine(arraySpec.SkipRead!(len));
                        Builder.writeBuilder.AppendLine(arraySpec.SkipWrite!(len));
                    }
                }
                else
                {
                    if (Builder.currentSkip == null)
                        Builder.currentSkip = new SizeBuilder();
                    Builder.currentSkip.AddExpression($"({len} * {arraySpec.ElementSize})");
                }
            }
            else
            {
                Builder.AppendMember(fieldName, arraySpec.CsType);
                Builder.readBuilder.AppendLine(arraySpec.Read(fieldName, len));
                Builder.writeBuilder.AppendLine(arraySpec.Write(fieldName));
            }

            base.VisitArray(field, index, type, param);
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
