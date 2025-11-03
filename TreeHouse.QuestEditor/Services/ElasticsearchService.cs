using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.Options;
using TreeHouse.QuestModels.Elasticsearch;

namespace TreeHouse.QuestEditor.Services;

internal class ElasticsearchService(IOptions<DbConfig> config)
{
    private readonly ElasticsearchClient client =
        new(
            new ElasticsearchClientSettings(new Uri(config.Value.ElasticUrl)).ConfigureQuestModels()
        );

    public Task<Quest> GetQuestById(long id) => GetById<Quest>(id, x => x.Id);

    public Task<Dialog> GetDialogById(long id) => GetById<Dialog>(id, x => x.Id);

    private async Task<T> GetById<T>(long id, Expression<Func<T, object?>> idExpression)
    {
        SearchResponse<T> response = await client.SearchAsync<T>(s => s
            .Indices(Indices.Index<T>())
            .Query(q => q
                .Match(m => m
                    .Field(idExpression)
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

    public async Task<IReadOnlyList<ElasticAutoCompleteResult>> AutoCompleteDialog(string query) // TODO
    {
        SearchResponse<Dialog> response = await client.SearchAsync<Dialog>(s => s
            .Indices(Indices.Index<Dialog>())
            .Query(q => q
                .MultiMatch(m => m
                    .Type(TextQueryType.BoolPrefix)
                    .Fields(
                        x => x.Text,
                        x => x.Text.Suffix(Suffix.TwoGram),
                        x => x.Text.Suffix(Suffix.ThreeGram)
                    )
                    .Query(query)
                )
            )
            .Size(10)
            .Source(c => c
                .Filter(f => f
                    .Includes(
                        x => x.Text,
                        x => x.Id
                    )
                )
            )
        ).CheckSuccess();

        return response.Documents.Select(x => new ElasticAutoCompleteResult()
        {
            Id = x.Id,
            Content = x.Text
        }).ToList();
    }
}
