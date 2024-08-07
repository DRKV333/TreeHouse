using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using TreeHouse.Common;
using TreeHouse.Common.IO;

namespace TreeHouse.OtherParams.JsonConverter;

public abstract class DbJsonConverterBase
{
    private readonly ParamSetJsonConverter converter;

    private readonly string keyColumn;
    private readonly string dataColumn;
    private readonly string? classIdColumn;

    private readonly Dictionary<string, Dictionary<Guid, JsonObject>> paramSetsJson = new();

    private readonly byte[] commonBlobBuffer = new byte[100 * 2^10];

    protected DbJsonConverterBase(ParamSetJsonConverter converter, string keyColumn, string dataColumn, string? classIdColumn)
    {
        this.converter = converter;
        this.keyColumn = keyColumn;
        this.dataColumn = dataColumn;
        this.classIdColumn = classIdColumn;
    }

    protected void ReadParamSetsForTable(SqliteConnection connection, string table)
    {
        using SqliteCommand command = connection.CreateCommand();

        if (classIdColumn != null)
            command.CommandText = $"SELECT {keyColumn}, {dataColumn}, {classIdColumn} FROM {table}";
        else
            command.CommandText = $"SELECT {keyColumn}, {dataColumn} FROM {table}";

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            Guid key = new();

            try
            {
                key = Guid.Parse(reader.GetString(0));
                int? classId = classIdColumn != null ? reader.GetInt32(2) : null;

                using Stream dataBlob = reader.GetStream(1);
                ReadFromBlob(table, key, dataBlob, classId);
            }
            catch (Exception e)
            {
                throw new FormatException($"Falied to read row {key} from table {table}", e);
            }
        }
    }

    protected virtual JsonObject GetDefaultJson(string table, Guid key, int? classId) => new JsonObject();

    private void ReadFromBlob(string table, Guid key, Stream blob, int? classId)
    {
        JsonObject json = GetDefaultJson(table, key, classId);

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

    public void WriteParamSetsJson(SqliteConnection connection)
    {
        using SqliteTransaction transaction = connection.BeginTransaction();

        foreach (var tableItem in paramSetsJson)
        {
            string table = tableItem.Key;

            SqliteUtils.AddColumnIfNotExists(connection, table, "TEXT", "dataJSON");

            using SqliteCommand updateCommand = connection.CreateCommand();
            updateCommand.CommandText = $"UPDATE {table} SET dataJSON = $json WHERE {keyColumn} = $key";
            updateCommand.Parameters.Add("$key", SqliteType.Text);
            updateCommand.Parameters.Add("$json", SqliteType.Text);

            foreach (var rowItem in tableItem.Value)
            {
                updateCommand.Parameters["$key"].Value = rowItem.Key.ToString();

                updateCommand.Parameters["$json"].Value = rowItem.Value.ToJsonString(
                    new JsonSerializerOptions()
                    {
                        WriteIndented = true
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
