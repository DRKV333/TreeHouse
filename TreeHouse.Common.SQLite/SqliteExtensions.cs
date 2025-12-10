using System.Collections.Generic;
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

    public static IReadOnlyCollection<string> GetTables(this SqliteConnection connection, string db = "")
    {
        if (db != "")
            db += ".";

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT name FROM {db}sqlite_master WHERE type = 'table'";

        using SqliteDataReader reader = command.ExecuteReader();

        List<string> result = new();
        while(reader.Read())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    public static void Attach(this SqliteConnection connection, string path, string name)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"ATTACH DATABASE @path AS {name}"; // TODO: Use URI for path, so that this can be readonly.
        command.Parameters.AddWithValue("@path", path);
        command.ExecuteNonQuery();
    }
}
