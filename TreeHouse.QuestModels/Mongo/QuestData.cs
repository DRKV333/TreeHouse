using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TreeHouse.QuestModels.Mongo;

public class QuestData
{
    [BsonId]
    public ObjectId MongoId { get; set; }

    public long Id { get; set; }

    public int Order { get; set; }

    public string Name { get; set; } = "";

    public int? Level { get; set; }

    public string Category { get; set; } = "";

    public int? Exp { get; set; }

    public int? Bits { get; set; }

    public List<string> Rewards { get; set; } = null!;

    public string Comments { get; set; } = "";
}
