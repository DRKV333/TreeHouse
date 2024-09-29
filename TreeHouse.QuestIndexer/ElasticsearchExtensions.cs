using System;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Transport.Products.Elasticsearch;

namespace TreeHouse.QuestIndexer;

internal static class ElasticsearchExtensions
{
    public static PropertiesDescriptor<T> TextEnglishWithKeyword<T>(this PropertiesDescriptor<T> desc, Expression<Func<T, object>> propertyName) => desc
        .Text(propertyName, p => p
            .Fielddata()
            .Analyzer("english")
            .Fields(f => f
                .Keyword("keyword")
            )
        );

    public static PropertiesDescriptor<T> TextEnglish<T>(this PropertiesDescriptor<T> desc, Expression<Func<T, object>> propertyName) => desc
        .Text(propertyName, p => p
            .Fielddata()
            .Analyzer("english")
        );

    public static PropertiesDescriptor<T> IdKeywordWithNumber<T>(this PropertiesDescriptor<T> desc, Expression<Func<T, object>> propertyName) => desc
        .Keyword(propertyName, p => p
            .Fields(f => f
                .IntegerNumber("number")
            )
        );

    public static IndexSettingsDescriptor<T> SingleNode<T>(this IndexSettingsDescriptor<T> desc) => desc
        .NumberOfShards(1)
        .NumberOfReplicas(0);

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

            throw new InvalidOperationException(builder.ToString());
        }

        return response;
    }

    public static async Task<TResponse> CheckSuccess<TResponse>(this Task<TResponse> responseTask, string? operation = null)
        where TResponse : ElasticsearchResponse
    {
        return CheckSuccess(await responseTask, operation);
    }
}
