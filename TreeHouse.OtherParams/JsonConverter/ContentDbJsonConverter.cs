using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using TreeHouse.Common;
using TreeHouse.Common.IO;

namespace TreeHouse.OtherParams.JsonConverter;

public class ContentDbJsonConverter
{
    private record class TableData(
        Model.Table Table,
        Dictionary<Guid, JsonObject> ParamSetJson
    );

    private readonly ParamSetJsonConverter converter;

    private readonly Dictionary<int, TableData> tableDatas = new();

    private readonly byte[] commonBlobBuffer = new byte[100 * 2^10];

    public ContentDbJsonConverter(ParamSetJsonConverter converter)
    {
        this.converter = converter;
    }

    public void ReadParamSets(SqliteConnection connection, Model.Table table)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT guid, ixClass, data FROM {table.Name}";

        using SqliteDataReader reader = command.ExecuteReader();
        
        while (reader.Read())
        {
            Guid guid = new();
            
            try
            {
                guid = Guid.Parse(reader.GetString(0));
                int classId = reader.GetInt32(1);

                byte[] blobBuffer = commonBlobBuffer;
                int blobLength;

                using (Stream dataBlob = reader.GetStream(2))
                {
                    blobLength = (int)dataBlob.Length;

                    if (blobLength > blobBuffer.Length)
                        blobBuffer = new byte[blobLength];

                    dataBlob.ReadExactly(blobBuffer, 0, blobLength);
                }

                SpanReader spanReader = new(new ReadOnlySpan<byte>(blobBuffer, 0, blobLength));
                JsonObject json = new();
                converter.ConvertInto(json, spanReader, classId);

                TableData data = tableDatas.TryGetOrAdd(table.Id, id => new TableData(table, new()));
                data.ParamSetJson[guid] = json;
            }
            catch (Exception e)
            {
                throw new FormatException($"Falied to read row {guid} from table {table.Name}", e);
            }
        }
    }

    public void WriteParamSetsJson(SqliteConnection connection)
    {
        using SqliteTransaction transaction = connection.BeginTransaction();

        foreach (TableData data in tableDatas.Values)
        {
            AddColumnIfNotExists(connection, data.Table.Name, "dataJSON", "TEXT");

            using SqliteCommand updateCommand = connection.CreateCommand();
            updateCommand.CommandText = $"UPDATE {data.Table.Name} SET dataJSON = $json WHERE guid = $guid";
            updateCommand.Parameters.Add("$guid", SqliteType.Text);
            updateCommand.Parameters.Add("$json", SqliteType.Text);

            foreach (var item in data.ParamSetJson)
            {
                updateCommand.Parameters["$guid"].Value = item.Key.ToString();

                updateCommand.Parameters["$json"].Value = item.Value.ToJsonString(
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }
                );

                updateCommand.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }

    public static void AddColumnIfNotExists(SqliteConnection connection, string table, string column, string type)
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
