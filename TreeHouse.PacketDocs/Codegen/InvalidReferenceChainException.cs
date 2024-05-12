using System;
using System.Collections.Generic;
using TreeHouse.Common;

namespace TreeHouse.PacketDocs.Codegen;

public class InvalidReferenceChainException : Exception
{
    private readonly List<string> references = new();

    public IReadOnlyList<string> References => references;

    public InvalidReferenceChainException(string? message = null, string? reference = null, Exception? inner = null) : base(message, inner)
    {
        if (reference != null)
            AddReference(reference);
    }

    public void AddReference(string reference) => references.Add(reference);

    public override string Message => $"{base.Message} ({string.Join(" -> ", references.EnumerateBackwards())})";
}
