using Elastic.Clients.Elasticsearch;

namespace TreeHouse.QuestModels.Elasticsearch;

public class Dialog
{
    public const string IndexName = "ol-dialog";

    public Id ElasticId { get; set; } = null!;

    public long Id { get; set; }

    public string Text { get; set; } = "";

    public int Ver { get; set; }
}
