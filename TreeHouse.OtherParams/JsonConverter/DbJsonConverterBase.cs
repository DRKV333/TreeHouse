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

namespace TreeHouse.OtherParams.JsonConverter;

public abstract class DbJsonConverterBase
{
    private readonly ParamSetJsonConverter converter;

    private readonly string keyColumn;
    private readonly string dataColumn;
    private readonly string? classIdColumn;
    private readonly string? extraColumn;

    private readonly Dictionary<string, Dictionary<Guid, JsonObject>> paramSetsJson = new();

    private readonly byte[] commonBlobBuffer = new byte[100 * 2^10];

    protected DbJsonConverterBase(ParamSetJsonConverter converter, string keyColumn, string dataColumn, string? classIdColumn, string? extraColumn = null)
    {
        this.converter = converter;
        this.keyColumn = keyColumn;
        this.dataColumn = dataColumn;
        this.classIdColumn = classIdColumn;
        this.extraColumn = extraColumn;
    }

    protected void ReadParamSetsForTable(SqliteConnection connection, string table)
    {
        int extraOrd = 2;

        StringBuilder commandBuilder = new();
        commandBuilder.Append($"SELECT {keyColumn}, {dataColumn}");
        if (classIdColumn != null)
        {
            commandBuilder.Append($", {classIdColumn}");
            extraOrd = 3;
        }
        if (extraColumn != null)
            commandBuilder.Append($", {extraColumn}");
        commandBuilder.Append($" FROM {table}");

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandBuilder.ToString();

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            Guid key = Guid.Empty;

            try
            {
                key = Guid.Parse(reader.GetString(0));
                int? classId = classIdColumn != null ? reader.GetInt32(2) : null;
                string? extra = extraColumn != null ? reader.GetString(extraOrd) : null;

                using Stream dataBlob = reader.GetStream(1);
                ReadFromBlob(table, key, dataBlob, classId, extra);
            }
            catch (Exception e)
            {
                throw new FormatException($"Falied to read row {key} from table {table}", e);
            }
        }
    }

    protected virtual JsonObject GetDefaultJson(string table, Guid key, int? classId, string? extra) => new JsonObject();

    private void ReadFromBlob(string table, Guid key, Stream blob, int? classId, string? extra)
    {
        JsonObject json = GetDefaultJson(table, key, classId, extra);

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
                SqliteUtils.AddColumnIfNotExists(connection, table, "TEXT", "dataJSON");

            if (writeJsonb)
                SqliteUtils.AddColumnIfNotExists(connection, table, "BLOB", "dataJSONB");

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
