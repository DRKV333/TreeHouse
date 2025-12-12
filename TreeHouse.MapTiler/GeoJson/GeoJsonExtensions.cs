using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using TreeHouse.Common;

namespace TreeHouse.MapTiler.GeoJson;

public static class GeoJsonExtensions
{
    private static readonly JsonDerivedType[] derivedTypes = [
        new JsonDerivedType(typeof(PointGeometry), "Point"),
        new JsonDerivedType(typeof(LineStringGeometry), "LineString"),
        new JsonDerivedType(typeof(PolygonGeometry), "Polygon"),
        new JsonDerivedType(typeof(MultiPointGeometry), "MultiPoint"),
        new JsonDerivedType(typeof(MultiLineStringGeometry), "MultiLineString"),
        new JsonDerivedType(typeof(MultiPolygonGeometry), "MultiPolygon"),
        new JsonDerivedType(typeof(GeometryCollection), "GeometryCollection"),
        new JsonDerivedType(typeof(GeoJsonFeature), "Feature"),
        new JsonDerivedType(typeof(GeoJsonFeatureCollection), "FeatureCollection")
    ];

    public static IJsonTypeInfoResolver WithGeoJsonTypesModifier(this IJsonTypeInfoResolver resolver) =>
        resolver.WithAddedModifier(info =>
        {
            if (info.Type.GetCustomAttribute<GeoJsonTypeAttribute>() != null)
            {
                info.PolymorphismOptions = new JsonPolymorphismOptions()
                {
                    IgnoreUnrecognizedTypeDiscriminators = false,
                    TypeDiscriminatorPropertyName = "type",
                    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization
                };

                info.PolymorphismOptions.DerivedTypes.AddRange(derivedTypes.Where(x => x.DerivedType.IsAssignableTo(info.Type)));
            }
        });
}
