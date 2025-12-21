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
using Microsoft.VisualBasic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata;
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
        new Option<DirectoryInfo>(["-i", "--images"]).ExistingOnly(),
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

static void ExtractInfo(FileInfo packageFile, DirectoryInfo imagesDir, FileInfo outFile)
{
    Regex boxRegex = new(@"^\(Min=\(X=(?<minx>[-\d.]+),Y=(?<miny>[-\d.]+),Z=[-\d.]+[-\d.]+\),Max=\(X=(?<maxx>[-\d.]+),Y=(?<maxy>[-\d.]+),Z=[-\d.]+\),IsValid=1\)$");

    using UnrealPackage package = UnrealLoader.LoadPackage(packageFile.FullName, FileAccess.Read);

    package.InitializePackage();

    Dictionary<string, Dictionary<string, MapInfo>> mapInfos;
    if (imagesDir != null)
    {
        mapInfos = imagesDir.EnumerateDirectories()
            .ToDictionary(
                x => x.Name,
                x => x.EnumerateDirectories()
                    .ToDictionary(
                        y => y.Name,
                        y => MapInfoFromConverted(y).Result
                    )
            );
    }
    else
    {
        mapInfos = new();
    }

    foreach (UObject obj in package.Objects)
    {
        if (obj.Class != null && obj.Class.Name == "RUFloorMapInfo")
        {
            obj.Load<UObjectStream>();

            string path = obj.GetPath();
            Console.WriteLine(path);

            string[] pathParts = path.Split('.');
            string packageName = pathParts[0];
            string zoneName = pathParts[1];

            MapInfo info;
            if (imagesDir != null)
            {
                if (!mapInfos.TryGetValue(packageName, out var zones) || !zones.TryGetValue(zoneName, out info!))
                    continue;
            }
            else
            {
                info = new MapInfo();
                mapInfos.TryGetOrAdd(packageName, _ => new()).Add(zoneName, info);
            }

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

            // This map is actually misaligned ingame.
            if (zoneName == "LM_Platform")
            {
                float inverseScale = info.UnitsPerPixel * (1 << info.MaxZoom);
                float correction = inverseScale * -2;

                info.MinX += correction;
                info.MinY += correction;
                info.MaxX += correction;
                info.MaxY += correction;
            }
        }
    }

    using Stream fs = outFile.Create();
    JsonSerializer.Serialize(fs, mapInfos);
}

static async Task<MapInfo> MapInfoFromConverted(DirectoryInfo dir)
{
    MapInfo info = new()
    {
        MaxZoom = dir.EnumerateDirectories().Max(x => int.TryParse(x.Name, out int nameNum) ? nameNum : -1)
    };

    string zeroImage = Path.Combine(dir.FullName, "0", "0", "0.png");
    if (File.Exists(zeroImage))
    {
        ImageInfo imageInfo = await Image.IdentifyAsync(zeroImage);
        info.TileWidth = imageInfo.Width;
        info.TileHeight = imageInfo.Height;
    }

    return info;
}

static async Task ExtractGeoJson(FileInfo instanceDb, FileInfo mapInfoFile, DirectoryInfo output)
{
    Dictionary<string, Dictionary<string, MapInfo>> mapInfos;
    using (Stream fs = mapInfoFile.OpenRead())
    {
        mapInfos = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, MapInfo>>>(fs)!;
    }

    Dictionary<int, List<((string packageName, string zoneName, MapInfo info) info, GeoJsonFeatureCollection geoJson)>> zonesByWorldId = mapInfos
        .SelectMany(p => p.Value.Select(z => (p.Key, z.Key, info: z.Value)))
        .GroupBy(x => x.info.WorldId)
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
                                Instance.dataJSON ->> "npcType" AS npcType,
                                Instance.dataJSON ->> "defb" AS defb,
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
        string? instanceNpcType = reader.GetNullableString(3);
        string? instanceDefb = reader.GetNullableString(4);
        int worldId = reader.GetInt32(5);

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

        FeatureType type = FeatureType.Other;

        if (instanceNpcType == "Vendor")
            type = FeatureType.Vendor;

        if (instanceDefb == "Portal")
            type = FeatureType.Portal;

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
                ["name"] = instanceEditorName,
                ["type"] = type.ToString()
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
                x >= i.info.info.MinX && x <= i.info.info.MaxX &&
                y >= i.info.info.MinY && y <= i.info.info.MaxY
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

    foreach (var zone in zonesByWorldId.Values.SelectMany(x => x))
    {
        string outName = $"{zone.info.packageName}.{zone.info.zoneName}.geojson";
        Console.WriteLine(outName);

        string outPath = Path.Join(output.FullName, outName);

        using Stream fs = File.Create(outPath);
        await JsonSerializer.SerializeAsync(fs, zone.geoJson, jsonOptions);
    }
}