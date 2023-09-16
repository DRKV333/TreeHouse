using OtherParams;
using OtherParams.Parsing;
using System.IO;

if (args.Length < 3)
    return;

if (args[0] == "parse")
{
    ParamlistParser parser = new ParamlistParser();
    
    using TextReader reader = File.OpenText(args[1]);
    await parser.ReadParamlistAsync(reader);

    ParamDb db = ParamDb.Open(args[2], true);
    await db.Database.EnsureCreatedAsync();
    await parser.WriteDbAsync(db);
    await db.SaveChangesAsync();
}
else if (args[0] == "print")
{
    ParamDb db = ParamDb.Open(args[1]);
    PumlTemplate template = new PumlTemplate(db);
    using TextWriter writer = File.CreateText(args[2]);
    await template.RenderAsync(writer);
}