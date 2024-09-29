using System;
using Elastic.Clients.Elasticsearch;

namespace TreeHouse.QuestIndexer;

internal class Image
{
    public Id ElasticId { get; set; } = null!;

    public string FileName { get; set; } = "";

    public float[] Features { get; set; } = Array.Empty<float>();
}
