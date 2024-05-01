using System;
using System.Collections.Generic;

namespace TreeHouse.PacketDocs.Codegen;

internal class SizeResolver
{
    private class TypeData
    {
        public int? Size { get; set; } = null;

        public bool HasSize { get; set; } = false;

        public required Func<int?> Resolve { get; init; }
    }

    public readonly struct SelfToken
    {
        public SizeResolver Resolver { get; }

        private readonly string typeName;

        public SelfToken(SizeResolver resolver, string typeName)
        {
            Resolver = resolver;
            this.typeName = typeName;
        }

        public void SetResolveDelegate(Func<int?> resolve)
        {
            if (Resolver.types.ContainsKey(typeName))
                throw new InvalidOperationException($"Type {typeName} already has a delegate.");

            Resolver.types.Add(typeName, new TypeData() { Resolve = resolve });
        }
    }

    public readonly struct ReferenceToken
    {
        public SizeResolver Resolver { get; }

        private readonly string typeName;

        public ReferenceToken(SizeResolver resolver, string typeName)
        {
            Resolver = resolver;
            this.typeName = typeName;
        }

        public int? GetSize()
        {
            if (!Resolver.types.TryGetValue(typeName, out TypeData? data))
                throw new InvalidOperationException($"Type {typeName} does not exist.");

            if (data.HasSize)
                return data.Size;

            data.Size = data.Resolve();
            data.HasSize = true;

            return data.Size;
        }
    }

    private readonly Dictionary<string, TypeData> types = new();

    public SelfToken CreateSelfToken(string typeName) => new SelfToken(this, typeName);

    public ReferenceToken CreateReferenceToken(string typeName) => new ReferenceToken(this, typeName);
}
