using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using TreeHouse.QuestModels.Mongo;

namespace TreeHouse.QuestEditor.Services;

internal class MongoDbService(IOptions<DbConfig> config)
{
    private readonly IMongoCollection<QuestData> collection =
        new MongoClient(config.Value.MongoUrl)
        .GetDatabase("ol-questdata")
        .GetCollection<QuestData>("quests");

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
}
