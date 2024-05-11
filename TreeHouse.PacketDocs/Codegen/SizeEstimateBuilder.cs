using System.Collections.Generic;

namespace TreeHouse.PacketDocs.Codegen;

internal class SizeEstimateBuilder
{
    private record class FieldReferenceToken(
        SizeResolver.ReferenceToken Token,
        string FieldName
    );

    private readonly SizeBuilder builder = new();

    private readonly List<FieldReferenceToken> containedTypes = new();
    private readonly List<FieldReferenceToken> referencedArrayTypes = new();

    private readonly SizeResolver sizeResolver;

    public SizeEstimateBuilder(SizeResolver.SelfToken selfSize)
    {
        sizeResolver = selfSize.Resolver;

        selfSize.SetResolveDelegate(() => {
            if (!builder.IsConstant)
            {
                return null;
            }
            else
            {
                ResolveOtherTypes();

                if (!builder.IsConstant)
                    return null;
                else
                    return builder.SizeConstant;
            }
        });
    }

    public void AddContainedType(string type, string fieldName) =>
        containedTypes.Add(new FieldReferenceToken(sizeResolver.CreateReferenceToken(type), fieldName));

    public void AddRefencedArrayType(string type, string fieldName) =>
        referencedArrayTypes.Add(new FieldReferenceToken(sizeResolver.CreateReferenceToken(type), fieldName));

    public void AddIntrinsic(IntrinsicSpec spec, string fieldName)
    {
        if (spec.Size == -1)
            builder.AddExpression(spec.EstimateSize!(fieldName));
        else
            builder.AddConstant(spec.Size);
    }

    public void AddIntrinsicArray(IntrinsicArraySpec arraySpec, string fieldName)
    {
        if (arraySpec.ElementSize == -1)
            builder.AddExpression(arraySpec.EstimateSize!(fieldName));
        else
            builder.AddExpression(IntrinsicSpecs.ArraySizeWithContantElementSize(fieldName, arraySpec.ElementSize));
    }

    public string GetSizeEstimate()
    {
        ResolveOtherTypes();
        return builder.GetSize().ToString();
    }

    private void ResolveOtherTypes()
    {
        foreach (FieldReferenceToken item in containedTypes)
        {
            int? size = item.Token.GetSize();
            if (size == null)
                builder.AddExpression(IntrinsicSpecs.EstimateStructureSize(item.FieldName));
            else
                builder.AddConstant(size.Value);
        }
        containedTypes.Clear();

        foreach (FieldReferenceToken item in referencedArrayTypes)
        {
            int? size = item.Token.GetSize();
            if (size == null)
                builder.AddExpression(IntrinsicSpecs.EstimateArrayStructureSize(item.FieldName));
            else
                builder.AddExpression(IntrinsicSpecs.ArraySizeWithContantElementSize(item.FieldName, size.Value));
        }
        referencedArrayTypes.Clear();
    }
}
