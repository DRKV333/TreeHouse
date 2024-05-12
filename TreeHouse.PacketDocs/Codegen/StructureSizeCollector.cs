using System;
using System.Collections.Generic;
using TreeHouse.PacketFormat;

namespace TreeHouse.PacketDocs.Codegen;

internal class StructureSizeCollector
{
    private class SizeVisitor : FieldsListVisitor<SizeBuilder>
    {
        public required StructureSizeCollector Collector { get; init; }

        protected override void VisitPrimitive(Field field, int index, PrimitiveFieldType type, SizeBuilder param)
        {
            IntrinsicSpec? spec;
            if (type.Value.StartsWith(':'))
            {
                string structName = type.Value[1..];

                Size structSize = Collector.GetSizeEstimate(structName);
                if (structSize.IsConstant)
                {
                    param.AddConstant(structSize.Constant);
                    return;
                }

                spec = IntrinsicSpecs.GetIntrinsicFromStructure(StructureBuilder.ConvertTypeName(structName));
            }
            else if (!IntrinsicSpecs.TryGetInrinsic(type.Value, out spec))
            {
                return;
            }

            AddPrimitive(spec, field.Name, param);

            base.VisitPrimitive(field, index, type, param);
        }

        protected override void VisitArray(Field field, int index, ArrayFieldType type, SizeBuilder param)
        {
            string? len;
            if (int.TryParse(type.Len, out int constantLen))
                len = null;
            else
                len = StructureBuilder.ConvertFieldName(type.Len);

            IntrinsicArraySpec? arraySpec;
            if (type.Type.StartsWith(':'))
            {
                string structName = type.Type[1..];

                Size structSize = Collector.GetSizeEstimate(structName);
                if (structSize.IsConstant)
                {
                    if (len == null)
                        param.AddConstant(constantLen * structSize.Constant);
                    else
                        param.AddExpression($"({len} * {structSize.Constant})");

                    return;
                }

                arraySpec = IntrinsicSpecs.GetArrayFromStructure(StructureBuilder.ConvertTypeName(structName));
            }
            else if (!IntrinsicSpecs.TryGetArrayInrinsic(type.Type, out arraySpec))
            {
                return;
            }

            if (arraySpec.ElementSize == -1)
            {
                if (field.Name == null)
                {
                    if (arraySpec.ElementSkipWriteSizeEstimate == -1)
                    {
                        throw new InvalidOperationException($"Can't skip array type {arraySpec.CsType}");
                    }
                    else
                    {
                        if (len == null)
                            param.AddConstant(constantLen * arraySpec.ElementSkipWriteSizeEstimate);
                        else
                            param.AddExpression($"({len} * {arraySpec.ElementSkipWriteSizeEstimate})");
                    }
                }
                else
                {
                    param.AddExpression(arraySpec.EstimateSize!(StructureBuilder.ConvertFieldName(field.Name)));
                }
            }
            else
            {
                if (len == null)
                {
                    param.AddConstant(constantLen * arraySpec.ElementSize);
                }
                else
                {
                    if (field.Name == null)
                        param.AddExpression($"({len} * {arraySpec.ElementSize})");
                    else
                        param.AddExpression(IntrinsicSpecs.ArraySizeWithContantElementSize(StructureBuilder.ConvertFieldName(field.Name), arraySpec.ElementSize));
                }
            }

            base.VisitArray(field, index, type, param);
        }

        protected override void VisitEnum(Field field, int index, EnumFieldType type, SizeBuilder param)
        {
            if (IntrinsicSpecs.TryGetInrinsic(type.Name, out IntrinsicSpec? spec))
                AddPrimitive(spec, null, param);

            base.VisitEnum(field, index, type, param);
        }

        private static void AddPrimitive(IntrinsicSpec spec, string? fieldName, SizeBuilder param)
        {
            if (spec.Size == -1)
            {
                if (fieldName == null)
                {
                    if (spec.SkipWriteSizeEstimate == -1)
                        throw new InvalidOperationException($"Can't skip type {spec.CsType}");
                    else
                        param.AddConstant(spec.SkipWriteSizeEstimate);
                }
                else
                {
                    param.AddExpression(spec.EstimateSize!(StructureBuilder.ConvertFieldName(fieldName)));
                }
            }
            else
            {
                param.AddConstant(spec.Size);
            }
        }
    }

    private readonly SizeVisitor visitor;

    private readonly Dictionary<string, Size> cache = new();
    private readonly HashSet<string> inProgress = new();

    private readonly Func<string, FieldsList> getStructure;

    public StructureSizeCollector(Func<string, FieldsList> getStructure)
    {
        visitor = new SizeVisitor() { Collector = this };
        this.getStructure = getStructure;
    }

    public Size GetSizeEstimate(string structName)
    {
        if (cache.TryGetValue(structName, out Size cachedSize))
            return cachedSize;

        if (!inProgress.Add(structName))
            throw new InvalidReferenceChainException("Circular structure reference detected", structName);

        try
        {
            Size size = DoEstimateSize(structName);
            cache.Add(structName, size);
            return size;
        }
        catch (InvalidReferenceChainException e)
        {
            e.AddReference(structName);
            throw;
        }
        finally
        {
            inProgress.Remove(structName);
        }
    }

    private Size DoEstimateSize(string structName)
    {
        FieldsList fieldsList = getStructure(structName);
        SizeBuilder builder = new();

        visitor.VisitFieldsList(fieldsList, builder);

        return builder.GetSize();
    }
}
