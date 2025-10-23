using System;
using System.Collections.Generic;
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

    public async Task<IReadOnlyList<ElasticAutoCompleteResult>> AutoCompleteQuestName(string query)
    {
        SearchResponse<Quest> response = await client.SearchAsync<Quest>(s => s
            .Indices(Indices.Index<Quest>())
            .Query(q => q
                .Match(m => m
                    .Field(x => x.Name)
                    .Query(query)
                    .Fuzziness(f => f.Value("AUTO"))
                )
            )
            .Size(10)
            .Source(false)
            .DocvalueFields(
                f => f.Field(x => x.Id),
                f => f.Field(x => x.Name.Suffix(Suffix.Keyword))
            )
        ).CheckSuccess();

        return response.Hits.Select(h => new ElasticAutoCompleteResult()
        {
            Id = h.GetFieldValues(client, x => x.Id).Single(),
            Content = h.GetFieldValues(client, x => (string)x.Name.Suffix(Suffix.Keyword)).Single()
        }).ToList();
    }
}
