using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using TreeHouse.Common;
using TreeHouse.Common.IO;
using TreeHouse.Common.SQLite;

namespace TreeHouse.OtherParams.JsonConverter;

public abstract class DbJsonConverterBase
{
    protected const int OrdKeyColumn = 0;
    protected const int OrdDataColumn = 1;
    protected const int OrdExtraOffset = 2;

    protected readonly ParamSetJsonConverter converter;

    private readonly string columns;
    protected readonly string keyColumn;
    protected readonly int? ordClassIdColumn;

    private readonly Dictionary<string, Dictionary<Guid, JsonObject>> paramSetsJson = new();

    private readonly byte[] commonBlobBuffer = new byte[100 * 2^10];

    protected DbJsonConverterBase(ParamSetJsonConverter converter, string keyColumn, string dataColumn, IEnumerable<string> extraColumns, int? ordClassIdColumn)
    {
        this.converter = converter;
        this.keyColumn = keyColumn;
        this.ordClassIdColumn = ordClassIdColumn;

        columns = string.Join(", ", new[] { keyColumn, dataColumn }.Concat(extraColumns));
    }

    protected void ReadParamSetsForTable(SqliteConnection connection, string table)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {columns} FROM {table}";

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            Guid key = Guid.Empty;

            try
            {
                key = Guid.Parse(reader.GetString(OrdKeyColumn));
                int? classId = ordClassIdColumn != null ? reader.GetInt32(ordClassIdColumn.Value) : null;

                JsonObject json = GetDefaultJson(table, reader);

                using Stream dataBlob = reader.GetStream(OrdDataColumn);
                ReadFromBlob(json, table, key, dataBlob, classId);
            }
            catch (Exception e)
            {
                throw new FormatException($"Falied to read row {key} from table {table}", e);
            }
        }
    }

    protected virtual JsonObject GetDefaultJson(string table, SqliteDataReader reader) => new JsonObject();

    private void ReadFromBlob(JsonObject json, string table, Guid key, Stream blob, int? classId)
    {
        byte[] blobBuffer = commonBlobBuffer;

        int blobLength = (int)blob.Length;
        if (blobLength > blobBuffer.Length)
            blobBuffer = new byte[blobLength];

        blob.ReadExactly(blobBuffer, 0, blobLength);

        SpanReader spanReader = new(new ReadOnlySpan<byte>(blobBuffer, 0, blobLength));
        converter.ConvertInto(json, spanReader, classId);

        Dictionary<Guid, JsonObject> tableJsons = paramSetsJson.TryGetOrAdd(table, k => new Dictionary<Guid, JsonObject>());
        tableJsons[key] = json;
    }

    public void WriteParamSetsJson(SqliteConnection connection, bool writeJson = true, bool formatJson = true, bool writeJsonb = false)
    {
        if (!writeJson && !writeJsonb)
            return;

        if (!writeJson)
            formatJson = false;

        using SqliteTransaction transaction = connection.BeginTransaction();

        foreach (var tableItem in paramSetsJson)
        {
            string table = tableItem.Key;

            if (writeJson)
                connection.AddColumnIfNotExists(table, "TEXT", "dataJSON");

            if (writeJsonb)
                connection.AddColumnIfNotExists(table, "BLOB", "dataJSONB");

            StringBuilder commandBuilder = new();
            commandBuilder.Append($"UPDATE {table} SET ");
            if (writeJson)
                commandBuilder.Append("dataJSON = $json ");
            if (writeJson && writeJsonb)
                commandBuilder.Append(", ");
            if (writeJsonb)
                commandBuilder.Append("dataJSONB = jsonb($json) ");
            commandBuilder.Append($"WHERE {keyColumn} = $key");

            using SqliteCommand updateCommand = connection.CreateCommand();
            updateCommand.CommandText = commandBuilder.ToString();
            updateCommand.Parameters.Add("$key", SqliteType.Text);
            updateCommand.Parameters.Add("$json", SqliteType.Text);

            foreach (var rowItem in tableItem.Value)
            {
                updateCommand.Parameters["$key"].Value = rowItem.Key.ToString();

                updateCommand.Parameters["$json"].Value = rowItem.Value.ToJsonString(
                    new JsonSerializerOptions()
                    {
                        WriteIndented = formatJson
                    }
                );

                updateCommand.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }

    public DefaultValueProvider<Guid> GetDefaultValueProvider() =>
        new DefaultValueProvider<Guid>(paramSetsJson.Values.SelectMany(x => x).ToDictionary());
}
