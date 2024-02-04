using System.Collections.Generic;
using PacketFormat;
using YamlDotNet.Serialization;

namespace PacketDocs.Lua;

internal class LuaPacketFormatDocument : PacketFormatDocument
{
    [YamlMember(Alias = "byId")]
    public Dictionary<int, Dictionary<int, string>> ById { get; set; } = new();
}