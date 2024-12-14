using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace TreeHouse.OtherParams.JsonConverter;

public class InstanceDbJsonConverter : DbJsonConverterBase
{
    private readonly DefaultValueProvider<Guid>? defaultValueProvider;

    public InstanceDbJsonConverter(ParamSetJsonConverter converter, DefaultValueProvider<Guid>? defaultValueProvider, bool validateClasses = true) : base(
        converter: converter,
        keyColumn: "uxInstanceGuid",
        dataColumn: "data",
        ExtraColumns(validateClasses, defaultValueProvider != null),
        validateClasses ? OrdExtraOffset + 0 : null
    )
    {
        this.defaultValueProvider = defaultValueProvider;
    }

    private static IEnumerable<string> ExtraColumns(bool validateClasses, bool provideDefaults)
    {
        if (validateClasses)
            yield return "ixClass";
        if (provideDefaults)
            yield return "uxContentGuid";
    }

    protected override JsonObject GetDefaultJson(string table, SqliteDataReader reader) =>
        defaultValueProvider != null
            ? defaultValueProvider.GetDefault(
                Guid.Parse(reader.GetString(
                    ordClassIdColumn == null ? OrdExtraOffset + 0 : OrdExtraOffset + 1
                ))
            )
            : new JsonObject();

    public void ReadParamSets(SqliteConnection connection) => ReadParamSetsForTable(connection, "Instance");
}
