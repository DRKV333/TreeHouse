using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace TreeHouse.OtherParams.GeoJson;

[GeoJsonType]
public abstract class GeoJsonObject
{
    [JsonPropertyName("bbox")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double[]? BoudingBox { get; set; }

    [JsonExtensionData]
    public JsonObject? ForeignMembers { get; set; }
}

[GeoJsonType]
public class GeoJsonFeature : GeoJsonObject
{
    [JsonPropertyName("geometry")]
    public GeoJsonGeometry? Geometry { get; set; }

    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonValue? Id { get; set; }

    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonObject? Properties { get; set; }
}

[GeoJsonType]
public class GeoJsonFeatureCollection : GeoJsonObject
{
    [JsonPropertyName("features")]
    public required IList<GeoJsonFeature> Features { get; set; } = null!;
}