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
}
