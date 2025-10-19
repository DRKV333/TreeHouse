using System;
using System.Linq;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Options;
using TreeHouse.QuestModels.Elasticsearch;

namespace TreeHouse.QuestEditor.Services;

internal class ElasticsearchService(IOptions<DbConfig> config)
{
    private readonly ElasticsearchClient client =
        new(
            new ElasticsearchClientSettings(new Uri(config.Value.ElasticUrl)).ConfigureQuestModels()
        );

    public async Task<Quest> GetQuestById(long id)
    {
        SearchResponse<Quest> response = await client.SearchAsync<Quest>(s => s
            .Indices(Indices.Index<Quest>())
            .Query(q => q
                .Match(m => m
                    .Field(x => x.Id)
                    .Query(id)
                )
            )
            .Size(1)
            .Source(true)
        ).CheckSuccess();

        if (response.Hits.Count == 0)
            throw new InvalidOperationException($"Quest with id {id} not found");

        return response.Hits.First().Source!;
    }
}
