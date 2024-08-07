using Microsoft.Data.Sqlite;

namespace TreeHouse.OtherParams.JsonConverter;

public class InstanceDbJsonConverter : DbJsonConverterBase
{
    public InstanceDbJsonConverter(ParamSetJsonConverter converter, bool validateClasses = true)
        : base(converter, "uxInstanceGuid", "data", validateClasses ? "ixClass" : null)
    {
    }

    public void ReadParamSets(SqliteConnection connection) => ReadParamSetsForTable(connection, "Instance");
}
