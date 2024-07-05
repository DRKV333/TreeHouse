using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TreeHouse.Common.IO;
using TreeHouse.OtherParams.Model;

namespace TreeHouse.OtherParams.JsonConverter;

public class ParamSetJsonConverter
{
    private delegate JsonNode? JsonReader(ref SpanReader reader);

    private static readonly Dictionary<ParamType, JsonReader> JsonReaders = new()
    {
        [ParamType.Float] = (ref SpanReader r) => JsonValue.Create(r.ReadSingleLE()),
        [ParamType.String] = ReadString,
        [ParamType.JSON] = ReadJson,
        [ParamType.ContentRef] = ReadString,
        [ParamType.ContentRefAndInt] = ReadString,
        [ParamType.ContentRefList] = ReadString,
        [ParamType.InstanceGroup] = ReadString, // TODO: Parse some of these strings a bit more?
        [ParamType.Int] = (ref SpanReader r) => JsonValue.Create(r.ReadInt32LE()),
        [ParamType.Int64] = (ref SpanReader r) => JsonValue.Create(r.ReadInt64LE()),
        [ParamType.AvatarID] = (ref SpanReader r) => JsonValue.Create(r.ReadUInt64LE()),
        [ParamType.Bool] = (ref SpanReader r) => JsonValue.Create(r.ReadByte() != 0),
        [ParamType.Guid] = (ref SpanReader r) => JsonValue.Create(r.ReadGuidLE()),
        [ParamType.LocalizedString] = (ref SpanReader r) => JsonValue.Create(r.ReadGuidLE()),
        [ParamType.FloatRange] = (ref SpanReader r) => ReadFixedFloatArray(ref r, 2),
        [ParamType.Vector3] = (ref SpanReader r) => ReadFixedFloatArray(ref r, 3),
        [ParamType.IntVector] = (ref SpanReader r) => ReadVector(ref r, (ref SpanReader re) => JsonValue.Create(re.ReadInt32LE())),
        [ParamType.StringVector] = (ref SpanReader r) => ReadVector(ref r, ReadString),
        [ParamType.LocalizedStringVector] = (ref SpanReader r) => ReadVector(ref r, (ref SpanReader re) => JsonValue.Create(re.ReadGuidLE().ToString())),
    };

    private readonly Dictionary<ushort, ParamDeclaration> paramsById;

    private ParamSetJsonConverter(Dictionary<ushort, ParamDeclaration> paramsById)
    {
        this.paramsById = paramsById;
    }

    private static string ReadStringRaw(ref SpanReader reader)
    {
        ushort lenght = reader.ReadUInt16LE();
        return reader.ReadASCIIFixed(lenght);
    }

    private static JsonValue ReadString(ref SpanReader reader) => JsonValue.Create(ReadStringRaw(ref reader));

    private static JsonNode? ReadJson(ref SpanReader reader)
    {
        string str = ReadStringRaw(ref reader);
        if (string.IsNullOrWhiteSpace(str))
            return null;

        try
        {
            return JsonNode.Parse(
                str,
                documentOptions: new JsonDocumentOptions()
                {
                    AllowTrailingCommas = true
                }
            );
        }
        catch (JsonException)
        {
            // TODO: Fix these broken JSONs somehow.
            return JsonValue.Create(str);
        }
    }

    private static JsonArray ReadFixedFloatArray(ref SpanReader reader, int length)
    {
        JsonArray array = new();

        for (int i = 0; i < length; i++)
        {
            array.Add(JsonValue.Create(reader.ReadSingleLE()));
        }
        
        return array;
    }

    private static JsonArray ReadVector(ref SpanReader reader, JsonReader readElement)
    {
        JsonArray array = new();

        uint length = reader.ReadUInt32LE();
        for (uint i = 0; i < length; i++)
        {
            array.Add(readElement(ref reader));
        }
        
        return array;
    }

    public static async Task<ParamSetJsonConverter> CreateInstance(ParamDb db) =>
        new ParamSetJsonConverter(
            await db.ParamDeclarations
                .Include(x => x.Definition)
                .ToDictionaryAsync(x => (ushort)x.ParamId)
        );

    public void ConvertInto(JsonObject obj, SpanReader reader, int? classId = null)
    {
        byte firstByte = reader.ReadByte();
        if (firstByte != 1)
            throw new FormatException("ParamSet did not begin with 1");

        ushort fieldCount = reader.ReadUInt16LE();
        for (int i = 0; i < fieldCount; i++)
        {
            ushort paramId = reader.ReadUInt16LE();
            ParamType typeId = (ParamType)reader.ReadByte();

            if (!paramsById.TryGetValue(paramId, out ParamDeclaration? decl))
                throw new ParamParsingException(i, paramId, typeId, reader.Position, $"Unknown ParamId");

            if (classId != null && decl.ClassId != classId)
                throw new ParamParsingException(i, paramId, typeId, reader.Position, $"ParamSet of class {classId} contains param defined for class {decl.ClassId}");

            if (typeId != decl.Definition.TypeId)
                throw new ParamParsingException(i, paramId, typeId, reader.Position, $"Expected type was {decl.Definition.TypeId}");

            if (!JsonReaders.TryGetValue(typeId, out JsonReader? jsonReader))
                throw new ParamParsingException(i, paramId, typeId, reader.Position, $"Don't know how to read this type");

            obj[decl.Definition.Name] = jsonReader(ref reader);
        }

        if (!reader.EndOfBuffer)
            throw new FormatException("Some data from the ParamSet was not parsed");
    }
}
