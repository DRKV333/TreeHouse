using System;
using Elastic.Clients.Elasticsearch;

namespace TreeHouse.QuestModels.Elasticsearch;

public class Image
{
    public const string IndexName = "ol-image";

    public Id ElasticId { get; set; } = null!;

    public string FileName { get; set; } = "";

    public float[] Features { get; set; } = Array.Empty<float>();
}
