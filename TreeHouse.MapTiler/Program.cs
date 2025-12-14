using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using TreeHouse.Common;
using TreeHouse.Common.CommandLine;
using TreeHouse.Common.SQLite;
using TreeHouse.MapTiler;
using TreeHouse.MapTiler.GeoJson;
using UELib;
using UELib.Core;

await new RootCommand()
{
    new Command("convert")
    {
        new Option<DirectoryInfo>(["-s", "--source"]).ExistingOnly().Required(),
        new Option<DirectoryInfo>(["-t", "--target"]).Required()
    }.WithHandler(Convert),

    new Command("extract-info")
    {
        new Option<FileInfo>(["-p", "--package"]).ExistingOnly().Required(),
        new Option<FileInfo>(["-o", "--out"]).Required()
    }.WithHandler(ExtractInfo),

    new Command("extract-geojson")
    {
        new Option<FileInfo>(["-d", "--instance-db"]).ExistingOnly().Required(),
        new Option<FileInfo>(["-i", "--map-info"]).ExistingOnly().Required(),
        new Option<DirectoryInfo>(["-o", "--output"]).Required()
    }.WithHandler(ExtractGeoJson)
}
.InvokeAsync(args);

const int SlippyTileSize = 256;

static async Task Convert(DirectoryInfo source, DirectoryInfo target)
{
    (int originalTileSize, Image stiched) = await LoadStiched(source);
    await WriteSlippyTiles(stiched, target, originalTileSize);
}

static async Task WriteSlippyTiles(Image image, DirectoryInfo target, int originalTileSize)
{
    int bigDimention = Math.Max(image.Width, image.Height);
    int zoomLevels = Math.Max(1, (int)MathF.Ceiling(MathF.Log2((float)bigDimention / SlippyTileSize)) + 1);

    int maxTileCount = 1 << (zoomLevels - 1);
    int tileWidth = image.Width / maxTileCount;
    int tileHeight = image.Height / maxTileCount;

    Console.WriteLine($"Creating {tileWidth}x{tileHeight} slippy tiles with {zoomLevels} levels...");

    ConvertInfo info = new()
    {
        TileWidth = tileWidth,
        TileHeight = tileHeight,
        OriginalTileSize = originalTileSize,
        MaxZoom = zoomLevels - 1
    };

    using (Stream fs = File.Create(Path.Join(target.FullName, "ConvertInfo.json")))
    {
        await JsonSerializer.SerializeAsync(fs, info);
    }

    for (int i = 0; i < zoomLevels; i++)
    {
        int tileCount = 1 << i;
        int scaledWidth = tileWidth * tileCount;
        int scaledHeight = tileHeight * tileCount;

        Console.WriteLine($"Level: {i}, Scaled Size: {scaledWidth}x{scaledHeight}");

        Image? scaled = null;

        Image zoomSource;
        if (i == zoomLevels - 1)
        {
            zoomSource = image;
        }
        else
        {
            scaled = image.Clone(x => x.Resize(scaledWidth, scaledHeight, new BicubicResampler()));
            zoomSource = scaled;
        }

        try
        {
            string zoomDir = Path.Join(target.FullName, i.ToString());

            for (int x = 0; x < tileCount; x++)
            {
                string xDir = Path.Join(zoomDir, x.ToString());
                Directory.CreateDirectory(xDir);

                for (int y = 0; y < tileCount; y++)
                {
                    using Image tile = zoomSource.Clone(c => c.Crop(new Rectangle(x * tileWidth, y * tileHeight, tileWidth, tileHeight)));
                    await tile.SaveAsPngAsync(Path.Join(xDir, $"{y}.png"));
                }
            }
        }
        finally
        {
            scaled?.Dispose();
        }
    }
}

static async Task<(int, Image)> LoadStiched(DirectoryInfo source)
{
    (int xSize, int ySize, string ext) = DetectSizeInTiles(source);
    if (xSize == 0 || ySize == 0)
        throw new InvalidOperationException("No tiles to load.");

    Console.WriteLine($"Map size in tiles: {xSize}x{ySize}");

    int tileSize = 0;
    Image<Rgb24>? stiched = null;

    Console.WriteLine("Loading source tiles...");

    try
    {
        for (int i = 0; i < xSize; i++)
        {
            for (int j = 0; j < ySize; j++)
            {
                string tileFileName = $"Tile_{i}_{j}{ext}";
                string tileFilePath = Path.Join(source.FullName, tileFileName);

                using Image tile = await Image.LoadAsync(tileFilePath);

                if (stiched == null)
                {
                    tileSize = tile.Width;

                    Console.WriteLine($"Tile size: {tileSize}");

                    int stichedWidth = tileSize * xSize;
                    int stichedHeight = tileSize * ySize;

                    Console.WriteLine($"Stiched image size: {stichedWidth}x{stichedHeight}");

                    stiched = new Image<Rgb24>(stichedWidth, stichedHeight, new Rgb24(0, 0, 0));
                }

                if (tile.Width != tileSize || tile.Height != tileSize)
                    throw new InvalidOperationException($"{tileFileName}: Wrong tile size!");

                stiched.Mutate(x => x.DrawImage(tile, new Point(i * tileSize, j * tileSize), 1));
            }
        }
    }
    catch (Exception)
    {
        stiched?.Dispose();
        throw;
    }

    return (tileSize, stiched!);
}

static (int, int, string) DetectSizeInTiles(DirectoryInfo source)
{
    FileInfo[] files = source.GetFiles("Tile_*_*.*");

    if (files.Length == 0)
        return (0, 0, "");

    string ext = Path.GetExtension(files.First().Name);
    Console.WriteLine($"Ext: {ext}");

    List<string> fileNames = files.Select(x => x.Name).ToList();

    int x = 0;

    while (fileNames.Contains($"Tile_{x}_0{ext}"))
    {
        x++;
    }

    int y = 0;

    while (fileNames.Contains($"Tile_0_{y}{ext}"))
    {
        y++;
    }

    return (x, y, ext);
}

static void ExtractInfo(FileInfo packageFile, FileInfo outFile)
{
    Regex boxRegex = new(@"^\(Min=\(X=(?<minx>[-\d.]+),Y=(?<miny>[-\d.]+),Z=[-\d.]+[-\d.]+\),Max=\(X=(?<maxx>[-\d.]+),Y=(?<maxy>[-\d.]+),Z=[-\d.]+\),IsValid=1\)$");

    using UnrealPackage package = UnrealLoader.LoadPackage(packageFile.FullName, FileAccess.Read);

    package.InitializePackage();

    List<MapInfo> mapInfos = new();

    foreach (UObject obj in package.Objects)
    {
        if (obj.Class != null && obj.Class.Name == "RUFloorMapInfo")
        {
            MapInfo info = new();
            mapInfos.Add(info);

            obj.Load<UObjectStream>();

            string path = obj.GetPath();
            Console.WriteLine(path);

            string[] pathParts = path.Split('.');
            info.PackageName = pathParts[0];
            info.ZoneName = pathParts[1];

            UDefaultProperty? idProp = obj.Properties.Find("WorldID");
            if (idProp != null)
                info.WorldId = int.Parse(idProp.Value);

            UDefaultProperty? unitsProp = obj.Properties.Find("UnitsPerPixel");
            if (unitsProp != null)
                info.UnitsPerPixel = float.Parse(unitsProp.Value);

            UDefaultProperty? bboxProp = obj.Properties.Find("BoundingBox");
            if (bboxProp != null && boxRegex.TryMatch(bboxProp.Value, out Match match))
            {
                info.MinX = float.Parse(match.Groups["minx"].Value);
                info.MinY = float.Parse(match.Groups["miny"].Value);
                info.MaxX = float.Parse(match.Groups["maxx"].Value);
                info.MaxY = float.Parse(match.Groups["maxy"].Value);
            }
        }
    }

    using Stream fs = outFile.Create();
    JsonSerializer.Serialize(fs, mapInfos);
}

static async Task ExtractGeoJson(FileInfo instanceDb, FileInfo mapInfoFile, DirectoryInfo output)
{
    IList<MapInfo> mapInfos;
    using (Stream fs = mapInfoFile.OpenRead())
    {
        mapInfos = JsonSerializer.Deserialize<IList<MapInfo>>(fs)!;
    }

    Dictionary<int, List<(MapInfo info, GeoJsonFeatureCollection geoJson)>> zonesByWorldId = mapInfos
        .GroupBy(x => x.WorldId)
        .ToDictionary(
            x => x.Key,
            x => x.Select(z => (z, new GeoJsonFeatureCollection() { Features = new List<GeoJsonFeature>() })).ToList()
        );

    output.Create();

    using SqliteConnection connection = SqliteUtils.Open(instanceDb.FullName);

    using SqliteCommand posQuery = connection.CreateCommand();

    posQuery.CommandText =  """
                            SELECT
                                Instance.uxInstanceGuid,
                                Instance.sEditorName,
                                Instance.dataJSON ->> "pos" AS pos,
                                WorldDef.ixWorldID
                            FROM Instance
                            JOIN Zone ON Instance.uxZoneGuid = Zone.uxZoneGuid
                            JOIN WorldDef ON Zone.uxWorldDefGuid = WorldDef.uxWorldDefGuid
                            WHERE pos IS NOT NULL
                            """;

    using SqliteDataReader reader = posQuery.ExecuteReader();

    HashSet<int> missingWorlds = new();

    while (reader.Read())
    {
        string instanceUid = reader.GetString(0);
        string instanceEditorName = reader.GetString(1);
        string instancePos = reader.GetString(2);
        int worldId = reader.GetInt32(3);

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

        if (!zonesByWorldId.TryGetValue(worldId, out var zones))
        {
            if (missingWorlds.Add(worldId))
                Console.WriteLine($"No map info for WorldDef with id '{worldId}' found for instance '{instanceUid}'.");
            continue;
        }

        double x = posParsed[0];
        double y = posParsed[1];

        GeoJsonFeature feature = new()
        {
            Id = JsonValue.Create(instanceUid),
            Geometry = new PointGeometry()
            {
                Coordinates = [y, x]
            },
            Properties = new JsonObject()
            {
                ["name"] = instanceEditorName
            }
        };

        GeoJsonFeatureCollection collection;
        if (zones.Count == 1)
        {
            collection = zones[0].geoJson;
        }
        else
        {
            (_, collection) = zones.FirstOrDefault(i =>
                x >= i.info.MinX && x <= i.info.MaxX &&
                y >= i.info.MinY && y <= i.info.MaxY
            );
        }

        if (collection != null)
            collection.Features.Add(feature);
        else
            Console.WriteLine($"Instance '{instanceUid}' is not in any zone of world '{worldId}'."); // TODO: Do something about these.
    }

    JsonSerializerOptions jsonOptions = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver().WithGeoJsonTypesModifier()
    };

    foreach ((MapInfo info, GeoJsonFeatureCollection geoJson) in zonesByWorldId.Values.SelectMany(x => x))
    {
        string outName = $"{info.PackageName}.{info.ZoneName}.geojson";
        Console.WriteLine(outName);

        string outPath = Path.Join(output.FullName, outName);

        using Stream fs = File.Create(outPath);
        await JsonSerializer.SerializeAsync(fs, geoJson, jsonOptions);
    }
}