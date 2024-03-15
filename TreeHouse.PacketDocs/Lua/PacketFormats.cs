using YamlDotNet.Serialization;

namespace TreeHouse.PacketDocs.Lua;

internal class PacketFormats
{
    [YamlMember(Alias = "main")]
    public LuaPacketFormatDocument Main { get; set; } = null!;

    [YamlMember(Alias = "nativeparam")]
    public LuaPacketFormatDocument Nativeparam { get; set; } = null!;
}
