using Microsoft.Data.Sqlite;

namespace TreeHouse.Common.SQLite;

public static class SqliteExtensions
{
    public static void AddColumnIfNotExists(this SqliteConnection connection, string table, string type, string column)
    {
        bool haveColumn = false;
        
        using (SqliteCommand infoCommand = connection.CreateCommand())
        {
            infoCommand.CommandText = $"PRAGMA table_info({table})";

            using SqliteDataReader reader = infoCommand.ExecuteReader();
            while (reader.Read())
            {
                if (reader.GetString(1) == column)
                {
                    haveColumn = true;
                    break;
                }
            }
        }

        if (!haveColumn)
        {
            using SqliteCommand alterCommand = connection.CreateCommand();
            alterCommand.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {type}";
            alterCommand.ExecuteNonQuery();
        }
    }
}
