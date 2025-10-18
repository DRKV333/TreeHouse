using System.IO;
using System.Text;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Transport.Products.Elasticsearch;

namespace TreeHouse.QuestModels.Elasticsearch;

public static class ElasticsearchExtensions
{
    public static TResponse CheckSuccess<TResponse>(this TResponse response, string? operation = null)
        where TResponse : ElasticsearchResponse
    {
        if (!response.IsSuccess())
        {
            StringBuilder builder = new();
            builder.Append("Elasticsearch API call failed");
            if (operation != null)
            {
                builder.Append(" while ");
                builder.Append(operation);
            }
            builder.AppendLine(":");
            builder.AppendLine(response.ToString());

            throw new IOException(builder.ToString());
        }

        return response;
    }

    public static async Task<TResponse> CheckSuccess<TResponse>(this Task<TResponse> responseTask, string? operation = null)
        where TResponse : ElasticsearchResponse
    {
        return CheckSuccess(await responseTask, operation);
    }

    public static ElasticsearchClientSettings ConfigureQuestModels(this ElasticsearchClientSettings settings) => settings
        .DisableDirectStreaming()
        .DefaultMappingFor<Quest>(x => x.IndexName(Quest.IndexName).IdProperty(x => x.ElasticId))
        .DefaultMappingFor<Dialog>(x => x.IndexName(Dialog.IndexName).IdProperty(x => x.ElasticId))
        .DefaultMappingFor<Image>(x => x.IndexName(Image.IndexName).IdProperty(x => x.ElasticId));

    public static CreateIndexRequestDescriptor<Quest> CreateQuest(this CreateIndexRequestDescriptor<Quest> desc) => desc
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
        );

    public static CreateIndexRequestDescriptor<Dialog> CreateDialog(this CreateIndexRequestDescriptor<Dialog> desc) => desc
        .Settings(s => s.SingleNode())
        .Mappings(m => m
            .Properties(p => p
                .IdKeywordWithNumber(x => x.Id)
                .TextEnglish(x => x.Text)
                .IntegerNumber(x => x.Ver)
            )
        );

    public static CreateIndexRequestDescriptor<Image> CreateImage(this CreateIndexRequestDescriptor<Image> desc, int featureDims) => desc
        .Settings(s => s.SingleNode())
        .Mappings(m => m
            .Properties(p => p
                .Keyword(x => x.FileName)
                .DenseVector(x => x.Features, v => v
                    .ElementType(DenseVectorElementType.Float)
                    .Dims(featureDims)
                    .Similarity(DenseVectorSimilarity.L2Norm)
                )
            )
        );
}
