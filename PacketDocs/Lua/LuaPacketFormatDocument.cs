using System.Collections.Generic;
using PacketFormat;
using YamlDotNet.Serialization;

namespace PacketDocs.Lua;

internal class LuaPacketFormatDocument
{
    [YamlMember(Alias = "fieldDefinitions")]
    public List<LuaField> FieldDefinitions { get; set; } = new();

    [YamlMember(Alias = "byId")]
    public Dictionary<int, Dictionary<int, int>> ById { get; set; } = new();

    [YamlMember(Alias = "packets")]
    public List<LuaPacketDefinition> Packets { get; set; } = new();

    [YamlMember(Alias = "structures")]
    public List<NamedFieldsList> Structures { get; set; } = new();

    public static LuaLiteralSerializer CreateSerializer()
    {
        LuaLiteralSerializer serializer = new();
        serializer.AddWhitelistAssembly(typeof(PacketFormatDocument).Assembly);
        serializer.AddObjectTransformer<PrimitiveFieldType>(x => x.Value);
        serializer.AddObjectTransformer<StructureFieldType>(x => x.Index);
        serializer.AddObjectTransformer<LuaFieldIndex>(x => x.Index);
        serializer.IgnoreProperty<LuaBranchDetails>(nameof(LuaBranchDetails.Field));
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

public class LuaField : Field
{
    [YamlMember(Alias = "abbrev")]
    public string Abbrev { get; set; }

    [YamlMember(Alias = "stash")]
    public int? Stash { get; set; } = null;
}

public class LuaFieldIndex : IFieldItem
{
    public int Index { get; set; }
}

public class LuaFieldWithLengthOverride : IFieldItem
{
    [YamlMember(Alias = "index")]
    public int Index { get; set; } = 0;

    [YamlMember(Alias = "len")]
    public int Len { get; set; } = 0;
}

public class LuaArrayFieldType : IFieldType
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "array";

    [YamlMember(Alias = "items")]
    public int Items { get; set; }
}

public class LuaBranch : IFieldItem
{
    [YamlMember(Alias = "branch")]
    public LuaBranchDetails Details { get; set; } = null!;
}

public class LuaBranchDetails : BranchDetails
{
    [YamlMember(Alias = "field")]
    public int FieldIndex { get; set; }
}