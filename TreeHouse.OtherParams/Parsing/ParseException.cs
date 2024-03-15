using System;

namespace TreeHouse.OtherParams.Parsing;

public class ParseException : Exception
{
    public string Line { get; }
    public int LineNumber { get; }

    public ParseException(string line, int lineNumber, string? message = null, Exception? inner = null) : base(message, inner)
    {
        Line = line;
        LineNumber = lineNumber;
    }

    public override string Message => $"{base.Message} ({LineNumber}:{Line})";
}
