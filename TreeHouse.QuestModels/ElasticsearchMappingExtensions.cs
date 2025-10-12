using System;
using System.Linq.Expressions;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

namespace TreeHouse.QuestModels;

internal static class ElasticsearchMappingExtensions
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
}
