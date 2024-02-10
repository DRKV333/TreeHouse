using System.Collections.Generic;
using PacketFormat;
using YamlDotNet.Serialization;

namespace PacketDocs.Lua;

internal class LuaPacketFormatDocument
{
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
    public int? Inherit { get; set; }

    [YamlIgnore]
    public PacketDefinition Original { get; set; } = null!;
}

internal class StructureFieldType : IFieldType
{
    public string Name { get; set; } = "";

    public int Index { get; set; }
}

public class LuaLimitedStringFieldType : IFieldType
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "";

    [YamlMember(Alias = "maxlen")]
    public object Maxlen { get; set; } = 0;
}

public class LuaArrayFieldType : IFieldType
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "array";

    [YamlMember(Alias = "len")]
    public object Len { get; set; } = 0;

    [YamlMember(Alias = "type")]
    public IFieldType Type { get; set; } = null!;
}
