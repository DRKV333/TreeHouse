using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using TreeHouse.Common.CommandLine;
using TreeHouse.LocalizationDiffer;
using UniDelta.Myers;

(string table, string[] columns)[] ColumnsToDiff =
[
    ( "Dialog", [ "Text" ]),
    ( "Condition", [ "ConditionDesc" ]),
    ( "Quest", [ "DisplayName", "Desc", "OfferDialog", "AcceptDialog", "CompleteDialog" ])
];

string TablePrefix = "# TABLE: ";
string ColumnPrefix = "## COLUMN: ";
string IDPrefix = "### ";

int ContextSize = 15;

await new RootCommand()
{
    new Command("diff")
    {
        new Argument<FileInfo>("original").ExistingOnly(),
        new Argument<FileInfo>("edited").ExistingOnly(),
        new Argument<FileInfo>("patchOutput")
    }.WithHandler(Diff)
}
.InvokeAsync(args);

async Task Diff(FileInfo original, FileInfo edited, FileInfo patchOut)
{
    using SqliteConnection connection = Open(original.FullName);
    Attach(connection, edited.FullName, "edited");

    IReadOnlyCollection<string> originalTables = GetTables(connection, "");
    IReadOnlyCollection<string> editedTables = GetTables(connection, "edited.");

    using TextWriter patchWriter = patchOut.CreateText();

    ShortestEditScriptDiffer<char> differ = new();

    foreach (var (table, columns) in ColumnsToDiff)
    {
        if (!originalTables.Contains(table))
            continue;

        Console.WriteLine($"Found table '{table}' in original.");

        if (!editedTables.Contains(table))
        {
            Console.WriteLine("Table is missing from edited!");
            continue;
        }

        await patchWriter.WriteLineAsync(TablePrefix + table);
        await patchWriter.WriteLineAsync();

        foreach (string column in columns)
        {
            await patchWriter.WriteLineAsync(ColumnPrefix + column);
            await patchWriter.WriteLineAsync();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT originalTab.ID, originalTab.{column}, editedTab.{column}
                FROM {table} AS originalTab
                INNER JOIN edited.{table} AS editedTab ON originalTab.ID = editedTab.ID
                WHERE originalTab.{column} <> editedTab.{column}
            """;

            using SqliteDataReader reader = command.ExecuteReader();
            int rowCount = 0;

            while(reader.Read())
            {
                long id = reader.GetInt64(0);
                string originalText = reader.GetString(1);
                string editedText = reader.GetString(2);

                EditScript<char> editScript = differ.FindEditScript(
                    new StringListAdapter(originalText),
                    new StringListAdapter(editedText)
                );

                await patchWriter.WriteLineAsync(IDPrefix + id);
                await MarkdownSerializer.WriteAsync(patchWriter, ContextSize, editScript, originalText);
                await patchWriter.WriteLineAsync();
                await patchWriter.WriteLineAsync();

                rowCount++;
            }

            Console.WriteLine($"Wrote diff for {rowCount} row(s).");
        }
    }
}

// TODO: These are from OtherParam, deduplicate maybe.
static string ConnectionString(string path, bool write = false) => new SqliteConnectionStringBuilder()
{
    DataSource = path,
    Mode = write ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadOnly,
}.ConnectionString;

static SqliteConnection Open(string path, bool write = false)
{
    SqliteConnection connection = new(ConnectionString(path, write));
    connection.Open();
    return connection;
}

static IReadOnlyCollection<string> GetTables(SqliteConnection connection, string db)
{
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

static void Attach(SqliteConnection connection, string path, string name)
{
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = $"ATTACH DATABASE @path AS {name}";
    command.Parameters.AddWithValue("@path", path);
    command.ExecuteNonQuery();
}