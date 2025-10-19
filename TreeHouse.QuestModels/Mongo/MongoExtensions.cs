using MongoDB.Driver;

namespace TreeHouse.QuestModels.Mongo;

public static class MongoExtensions
{
    public static IMongoCollection<QuestData> GetQuestDataCollection(this IMongoClient client) =>
        client.GetDatabase(QuestData.DatabaseName).GetCollection<QuestData>(QuestData.CollectionName);
}
