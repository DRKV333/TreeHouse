using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using TreeHouse.QuestModels.Mongo;

namespace TreeHouse.QuestEditor.Services;

internal sealed class MongoDbService : IDisposable
{
    private readonly MongoClient client;

    private readonly IMongoCollection<QuestData> collection;

    public MongoDbService(IOptions<DbConfig> config)
    {
        client = new MongoClient(config.Value.MongoUrl);
        collection = client.GetQuestDataCollection();
    }

    public IAsyncEnumerable<QuestNavItem> GetAllNavItems() => collection
        .Find(_ => true)
        .Project(Builders<QuestData>.Projection.Expression<QuestNavItem>(
            x => new QuestNavItem()
            {
                Name = x.Name,
                Category = x.Category,
                MongoId = x.MongoId.ToString()
            }
        ))
        .SortBy(x => x.Order)
        .ToAsyncEnumerable();

    public Task<QuestData> GetByMongoId(string id) =>
        collection.Find(x => x.MongoId == ObjectId.Parse(id)).SingleAsync();

    public Task Update(QuestData data) => collection.ReplaceOneAsync(x => x.MongoId == data.MongoId, data);

    public void Dispose()
    {
        client.Dispose();
    }
}
