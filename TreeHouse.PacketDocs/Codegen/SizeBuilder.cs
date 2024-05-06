using System.Collections.Generic;
using System.Text;

namespace TreeHouse.PacketDocs.Codegen;

internal readonly record struct Size(
    int Constant,
    string Expression
) {
    public readonly bool IsConstant => Expression.Length == 0;
}

internal class SizeBuilder
{
    public int SizeConstant { get; private set; } = 0;
    
    private readonly List<string> sizeExpressions = new();
    public IEnumerable<string> SizeExpressions => sizeExpressions;

    public bool IsConstant => sizeExpressions.Count == 0;

    public void AddConstant(int c) => SizeConstant += c;

    public void AddExpression(string expression) => sizeExpressions.Add(expression);

    public Size GetSize()
    {
        StringBuilder builder = new();

        if (SizeConstant > 0 || sizeExpressions.Count == 0)
        {
            builder.Append(SizeConstant);
            if (sizeExpressions.Count > 0)
                builder.Append(" + ");
        }

        builder.AppendJoin(" + ", sizeExpressions);

        return new Size(SizeConstant, builder.ToString());
    }
}
