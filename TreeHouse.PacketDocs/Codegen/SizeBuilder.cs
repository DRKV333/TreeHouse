using System.Collections.Generic;
using System.Text;

namespace TreeHouse.PacketDocs.Codegen;

internal readonly record struct Size(
    int Constant,
    string? Expression
) {
    public readonly bool IsConstant => Expression == null;

    public override string ToString() => Expression ?? Constant.ToString();
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
        if (sizeExpressions.Count == 0)
        {
            return new Size(SizeConstant, null);
        }
        else
        {
            StringBuilder builder = new();

            if (SizeConstant > 0)
            {
                builder.Append(SizeConstant);
                builder.Append(" + ");
            }
            builder.AppendJoin(" + ", sizeExpressions);

            return new Size(SizeConstant, builder.ToString());
        }
    }
}
