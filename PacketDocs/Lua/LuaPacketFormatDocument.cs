using System.Collections.Generic;
using PacketFormat;
using YamlDotNet.Serialization;

namespace PacketDocs.Lua;

internal class LuaPacketFormatDocument
{
    [YamlMember(Alias = "fieldDefinitions")]
    public List<LuaField> FieldDefinitions { get; set; } = new();

    [YamlMember(Alias = "idLength")]
    public int IdLength { get; set; }

    [YamlMember(Alias = "byId")]
    public Dictionary<int, object> ById { get; set; } = new();

    [YamlMember(Alias = "packets")]
    public List<LuaPacketDefinition> Packets { get; set; } = new();

    [YamlMember(Alias = "structures")]
    public List<NamedFieldsList> Structures { get; set; } = new();

    [YamlMember(Alias = "branches")]
    public List<LuaBranchDescription> Branches { get; set; } = new();

    public static LuaLiteralSerializer CreateSerializer()
    {
        LuaLiteralSerializer serializer = new();
        serializer.AddWhitelistAssembly(typeof(PacketFormatDocument).Assembly);
        serializer.AddObjectTransformer<PrimitiveFieldType>(x => x.Value);
        serializer.AddObjectTransformer<StructureFieldType>(x => x.Index);
        serializer.AddObjectTransformer<LuaFieldIndex>(x => x.Index);
        return serializer;
    }
}

internal class NamedFieldsList : FieldsList
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "";
}

internal class LuaPacketDefinition : NamedFieldsList
{
    [YamlMember(Alias = "inherit")]
    public int? Inherit { get; set; } = null;

    [YamlIgnore]
    public string? InheritName { get; set; } = null;
}

internal class StructureFieldType : IFieldType
{
    public string Name { get; set; } = "";

    public int Index { get; set; }
}

internal class LuaField : Field
{
    [YamlMember(Alias = "abbrev")]
    public string Abbrev { get; set; } = "";

    [YamlMember(Alias = "stash")]
    public int? Stash { get; set; } = null;
}

internal class LuaFieldIndex : IFieldItem
{
    public int Index { get; set; }
}

internal class LuaFieldWithLengthOverride : IFieldItem
{
    [YamlMember(Alias = "index")]
    public int Index { get; set; } = 0;

    [YamlMember(Alias = "len")]
    public int Len { get; set; } = 0;
}

internal class LuaArrayFieldType : IFieldType
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "array";

    [YamlMember(Alias = "items")]
    public int Items { get; set; }
}

internal class LuaBranch : IFieldItem
{
    [YamlMember(Alias = "branch")]
    public LuaBranchDetails Details { get; set; } = null!;
}

internal class LuaBranchDetails
{
    [YamlMember(Alias = "index")]
    public int Index { get; set; }

    [YamlMember(Alias = "isFalse")]
    public FieldsList? IsFalse { get; set; }

    [YamlMember(Alias = "isTrue")]
    public FieldsList? IsTrue { get; set; }
}

internal class LuaBranchDescription
{
    [YamlMember(Alias = "abbrev")]
    public string Abbrev { get; set; } = "";

    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "";

    [YamlMember(Alias = "field")]
    public int FieldIndex { get; set; }

    [YamlMember(Alias = "test_equal")]
    public int? TestEqual { get; set; }

    [YamlMember(Alias = "test_flag")]
    public int? TestFlag { get; set; }
}