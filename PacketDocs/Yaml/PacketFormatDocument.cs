using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PacketDocs.Yaml;

public class PacketFormatDocument
{
    /// <summary>
    /// A dictionary of packet descriptions, with keys
    /// corresponding to class names from the game client.
    /// </summary>
    [YamlMember(Alias = "packets")]
    public Dictionary<string, PacketDefinition> Packets { get; set; } = new();

    /// <summary>
    /// A dictionary of user defined data structures,
    /// which can be referenced in place of field types,
    /// by prefixing their keys with ':'.
    /// </summary>
    [YamlMember(Alias = "structures")]
    public Dictionary<string, FieldsList> Structures { get; set; } = new();

    public static IDeserializer CreateDeserializer() => new DeserializerBuilder()
        .WithNamingConvention(NullNamingConvention.Instance)
        .WithNodeDeserializer(ScalarWrapperTypeDiscriminatingNodeDeserializer<IFieldType, PrimitiveFieldType>.Instance)
        .WithTypeDiscriminatingNodeDeserializer(x =>
        {
            x.AddUniqueKeyTypeDiscriminator<IFieldItem>(new Dictionary<string, Type>
            {
                ["branch"] = typeof(Branch)
            });
            x.AddTypeDiscriminator(DefaultTypeDescriminator<IFieldItem, Field>.Instance);

            x.AddUniqueKeyTypeDiscriminator<IFieldType>(new Dictionary<string, Type>
            {
                ["maxlen"] = typeof(LimitedStringFieldType),
                ["len"] = typeof(ArrayFieldType),
                ["enum"] = typeof(EnumFieldType)
            });
        })
        .Build();
}

public class FieldsList
{
    /// <summary>
    /// A list of fields that make up the defined object. Fields are serialized sequentially with
    /// no padding.
    /// </summary>
    [YamlMember(Alias = "fields")]
    public List<IFieldItem> Fields { get; set; } = new();
}

/// <summary>
/// The definition of a data packet, sent or received by the game client. The actual binary
/// representation contains two bytes of identifiers, followed by the listed fields.
/// </summary>
public class PacketDefinition : FieldsList
{
    /// <summary>
    /// First byte of the packet body, identifies the packet type when combined with the subId.
    /// Packets with the same id are usually related.
    /// </summary>
    [YamlMember(Alias = "id")]
    public byte Id { get; set; }

    /// <summary>
    /// Second byte of the packet body, identifies the packet type when combined with the id.
    /// </summary>
    [YamlMember(Alias = "subId")]
    public byte SubId { get; set; }

    /// <summary>
    /// The fields of the specified packet are also part of the current packet, appearing before
    /// the field list defined here.
    /// </summary>
    [YamlMember(Alias = "inherit")]
    public string? Inherit { get; set; }
}

public interface IFieldItem
{
}

public class Branch : IFieldItem
{
    [YamlMember(Alias = "branch")]
    public BranchDetails Details { get; set; } = null!;
}

/// <summary>
/// This field contains a nested field list, whose structure can vary based on a
/// condition.
/// </summary>
public class BranchDetails
{
    /// <summary>
    /// The name of a previously defined field, which forms the basis of the condition. This
    /// field should have an intrinsic boolean or integer type. If an integer type field is used,
    /// the branch condition should be specified by test_flag or test_equal. Otherwise the branch
    /// condition is the boolean field's value.
    /// </summary>
    [YamlMember(Alias = "field")]
    public string Field { get; set; } = "";

    /// <summary>
    /// When used with an integer type field, defined the branch condition as an equality test
    /// against a constant value. field == test_equal
    /// </summary>
    [YamlMember(Alias = "test_equal")]
    public int? TestEqual { get; set; }

    /// <summary>
    /// When used with an integer type field, defines the branch condition as checking against a
    /// constant bitmask. (field & test_flag) == test_flag
    /// </summary>
    [YamlMember(Alias = "test_flag")]
    public int? TestFlag { get; set; }

    /// <summary>
    /// These fields are added to the object, if the branch condition evaluates to 'false'.
    /// </summary>
    [YamlMember(Alias = "isFalse")]
    public FieldsList? IsFalse { get; set; }

    /// <summary>
    /// These fields are added to the object, if the branch condition evaluates to 'true'.
    /// </summary>
    [YamlMember(Alias = "isTrue")]
    public FieldsList? IsTrue { get; set; }
}

public class Field : IFieldItem
{
    /// <summary>
    /// The field's name, as it will be identified in generated code. This should be unique in
    /// every possible branch. Fields with the same name should also have the same type, except
    /// for array or string lengths.
    /// </summary>
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }

    /// <summary>
    /// The field's data type. This can be an intrinsic type, a user defined data type prefixed with
    /// ':', an array type, a length limited string type, or an enumeration type.
    /// </summary>
    [YamlMember(Alias = "type")]
    public IFieldType Type { get; set; } = null!;
}

public interface IFieldType
{
}

public class PrimitiveFieldType : ScalarWrapperType, IFieldType
{
}

public class LimitedStringFieldType : IFieldType
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// The maximum length of the string in bytes. The game client will not read anything longer
    /// than this. If the client tries to send anything longer than this, it will be truncated.
    /// May be specified as a constant or as the name of a previously defined integer type field.
    /// </summary>
    [YamlMember(Alias = "maxlen")]
    public string Maxlen { get; set; } = "";
}

public class ArrayFieldType : IFieldType
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// The number if fields in the sequence. May be specified as a constant or as the name of a
    /// previously defined integer type field.
    /// </summary>
    [YamlMember(Alias = "len")]
    public string Len { get; set; } = "";

    /// <summary>
    /// The type of the repeating fields. May be an intrinsic type or user defined type.
    /// </summary>
    [YamlMember(Alias = "type")]
    public string Type { get; set; } = "";
}

public class EnumFieldType : IFieldType
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// A dictionary of possible values for the integer type field. Keys are the possible values,
    /// values are names for these values, which will appear in generated code.
    /// </summary>
    [YamlMember(Alias = "enum")]
    public Dictionary<int, string> Enum { get; set; } = new();
}
