using System;
using YamlDotNet.Core;
using YamlDotNet.Serialization.BufferedDeserialization.TypeDiscriminators;

namespace PacketDocs.Yaml;

internal class DefaultTypeDescriminator<TBase, TDefault> : ITypeDiscriminator where TDefault : TBase
{
    public static DefaultTypeDescriminator<TBase, TDefault> Instance { get; } = new();

    public Type BaseType => typeof(TBase);

    public bool TryDiscriminate(IParser buffer, out Type? suggestedType)
    {
        suggestedType = typeof(TDefault);
        return true;
    }
}
