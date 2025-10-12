using System.Collections.Generic;
using Elastic.Clients.Elasticsearch;

namespace TreeHouse.QuestModels.Elasticsearch;

public class Quest
{
    public const string IndexName = "ol-quest";

    public Id ElasticId { get; set; } = null!;

    public long Id { get; set; }

    public string Name { get; set; } = "";

    public string Desc { get; set; } = "";

    public string Offer { get; set; } = "";

    public string Accept { get; set; } = "";

    public string Complete { get; set; } = "";

    public IList<string> Condition { get; set; } = null!;
}
