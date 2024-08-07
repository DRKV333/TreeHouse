using Microsoft.Data.Sqlite;

namespace TreeHouse.OtherParams;

public static class SqliteUtils
{
    public static string ConnectionString(string path, bool write = false) => new SqliteConnectionStringBuilder()
    {
        DataSource = path,
        Mode = write ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadOnly,
    }.ConnectionString;

    public static SqliteConnection Open(string path, bool write = false)
    {
        SqliteConnection connection = new(ConnectionString(path, write));
        connection.Open();
        return connection;
    }

    public static void AddColumnIfNotExists(SqliteConnection connection, string table, string type, string column)
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
