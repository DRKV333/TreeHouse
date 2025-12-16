namespace TreeHouse.MapTiler;

public class MapInfo
{
    public int WorldId { get; set; }

    public float UnitsPerPixel { get; set; }

    public float MinX { get; set; }

    public float MinY { get; set; }

    public float MaxX { get; set; }

    public float MaxY { get; set; }

    public int TileWidth { get; set; }

    public int TileHeight { get; set; }

    public int MaxZoom { get; set; }
}
