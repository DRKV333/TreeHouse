using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace TreeHouse.OtherParams.JsonConverter;

public class ContentDbJsonConverter : DbJsonConverterBase
{
    private readonly DefaultValueProvider<int>? defaultValueProvider;

    public ContentDbJsonConverter(ParamSetJsonConverter converter, DefaultValueProvider<int>? defaultValueProvider, bool validateClasses = true) : base(
        converter: converter,
        keyColumn: "guid",
        dataColumn: "data",
        extraColumns: validateClasses || defaultValueProvider != null ? [ "ixClass" ] : [],
        ordClassIdColumn: validateClasses ? OrdExtraOffset + 0 : null
    )
    {
        this.defaultValueProvider = defaultValueProvider;
    }

    protected override JsonObject GetDefaultJson(string table, SqliteDataReader reader) =>
        defaultValueProvider != null ? defaultValueProvider.GetDefault(reader.GetInt32(OrdExtraOffset + 0)) : new JsonObject();

    public void ReadParamSets(SqliteConnection connection, Model.Table table) =>
        ReadParamSetsForTable(connection, table.Name);
}
