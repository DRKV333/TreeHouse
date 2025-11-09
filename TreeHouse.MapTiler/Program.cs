using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using TreeHouse.Common.CommandLine;

await new RootCommand()
{
    new Command("convert")
    {
        new Option<DirectoryInfo>(["-s", "--source"]).ExistingOnly().Required(),
        new Option<DirectoryInfo>(["-t", "--target"]).Required()
    }.WithHandler(Convert)
}
.InvokeAsync(args);

const int SlippyTileSize = 256;

static async Task Convert(DirectoryInfo source, DirectoryInfo target)
{
    Image stiched = await LoadStiched(source);
    await WriteSlippyTiles(stiched, target);
}

static async Task WriteSlippyTiles(Image image, DirectoryInfo target)
{
    int zoomLevel = 0;

    Console.WriteLine("Creating slippy tiles...");

    while (true)
    {
        int tileCount = 1 << zoomLevel;
        int fullSize = tileCount * SlippyTileSize;
        float scaleFactor = Math.Min((float)fullSize / image.Width, (float)fullSize / image.Height);

        Console.WriteLine($"Level: {zoomLevel}, Scale: {scaleFactor}");

        string zoomDir = Path.Join(target.FullName, zoomLevel.ToString());

        IResampler resampler = scaleFactor >= 1 ? new NearestNeighborResampler() : new BicubicResampler();

        using Image scaled = image.Clone(x => x.Resize(fullSize, fullSize, resampler));

        for (int x = 0; x < tileCount; x++)
        {
            string xDir = Path.Join(zoomDir, x.ToString());
            Directory.CreateDirectory(xDir);

            for (int y = 0; y < tileCount; y++)
            {
                using Image tile = scaled.Clone(c => c.Crop(new Rectangle(x * SlippyTileSize, y * SlippyTileSize, SlippyTileSize, SlippyTileSize)));
                await tile.SaveAsPngAsync(Path.Join(xDir, $"{y}.png"));
            }
        }

        if (scaleFactor >= 1)
            break;

        zoomLevel++;
    }
}

static async Task<Image> LoadStiched(DirectoryInfo source)
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

    return stiched!;
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