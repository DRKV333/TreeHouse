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
using Elastic.Clients.Elasticsearch.IndexManagement;
using Microsoft.Data.Sqlite;
using TreeHouse.Common;
using TreeHouse.Common.CommandLine;
using TreeHouse.ImageFeatures;
using TreeHouse.QuestIndexer;

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
    }.WithHandler(SearchImages)
}
.WithGlobalOption(new Option<string>(["-e", "--elastic-url"]).Required())
.InvokeAsync(args);

static async Task DeleteIndex<T>(string elasticUrl)
{
    ElasticsearchClient client = CreateClient(elasticUrl);
    Console.WriteLine(await client.Indices.DeleteAsync<T>());
}

static ElasticsearchClient CreateClient(string elasticUrl)
{
    return new ElasticsearchClient(new ElasticsearchClientSettings(new Uri(elasticUrl))
        .DisableDirectStreaming()
        .DefaultMappingFor<Quest>(x => x.IndexName("ol-quest").IdProperty(x => x.ElasticId))
        .DefaultMappingFor<Dialog>(x => x.IndexName("ol-dialog").IdProperty(x => x.ElasticId))
        .DefaultMappingFor<Image>(x => x.IndexName("ol-image").IdProperty(x => x.ElasticId))
    );
}

static async Task IndexQuests(string elasticUrl, FileInfo source)
{
    ElasticsearchClient client = CreateClient(elasticUrl);

    CreateIndexResponse response = await client.Indices.CreateAsync<Quest>(i => i
        .Settings(s => s.SingleNode())
        .Mappings(m => m
            .Properties(p => p
                .IdKeywordWithNumber(x => x.Id)
                .TextEnglishWithKeyword(x => x.Name)
                .TextEnglishWithKeyword(x => x.Desc)
                .TextEnglishWithKeyword(x => x.Offer)
                .TextEnglishWithKeyword(x => x.Accept)
                .TextEnglishWithKeyword(x => x.Complete)
                .TextEnglishWithKeyword(x => x.Condition)
            )
        )
    );

    if (!response.IsSuccess())
    {
        Console.WriteLine("Could not create index ol-quest.");
        Console.WriteLine(response);
        return;
    }

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

    BulkResponse indexRespose = await client.IndexManyAsync(quests);
    if (!indexRespose.IsSuccess())
    {
        Console.WriteLine("Failed to index quests.");
        Console.WriteLine(indexRespose);
        return;
    }

    await client.Indices.RefreshAsync<Quest>();
    await client.Indices.ForcemergeAsync<Quest>();
}

static async Task IndexDialogs(string elasticUrl, FileInfo source)
{
    ElasticsearchClient client = CreateClient(elasticUrl);

    CreateIndexResponse response = await client.Indices.CreateAsync<Dialog>(i => i
        .Settings(s => s.SingleNode())
        .Mappings(m => m
            .Properties(p => p
                .IdKeywordWithNumber(x => x.Id)
                .TextEnglish(x => x.Text)
                .IntegerNumber(x => x.Ver)
            )
        )
    );

    if (!response.IsSuccess())
    {
        Console.WriteLine("Could not create index ol-dialog.");
        Console.WriteLine(response);
        return;
    }

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

    BulkResponse indexRespose = await client.IndexManyAsync(dialogs);
    if (!indexRespose.IsSuccess())
    {
        Console.WriteLine("Failed to index dialogs.");
        Console.WriteLine(indexRespose);
        return;
    }

    await client.Indices.RefreshAsync<Dialog>();
    await client.Indices.ForcemergeAsync<Dialog>();
}

static async Task IndexImages(string elasticUrl, DirectoryInfo source)
{
    ElasticsearchClient client = CreateClient(elasticUrl);

    CreateIndexResponse response = await client.Indices.CreateAsync<Image>(i => i
        .Settings(s => s.SingleNode())
        .Mappings(m => m
            .Properties(p => p
                .Keyword(x => x.FileName)
                .DenseVector(x => x.Features, v => v
                    .ElementType("float")
                    .Dims(ImageFeatureExtractor.FeaturesDim)
                    .Similarity("l2_norm")
                )
            )
        )
    );

    if (!response.IsSuccess())
    {
        Console.WriteLine("Could not create index ol-image.");
        Console.WriteLine(response);
        return;
    }

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

        BulkResponse indexRespose = await client.IndexManyAsync(
            fileBatch.Zip(featuresBatch).Select(x => new Image{
                FileName = x.First.FullName,
                Features = x.Second
            })
        );

        if (!indexRespose.IsSuccess())
        {
            Console.WriteLine("Failed to index dialogs.");
            Console.WriteLine(indexRespose);
            return;
        }
    }

    await client.Indices.RefreshAsync<Image>();
    await client.Indices.ForcemergeAsync<Image>();

    Console.WriteLine("done");
}

static async Task SearchImages(string elasticUrl, int size, FileInfo image)
{
    using ImageFeatureExtractor extractor = new(1);
    using Stream fileStream = image.OpenRead();
    await extractor.SetInputImage(fileStream, 0);
    float[] features = (await extractor.GetFeatures())[0];

    ElasticsearchClient client = CreateClient(elasticUrl);

    SearchResponse<Image> response = await client.SearchAsync<Image>(s => s
        .Knn(k => k
            .k(size)
            .Field(x => x.Features)
            .QueryVector(features)
        )
        .Size(size)
        .Source(new SourceConfig(false))
        .DocvalueFields(f => f
            .Field(x => x.FileName)
        )
    );

    foreach (Hit<Image> hit in response.Hits)
    {
        Console.WriteLine($"{hit.Score} {((JsonElement)hit.Fields!.Values.First())[0]}");
    }
}