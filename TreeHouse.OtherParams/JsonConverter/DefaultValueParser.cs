using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace TreeHouse.OtherParams.JsonConverter;

public static class DefaultValueParser
{
    private delegate JsonNode? JsonParser(string s);

    private static readonly Dictionary<ParamType, JsonParser> JsonReaders = new()
    {
        [ParamType.Float] = ReadFloat,
        [ParamType.String] = ReadString,
        [ParamType.JSON] = s => JsonNode.Parse(s.Replace("\\\"", "\"")),
        [ParamType.ContentRef] = ReadString,
        [ParamType.ContentRefAndInt] = ReadString,
        [ParamType.ContentRefList] = ReadString,
        [ParamType.InstanceGroup] = ReadString,
        [ParamType.Int] = ReadInt,
        [ParamType.Int64] = s => JsonValue.Create(long.Parse(s)),
        [ParamType.AvatarID] = s => s == "#0" ? JsonValue.Create(0) : throw new FormatException($"Don't know how to parse non-zero AvatarID"),
        [ParamType.Bool] = s => JsonValue.Create(bool.Parse(s)),
        [ParamType.Guid] = ReadGuid,
        [ParamType.LocalizedString] = ReadGuid,
        [ParamType.FloatRange] = s => ReadArray(s, ReadFloat),
        [ParamType.Vector3] = s => ReadArray(s, ReadFloat, ' '),
        [ParamType.IntVector] = s => ReadArray(s, ReadInt),
        [ParamType.StringVector] = s => ReadArray(s, ReadString),
        [ParamType.LocalizedStringVector] = s => ReadArray(s, ReadGuid),
        [ParamType.BitSetFilter] = s => s.All(x => x == '0') ? JsonValue.Create(0) : throw new FormatException($"Don't know how to parse non-zero BitSetFilter")
    };

    private static JsonValue ReadString(string s) => JsonValue.Create(s);

    private static JsonValue ReadFloat(string s) => JsonValue.Create(float.Parse(s));

    private static JsonValue ReadInt(string s) => JsonValue.Create(int.Parse(s));

    private static JsonValue ReadGuid(string s) => JsonValue.Create(Guid.Parse(s));

    private static JsonArray ReadArray(string s, Func<string, JsonValue> readItem, char separator = ',') => new JsonArray(s
        .Split(separator, StringSplitOptions.RemoveEmptyEntries)
        .Select(x =>
            readItem(x.Trim(' ', '[', ']'))
        )
        .ToArray()
    );

    private static JsonNode? ParseDefaultValue(int paramId, ParamType type, string defaultValue)
    {
        try
        {
            if (!JsonReaders.TryGetValue(type, out JsonParser? parser))
                throw new FormatException($"Don't know how to parse default value of type {type}");

            return parser(defaultValue);
        }
        catch (Exception e)
        {
            throw new FormatException($"Failed to parse default value for param definition {paramId} of type {type}: {defaultValue}", e);
        }
    }

    public static async Task<DefaultValueProvider<int>> CreateProvider(ParamDb db) => new DefaultValueProvider<int>(
        await db.Classes
            .Include(x => x.DeclaredParams)
            .ThenInclude(x => x.Definition)
            .ToDictionaryAsync(
                x => x.UniqueId,
                x => new JsonObject(
                    x.DeclaredParams
                        .Where(p => p.Definition.Default != null)
                        .Select(p => new KeyValuePair<string, JsonNode?>(
                            p.Definition.Name,
                            ParseDefaultValue(p.ParamId, p.Definition.TypeId, p.Definition.Default!)
                        ))
                )
            )
    );
}
