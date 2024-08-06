using Elastic.Clients.Elasticsearch;

namespace TreeHouse.QuestIndexer;

internal class Dialog
{
    public Id ElasticId { get; set; } = null!;

    public long Id { get; set; }

    public string Text { get; set; } = "";

    public int Ver { get; set; }
}
