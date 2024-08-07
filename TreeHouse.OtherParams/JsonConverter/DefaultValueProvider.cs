using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace TreeHouse.OtherParams.JsonConverter;

public class DefaultValueProvider<TKey>
{
    private readonly IReadOnlyDictionary<TKey, JsonObject> defaultValues;

    public DefaultValueProvider(IReadOnlyDictionary<TKey, JsonObject> defaultValues)
    {
        this.defaultValues = defaultValues;
    }

    public JsonObject GetDefault(TKey key)
    {
        if (defaultValues.TryGetValue(key, out JsonObject? existing))
            return (JsonObject)existing.DeepClone();
        else
            return new JsonObject();
    }
}
