using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Microsoft.Data.Sqlite;
using MongoDB.Bson;
using MongoDB.Driver;
using TreeHouse.Common;
using TreeHouse.Common.CommandLine;
using TreeHouse.ImageFeatures;
using TreeHouse.QuestModels.Elasticsearch;
using TreeHouse.QuestModels.Mongo;

await new RootCommand()
{
    new Command("index-quests")
    {
        new Option<FileInfo>(["-s", "--source"]).ExistingOnly().Required()
    }.WithHandler(IndexQuests),
    new Command("delete-quests").WithHandler(DeleteIndex<Quest>),

    new Command("index-dialogs")
    {
        new Option<FileInfo>(["-s", "--source"]).ExistingOnly().Required()
    }.WithHandler(IndexDialogs),
    new Command("delete-dialogs").WithHandler(DeleteIndex<Dialog>),

    new Command("index-images")
    {
        new Option<DirectoryInfo>(["-s", "--source"]).ExistingOnly().Required()
    }.WithHandler(IndexImages),
    new Command("delete-images").WithHandler(DeleteIndex<Image>),
    new Command("search-images")
    {
        new Option<int>("--size").Default(5),
        new Argument<FileInfo>("image").ExistingOnly().Arity(ArgumentArity.ExactlyOne)
    }.WithHandler(SearchImages),

    new Command("import-data")
    {
        new Option<string>(["-m", "--mongo-url"]).Required(),
        new Option<FileInfo>(["-s", "--source"]).ExistingOnly().Required()
    }.WithHandler(ImportData),
    new Command("export-data")
    {
        new Option<string>(["-m", "--mongo-url"]).Required(),
        new Option<FileInfo>(["-t", "--target"]).Required()
    }.WithHandler(ExportData)
}
.WithGlobalOption(new Option<string>(["-e", "--elastic-url"]).Required())
.InvokeAsync(args);

static async Task DeleteIndex<T>(string elasticUrl)
{
    ElasticsearchClient client = CreateElasticClient(elasticUrl);
    Console.WriteLine(await client.Indices.DeleteAsync<T>());
}

static ElasticsearchClient CreateElasticClient(string elasticUrl) =>
    new ElasticsearchClient(new ElasticsearchClientSettings(new Uri(elasticUrl)).ConfigureQuestModels());

static async Task IndexQuests(string elasticUrl, FileInfo source)
{
    ElasticsearchClient client = CreateElasticClient(elasticUrl);

    await client.Indices.CreateAsync<Quest>(i => i.CreateQuest())
        .CheckSuccess($"creating index {Quest.IndexName}");

    using SqliteConnection connection = new(new SqliteConnectionStringBuilder()
    {
        DataSource = source.FullName,
        Mode = SqliteOpenMode.ReadOnly
    }.ConnectionString);
    connection.Open();

    using SqliteCommand questCommand = connection.CreateCommand();
    questCommand.CommandText = "SELECT Id, DisplayName, Desc, OfferDialog, AcceptedDialog, CompleteDialog FROM Quest";

    using SqliteCommand conditionsCommand = connection.CreateCommand();
    conditionsCommand.CommandText = "SELECT ConditionDesc FROM Condition WHERE QuestID = $questId ORDER BY ID";
    conditionsCommand.Parameters.Add("$questId", SqliteType.Integer);

    List<Quest> quests = new();

    using SqliteDataReader questReader = questCommand.ExecuteReader();
    while (questReader.Read())
    {
        Quest quest = new()
        {
            Id = questReader.GetInt64(0),
            Name = questReader.GetString(1),
            Desc = questReader.GetString(2),
            Offer = questReader.GetString(3),
            Accept = questReader.GetString(4),
            Complete = questReader.GetString(5),
            Condition = new List<string>()
        };

        conditionsCommand.Parameters["$questId"].Value = quest.Id;

        using SqliteDataReader conditionReader = conditionsCommand.ExecuteReader();
        while (conditionReader.Read())
        {
            quest.Condition.Add(conditionReader.GetString(0));
        }

        quests.Add(quest);
    }

    await client.IndexManyAsync(quests).CheckSuccess("indexing quests");

    await client.Indices.RefreshAsync<Quest>().CheckSuccess();
    await client.Indices.ForcemergeAsync<Quest>().CheckSuccess();
}

static async Task IndexDialogs(string elasticUrl, FileInfo source)
{
    ElasticsearchClient client = CreateElasticClient(elasticUrl);

    await client.Indices.CreateAsync<Dialog>(i => i.CreateDialog())
        .CheckSuccess($"creating index {Dialog.IndexName}");

    using SqliteConnection connection = new(new SqliteConnectionStringBuilder()
    {
        DataSource = source.FullName,
        Mode = SqliteOpenMode.ReadOnly
    }.ConnectionString);
    connection.Open();

    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "SELECT ID, Text, Version FROM Dialog";

    List<Dialog> dialogs = new();

    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read())
    {
        dialogs.Add(new Dialog()
        {
            Id = reader.GetInt64(0),
            Text = reader.GetString(1),
            Ver = reader.GetInt32(2)
        });
    }

    await client.IndexManyAsync(dialogs).CheckSuccess("indexing dialogs");

    await client.Indices.RefreshAsync<Dialog>().CheckSuccess();
    await client.Indices.ForcemergeAsync<Dialog>().CheckSuccess();
}

static async Task IndexImages(string elasticUrl, DirectoryInfo source)
{
    ElasticsearchClient client = CreateElasticClient(elasticUrl);

    await client.Indices.CreateAsync<Image>(i => i.CreateImage(ImageFeatureExtractor.FeaturesDim))
        .CheckSuccess($"creating index {Image.IndexName}");

    int batchSize = 100;

    using ImageFeatureExtractor extractor = new(batchSize);

    List<FileInfo[]> fileBatches = source
        .EnumerateFiles("*.png", new EnumerationOptions() { RecurseSubdirectories = true })
        .Chunk(batchSize)
        .ToList();

    foreach ((FileInfo[] fileBatch, int batchIdx) in fileBatches.WithIndex())
    {
        Stopwatch stopwatch = new();
        stopwatch.Start();

        foreach ((FileInfo file, int fileIdx) in fileBatch.WithIndex())
        {
            using Stream fileStream = file.OpenRead();
            await extractor.SetInputImage(fileStream, fileIdx);
        }

        IReadOnlyList<float[]> featuresBatch = await extractor.GetFeatures();

        stopwatch.Stop();
        Console.WriteLine($"{batchIdx + 1}/{fileBatches.Count} {stopwatch.Elapsed}");

        await client.IndexManyAsync(
            fileBatch.Zip(featuresBatch).Select(x => new Image{
                FileName = x.First.FullName,
                Features = x.Second
            })
        ).CheckSuccess("indexing images");
    }

    await client.Indices.RefreshAsync<Image>().CheckSuccess();
    await client.Indices.ForcemergeAsync<Image>().CheckSuccess();

    Console.WriteLine("done");
}

static async Task SearchImages(string elasticUrl, int size, FileInfo image)
{
    using ImageFeatureExtractor extractor = new(1);
    using Stream fileStream = image.OpenRead();
    await extractor.SetInputImage(fileStream, 0);
    float[] features = (await extractor.GetFeatures())[0];

    ElasticsearchClient client = CreateElasticClient(elasticUrl);

    SearchResponse<Image> response = await client.SearchAsync<Image>(s => s
        .Indices(Indices.Index<Image>())
        .Knn(k => k
            .K(size)
            .Field(x => x.Features)
            .QueryVector(features)
        )
        .Size(size)
        .Source(new SourceConfig(false))
        .DocvalueFields(f => f
            .Field(x => x.FileName)
        )
    ).CheckSuccess();

    foreach (Hit<Image> hit in response.Hits)
    {
        Console.WriteLine($"{hit.Score} {((JsonElement)hit.Fields!.Values.First())[0]}");
    }
}

static async Task ImportData(string elasticUrl, string mongoUrl, FileInfo source)
{
    List<QuestData> questDatas;
    using (Stream fs = source.OpenRead())
    {
        questDatas = (await JsonSerializer.DeserializeAsync<List<QuestData>>(fs))!;
    }

    ElasticsearchClient elasticClient = CreateElasticClient(elasticUrl);

    int order = 0;
    foreach (QuestData questData in questDatas)
    {
        questData.MongoId = ObjectId.GenerateNewId();

        questData.Order = order++;

        SearchResponse<Quest> search = await elasticClient.SearchAsync<Quest>(s => s
            .Indices(Indices.Index<Quest>())
            .Query(q => q
                .Match(m => m
                    .Field(x => x.Id)
                    .Query(questData.Id)
                )
            )
            .Size(1)
            .Source(false)
            .DocvalueFields(
                f => f.Field(x => x.Name.Suffix(Suffix.Keyword))
            )
        ).CheckSuccess();

        if (search.Hits.Count == 0)
            Console.WriteLine($"Did not find quest with id {questData.Id} in elastic!");
        else
            questData.Name = search.Hits.First().GetFieldValues(elasticClient, x => (string)x.Name.Suffix(Suffix.Keyword)).Single();
    }

    using MongoClient mongoClient = new(mongoUrl);
    IMongoCollection<QuestData> collection = mongoClient.GetQuestDataCollection();

    await collection.InsertManyAsync(questDatas);

    Console.WriteLine($"Imported {questDatas.Count} quests.");

    await collection.Indexes.CreateOneAsync(
        new CreateIndexModel<QuestData>(
            Builders<QuestData>.IndexKeys.Ascending(x => x.Order)
        )
    );
}

static async Task ExportData(string elasticUrl, string mongoUrl, FileInfo target)
{
    using MongoClient client = new(mongoUrl);
    IMongoCollection<QuestData> collection = client.GetQuestDataCollection();

    List<QuestData> questDatas = await collection
        .Find(_ => true)
        .SortBy(x => x.Order)
        .ToListAsync();

    using Stream fs = target.Create();
    await JsonSerializer.SerializeAsync(fs, questDatas, new JsonSerializerOptions() { WriteIndented = true });

    Console.WriteLine($"Exported {questDatas.Count} quests.");
}