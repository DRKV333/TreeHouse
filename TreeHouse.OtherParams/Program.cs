using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
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
        new Option<FileInfo>(new string[] { "--content-db", "-c" }).ExistingOnly().Required()
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

async Task JsonConvertHandler(FileInfo paramDb, FileInfo contentDb)
{
    using SqliteConnection conn = SqliteUtils.Open(contentDb.FullName, write: true);
    using ParamDb db = ParamDb.Open(paramDb.FullName);

    ParamSetJsonConverter converter = await ParamSetJsonConverter.CreateInstance(db);
    ContentDbJsonConverter dbConverter = new(converter);

    foreach (Table table in db.Tables.ToList())
    {
        dbConverter.ReadParamSets(conn, table);
    }

    dbConverter.WriteParamSetsJson(conn);
}
