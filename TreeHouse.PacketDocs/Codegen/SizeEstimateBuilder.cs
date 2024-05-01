using System.Collections.Generic;
using System.Text;

namespace TreeHouse.PacketDocs.Codegen;

internal class SizeEstimateBuilder
{
    private record class FieldReferenceToken(
        SizeResolver.ReferenceToken Token,
        string FieldName
    );

    private int sizeConstant = 0;
    private readonly List<string> sizeExpressions = new();
    private readonly List<FieldReferenceToken> containedTypes = new();
    private readonly List<FieldReferenceToken> referencedArrayTypes = new();

    private readonly SizeResolver sizeResolver;

    public SizeEstimateBuilder(SizeResolver.SelfToken selfSize)
    {
        sizeResolver = selfSize.Resolver;

        selfSize.SetResolveDelegate(() => {
            if (sizeExpressions.Count > 0)
            {
                return null;
            }
            else
            {
                ResolveOtherTypes();

                if (sizeExpressions.Count > 0)
                    return null;
                else
                    return sizeConstant;
            }
        });
    }

    public void AddConstant(int c) => sizeConstant += c;

    public void AddExpression(string expression) => sizeExpressions.Add(expression);

    public void AddContainedType(string type, string fieldName) =>
        containedTypes.Add(new FieldReferenceToken(sizeResolver.CreateReferenceToken(type), fieldName));

    public void AddRefencedArrayType(string type, string fieldName) =>
        referencedArrayTypes.Add(new FieldReferenceToken(sizeResolver.CreateReferenceToken(type), fieldName));

    public string GetSizeEstimate()
    {
        ResolveOtherTypes();

        StringBuilder builder = new();

        if (sizeConstant > 0 || sizeExpressions.Count == 0)
        {
            builder.Append(sizeConstant);
            if (sizeExpressions.Count > 0)
                builder.Append(" + ");
        }

        builder.AppendJoin(" + ", sizeExpressions);

        return builder.ToString();
    }

    private void ResolveOtherTypes()
    {
        foreach (FieldReferenceToken item in containedTypes)
        {
            int? size = item.Token.GetSize();
            if (size == null)
                sizeExpressions.Add($"{item.FieldName}.EstimateSize()");
            else
                sizeConstant += size.Value;
        }
        containedTypes.Clear();

        foreach (FieldReferenceToken item in referencedArrayTypes)
        {
            int? size = item.Token.GetSize();
            if (size == null)
                sizeExpressions.Add($"ArrayIntrinsics.EstimateArrayStructure({item.FieldName})");
            else
                sizeExpressions.Add($"({item.FieldName}.Length * {size})");
        }
        referencedArrayTypes.Clear();
    }
}
