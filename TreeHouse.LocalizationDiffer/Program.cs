using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using TreeHouse.Common.CommandLine;
using TreeHouse.Common.SQLite;
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
    using SqliteConnection connection = SqliteUtils.Open(original.FullName);
    connection.Attach(edited.FullName, "edited");

    IReadOnlyCollection<string> originalTables = connection.GetTables();
    IReadOnlyCollection<string> editedTables = connection.GetTables("edited");

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
