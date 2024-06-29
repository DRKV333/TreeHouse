using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using TreeHouse.Common.CommandLine;
using TreeHouse.OtherParams;
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
    }.WithHandler(PrintHandler)
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