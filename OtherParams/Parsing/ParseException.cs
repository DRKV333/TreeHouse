using System;

namespace OtherParams.Parsing;

public class ParseException : Exception
{
    public string Line { get; }
    public int LineNumber { get; }

    public ParseException(string line, int lineNumber, string? message = null, Exception? inner = null) : base(message, inner)
    {
        Line = line;
        LineNumber = lineNumber;
    }
}
