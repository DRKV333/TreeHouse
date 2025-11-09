using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TreeHouse.OtherParams.GeoJson;

[GeoJsonType]
public abstract class GeoJsonGeometry : GeoJsonObject;

[GeoJsonType]
public abstract class CoordinatesGeometry<T> : GeoJsonGeometry
{
    [JsonPropertyName("coordinates")]
    public required double[] Coordinates { get; set; } = null!;
}

[GeoJsonType]
public class PointGeometry : CoordinatesGeometry<double[]>;

[GeoJsonType]
public class LineStringGeometry : CoordinatesGeometry<IList<double[]>>;

[GeoJsonType]
public class PolygonGeometry : CoordinatesGeometry<IList<IList<double[]>>>;

[GeoJsonType]
public class MultiPointGeometry : CoordinatesGeometry<IList<double[]>>;

[GeoJsonType]
public class MultiLineStringGeometry : CoordinatesGeometry<IList<IList<double[]>>>;

[GeoJsonType]
public class MultiPolygonGeometry : CoordinatesGeometry<IList<IList<IList<double[]>>>>;

[GeoJsonType]
public class GeometryCollection : GeoJsonGeometry
{
    [JsonPropertyName("geometries")]
    public required IList<GeoJsonGeometry> Geometries { get; set; } = null!;
}