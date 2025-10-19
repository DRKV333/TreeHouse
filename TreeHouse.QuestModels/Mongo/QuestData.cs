using System.Collections.Generic;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TreeHouse.QuestModels.Mongo;

public class QuestData
{
    public const string DatabaseName = "ol-questdata";

    public const string CollectionName = "quests";

    [BsonId]
    [JsonIgnore]
    public ObjectId MongoId { get; set; }

    public long Id { get; set; }

    [JsonIgnore]
    public int Order { get; set; }

    [JsonIgnore]
    public string Name { get; set; } = "";

    public int? Level { get; set; }

    public string Category { get; set; } = "";

    public int? Exp { get; set; }

    public int? Bits { get; set; }

    public List<string> Rewards { get; set; } = null!;

    public List<DialogData> Dialogs { get; set; } = null!;

    public string Comments { get; set; } = "";
}
