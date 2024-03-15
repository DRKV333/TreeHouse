using System;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace TreeHouse.PacketFormat;

public abstract class ScalarWrapperType
{
    public string Value { get; set; } = "";
}

internal class ScalarWrapperTypeDiscriminatingNodeDeserializer<TDiscminated, TWrapper> : INodeDeserializer where TWrapper : ScalarWrapperType, TDiscminated, new()
{
    public static ScalarWrapperTypeDiscriminatingNodeDeserializer<TDiscminated, TWrapper> Instance { get; } = new();

    public bool Deserialize(IParser reader, Type expectedType, Func<IParser, Type, object?> nestedObjectDeserializer, out object? value)
    {
        if (expectedType == typeof(TDiscminated) && reader.TryConsume<Scalar>(out Scalar? scalar))
        {
            value = new TWrapper()
            {
                Value = scalar.Value
            };

            return true;
        }

        value = null;
        return false;
    }
}
