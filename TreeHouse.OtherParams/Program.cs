﻿using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TreeHouse.Common.CommandLine;
using TreeHouse.OtherParams;
using TreeHouse.OtherParams.JsonConverter;
using TreeHouse.OtherParams.Model;
using TreeHouse.OtherParams.Parsing;

await new RootCommand()
{
    new Command("parse")
    {
        new Option<FileInfo>(new string[] { "--param-db", "-d" }).Required(),
        new Option<FileInfo>(new string[] { "--param-list", "-l" }).ExistingOnly().Required()
    }.WithHandler(ParseHandler),
    new Command("print")
    {
        new Option<FileInfo>(new string[] { "--param-db", "-d" }).ExistingOnly().Required(),
        new Option<FileInfo>(new string[] { "--output", "-o" }).Required()
    }.WithHandler(PrintHandler),
    new Command("json-convert")
    {
        new Option<FileInfo>(new string[] { "--param-db", "-d" }).ExistingOnly().Required(),
        new Option<FileInfo>(new string[] { "--content-db", "-c" }).ExistingOnly(),
        new Option<FileInfo>(new string[] { "--instance-db", "-i" }).ExistingOnly(),
        new Option<bool>("--write-jsonb"),
        new Option<bool>("--write-unformatted"),
        new Option<bool>("--no-write-json")
    }.WithHandler(JsonConvertHandler)
}
.InvokeAsync(args);

async Task ParseHandler(FileInfo paramDb, FileInfo paramList)
{
    ParamlistParser parser = new();
    
    using TextReader reader = paramList.OpenText();
    await parser.ReadParamlistAsync(reader);

    ParamDb db = ParamDb.Open(paramDb.FullName, write: true);
    await db.Database.EnsureCreatedAsync();
    await parser.WriteDbAsync(db);
    await db.SaveChangesAsync();
}

async Task PrintHandler(FileInfo paramDb, FileInfo output)
{
    ParamDb db = ParamDb.Open(paramDb.FullName);
    PumlTemplate template = new(db);
    using TextWriter writer = output.CreateText();
    await template.RenderAsync(writer);
}

async Task JsonConvertHandler(FileInfo paramDb, FileInfo? contentDb, FileInfo? instanceDb, bool writeJsonb, bool writeUnformatted, bool noWriteJson)
{
    if (contentDb == null && instanceDb == null)
    {
        Console.WriteLine("Neither content, nor instance db was given, nothing to do.");
        return;
    }

    using ParamDb db = ParamDb.Open(paramDb.FullName);
    ParamSetJsonConverter converter = await ParamSetJsonConverter.CreateInstance(db);

    if (contentDb != null)
    {
        using SqliteConnection connection = SqliteUtils.Open(contentDb.FullName, write: true);
        ContentDbJsonConverter dbConverter = new(converter);

        Console.WriteLine("Reading content db...");

        foreach (Table table in await db.Tables.ToListAsync())
        {
            Console.WriteLine($"Reading table {table.Name}...");
            dbConverter.ReadParamSets(connection, table);
        }

        Console.WriteLine("Writing json to content db...");

        dbConverter.WriteParamSetsJson(
            connection: connection,
            writeJson: !noWriteJson,
            formatJson: !writeUnformatted,
            writeJsonb: writeJsonb
        );
    }

    if (instanceDb != null)
    {
        using SqliteConnection connection = SqliteUtils.Open(instanceDb.FullName, write: true);
        InstanceDbJsonConverter dbConverter = new(converter);

        Console.WriteLine("Reading instance db...");

        dbConverter.ReadParamSets(connection);

        Console.WriteLine("Writing json to instance db...");

        dbConverter.WriteParamSetsJson(
            connection: connection,
            writeJson: !noWriteJson,
            formatJson: !writeUnformatted,
            writeJsonb: writeJsonb
        );
    }

    Console.WriteLine("Done.");
}
