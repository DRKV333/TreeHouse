using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TreeHouse.Common.CommandLine;
using TreeHouse.Common.SQLite;
using TreeHouse.OtherParams;
using TreeHouse.OtherParams.GeoJson;
using TreeHouse.OtherParams.JsonConverter;
using TreeHouse.OtherParams.Model;
using TreeHouse.OtherParams.Parsing;

await new RootCommand()
{
    new Command("parse")
    {
        new Option<FileInfo>(["--param-db", "-d"]).Required(),
        new Option<FileInfo>(["--param-list", "-l"]).ExistingOnly().Required()
    }.WithHandler(ParseHandler),

    new Command("print")
    {
        new Option<FileInfo>(["--param-db", "-d"]).ExistingOnly().Required(),
        new Option<FileInfo>(["--output", "-o"]).Required()
    }.WithHandler(PrintHandler),

    new Command("json-convert")
    {
        new Option<FileInfo>(["--param-db", "-d"]).ExistingOnly().Required(),
        new Option<FileInfo>(["--content-db", "-c"]).ExistingOnly(),
        new Option<FileInfo>(["--instance-db", "-i"]).ExistingOnly(),
        new Option<bool>("--write-jsonb"),
        new Option<bool>("--write-unformatted"),
        new Option<bool>("--no-write-json"),
        new Option<bool>("--no-defaults")
    }.WithHandler(JsonConvertHandler),

    new Command("extract-geojson")
    {
        new Option<FileInfo>(["--instance-db", "-i"]).ExistingOnly().Required(),
        new Option<DirectoryInfo>(["--output", "-o"]).Required()
    }.WithHandler(ExtractGeoJson)
}
.InvokeAsync(args);

static async Task ParseHandler(FileInfo paramDb, FileInfo paramList)
{
    ParamlistParser parser = new();
    
    using TextReader reader = paramList.OpenText();
    await parser.ReadParamlistAsync(reader);

    ParamDb db = ParamDb.Open(paramDb.FullName, write: true);
    await db.Database.EnsureCreatedAsync();
    await parser.WriteDbAsync(db);
    await db.SaveChangesAsync();
}

static async Task PrintHandler(FileInfo paramDb, FileInfo output)
{
    ParamDb db = ParamDb.Open(paramDb.FullName);
    PumlTemplate template = new(db);
    using TextWriter writer = output.CreateText();
    await template.RenderAsync(writer);
}

static async Task JsonConvertHandler(FileInfo paramDb, FileInfo? contentDb, FileInfo? instanceDb, bool writeJsonb, bool writeUnformatted, bool noWriteJson, bool noDefaults)
{
    if (contentDb == null && instanceDb == null)
    {
        Console.WriteLine("Neither content, nor instance db was given, nothing to do.");
        return;
    }

    using ParamDb db = ParamDb.Open(paramDb.FullName);
    ParamSetJsonConverter converter = await ParamSetJsonConverter.CreateInstance(db);

    ContentDbJsonConverter? contentDbConverter = null;

    if (contentDb != null)
    {
        DefaultValueProvider<int>? paramDefaultsProvider = noDefaults ? null : await DefaultValueParser.CreateProvider(db);

        using SqliteConnection connection = SqliteUtils.Open(contentDb.FullName, write: true);
        contentDbConverter = new(converter, paramDefaultsProvider);

        Console.WriteLine("Reading content db...");

        foreach (Table table in await db.Tables.ToListAsync())
        {
            Console.WriteLine($"Reading table {table.Name}...");
            contentDbConverter.ReadParamSets(connection, table);
        }

        Console.WriteLine("Writing json to content db...");

        contentDbConverter.WriteParamSetsJson(
            connection: connection,
            writeJson: !noWriteJson,
            formatJson: !writeUnformatted,
            writeJsonb: writeJsonb
        );
    }

    if (instanceDb != null)
    {
        DefaultValueProvider<Guid>? contentDefaultsProvider = noDefaults ? null : contentDbConverter?.GetDefaultValueProvider();

        using SqliteConnection connection = SqliteUtils.Open(instanceDb.FullName, write: true);
        InstanceDbJsonConverter instanceDbConverter = new(converter, contentDefaultsProvider);

        Console.WriteLine("Reading instance db...");

        instanceDbConverter.ReadParamSets(connection);

        Console.WriteLine("Writing json to instance db...");

        instanceDbConverter.WriteParamSetsJson(
            connection: connection,
            writeJson: !noWriteJson,
            formatJson: !writeUnformatted,
            writeJsonb: writeJsonb
        );
    }

    Console.WriteLine("Done.");
}

static async Task ExtractGeoJson(FileInfo instanceDb, DirectoryInfo output)
{
    output.Create();

    using SqliteConnection connection = SqliteUtils.Open(instanceDb.FullName);

    Dictionary<string, (string name, GeoJsonFeatureCollection features)> geoJsonByWorld = GetWorldDefs(connection)
        .ToDictionary(
            x => x.uid,
            x => (x.name, new GeoJsonFeatureCollection() { Features = new List<GeoJsonFeature>() })
        );

    using SqliteCommand posQuery = connection.CreateCommand();

    posQuery.CommandText = """
SELECT
	Instance.uxInstanceGuid,
	Instance.sEditorName,
	Instance.dataJSON ->> "pos" AS pos,
    Zone.uxWorldDefGuid
FROM Instance
JOIN Zone ON Instance.uxZoneGuid = Zone.uxZoneGuid
WHERE pos IS NOT NULL
""";

    using SqliteDataReader reader = posQuery.ExecuteReader();

    while (reader.Read())
    {
        string instanceUid = reader.GetString(0);
        string instanceEditorName = reader.GetString(1);
        string instancePos = reader.GetString(2);
        string worldUid = reader.GetString(3);

        double[] posParsed;
        try
        {
            posParsed = JsonSerializer.Deserialize<double[]>(instancePos)!;
        }
        catch (JsonException e)
        {
            Console.WriteLine($"Failed to parse position '{instancePos}' for instance '{instanceUid}': {e}");
            continue;
        }

        if (!geoJsonByWorld.TryGetValue(worldUid, out var geoJson))
        {
            Console.WriteLine($"No WorldDef with id '{worldUid}' found for instance '{instanceUid}'.");
            continue;
        }

        geoJson.features.Features.Add(new GeoJsonFeature()
        {
            Id = JsonValue.Create(instanceUid),
            Geometry = new PointGeometry()
            {
                Coordinates = [posParsed[1], posParsed[0]]
            },
            Properties = new JsonObject()
            {
                ["name"] = instanceEditorName
            }
        });
    }

    JsonSerializerOptions jsonOptions = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver().WithGeoJsonTypesModifier()
    };

    foreach (var world in geoJsonByWorld.Values)
    {
        Console.WriteLine(world.name);

        string outPath = Path.Join(output.FullName, $"{world.name}.geojson");

        using Stream fs = File.Create(outPath);
        await JsonSerializer.SerializeAsync(fs, world.features, jsonOptions);
    }
}

static IEnumerable<(string uid, string name)> GetWorldDefs(SqliteConnection connection)
{
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = "SELECT uxWorldDefGuid, sWorldDef FROM WorldDef";

    using SqliteDataReader reader = command.ExecuteReader();

    while (reader.Read())
    {
        string worldUid = reader.GetString(0);
        string worldName = reader.GetString(1);

        yield return (worldUid, worldName);
    }
}