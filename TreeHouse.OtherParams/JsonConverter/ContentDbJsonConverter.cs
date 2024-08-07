using Microsoft.Data.Sqlite;

namespace TreeHouse.OtherParams.JsonConverter;

public class ContentDbJsonConverter : DbJsonConverterBase
{
    public ContentDbJsonConverter(ParamSetJsonConverter converter, bool validateClasses = true)
        : base(converter, "guid", "data", validateClasses ? "ixClass" : null)
    {
    }

    public void ReadParamSets(SqliteConnection connection, Model.Table table) =>
        ReadParamSetsForTable(connection, table.Name);
}
