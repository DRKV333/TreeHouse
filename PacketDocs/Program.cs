using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Common;
using Json.Schema;
using Markdig;
using Markdig.Parsers;
using PacketDocs;
using PacketDocs.Lua;
using PacketDocs.Markdown;
using PacketDocs.Templates;
using PacketFormat;
using WebMarkupMin.Core;
using YamlDotNet.Serialization;

IDeserializer yamlDeserializer = PacketFormatDocument.CreateDeserializer();
IDeserializer yamlDeserializerForJson = new DeserializerBuilder().WithAttemptingUnquotedStringTypeDeserialization().Build();

await new RootCommand()
{
    new Command("validate").WithHandler(ValidateHandler),
    new Command("check").WithHandler(CheckHandler),
    new Command("build")
    {
        new Option<FileInfo>(new[] { "--output", "-o" }).Required(),
        new Option<bool>("--skip-minify")
    }.WithHandler(BuildHandler),
    new Command("lua")
    {
        new Option<FileInfo>(new[] { "--output", "-o" }).Required()
    }.WithHandler(LuaHandler)
}
.WithGlobalOption(new Option<DirectoryInfo>(new[] { "--definitions", "-d" }).Required().ExistingOnly())
.InvokeAsync(args);

void ValidateHandler(DirectoryInfo defsDir)
{
    FileInfo schemaFile = new(Path.Join(defsDir.FullName, "otherland.packet.schema.yaml"));
    using JsonDocument schemaDoc = YamlToJson(schemaFile);
    JsonSchema jsonSchema = schemaDoc.Deserialize<JsonSchema>()!;

    bool hadError = false;

    foreach (FileInfo file in defsDir.EnumerateFiles("*.yaml", new EnumerationOptions() { RecurseSubdirectories = true }))
    {
        if (file.Name.EndsWith(".schema.yaml"))
            continue;

        using JsonDocument doc = YamlToJson(file);
        EvaluationResults results = jsonSchema.Evaluate(doc, new EvaluationOptions() { OutputFormat = OutputFormat.Hierarchical });

        if (!results.IsValid)
        {
            hadError = true;
            Console.WriteLine($"{file.Name} failed to validate!");
            PrintValidationError(results);
        }
    }

    if (hadError)
        Environment.ExitCode = 1;
}

void CheckHandler(DirectoryInfo defsDir)
{
    DocumentChecker checker = new();

    foreach (FileInfo file in defsDir.EnumerateFiles("*.yaml", new EnumerationOptions() { RecurseSubdirectories = true }))
    {
        if (file.Name.EndsWith(".schema.yaml"))
            continue;

        using TextReader reader = file.OpenText();
        PacketFormatDocument document = yamlDeserializer.Deserialize<PacketFormatDocument>(reader);
        checker.CheckDocument(file.Name, document);
    }

    checker.CheckReferences();

    foreach (DocumentCheckerError error in checker.Errors)
    {
        Console.WriteLine(error.ToErrorMessage());
    }
}

void BuildHandler(DirectoryInfo defsDir, FileInfo output, bool skipMinify)
{
    PacketFormatDocument joinedDocument = new();

    foreach (FileInfo file in defsDir.EnumerateFiles("*.yaml", new EnumerationOptions() { RecurseSubdirectories = true }))
    {
        if (file.Name.EndsWith(".schema.yaml"))
            continue;

        using TextReader reader = file.OpenText();
        PacketFormatDocument document = yamlDeserializer.Deserialize<PacketFormatDocument>(reader);

        joinedDocument.Packets.AddRange(document.Packets);
        joinedDocument.Structures.AddRange(document.Structures);
    }

    MarkdownPipeline pipeline = MarkdownPage.CreatePipeline();

    IHeadingProvider[] pages = new IHeadingProvider[]
    {
        new MarkdownPage(
            pipeline,
            new HeadingItem("Readme", "Readme", "readme"),
            MarkdownParser.Parse(File.ReadAllText(Path.Join(defsDir.FullName, "README.MD")), pipeline)
        ),
        new PacketsPageTemplate(new HeadingItem("Packets", "Packets", "packets"), joinedDocument.Packets),
        new StructuresPageTemplate(new HeadingItem("Structures", "Structures", "structures"), joinedDocument.Structures)
    };

    string indexContent = new IndexTemplate(pages).Render();

    if (!skipMinify)
    {
        HtmlMinifier minifier = new();
        MarkupMinificationResult result = minifier.Minify(indexContent);
        
        foreach (MinificationErrorInfo item in result.Errors.Concat(result.Warnings))
        {
            Console.WriteLine($"{item.LineNumber}:{item.ColumnNumber} {item.Category} {item.Message}");
        }

        indexContent = result.MinifiedContent;
    }

    File.WriteAllText(output.FullName, indexContent, Encoding.UTF8);
}

async Task LuaHandler(DirectoryInfo defsDir, FileInfo output)
{
    LuaLiteralSerializer luaSerializer = LuaPacketFormatDocument.CreateSerializer();
    LuaPacketFormatDocument luaDocument = new();

    Dictionary<string, int> packetIndexes = new();
    int nextPacketIndex = 1;
    
    Dictionary<string, int> structureIndexes = new();
    int nextStructureIndex = 1;

    List<StructureFieldType> structsToIndex = new();

    foreach (FileInfo file in defsDir.EnumerateFiles("*.yaml", new EnumerationOptions() { RecurseSubdirectories = true }))
    {
        if (file.Name.EndsWith(".schema.yaml"))
            continue;

        using TextReader reader = file.OpenText();
        PacketFormatDocument document = yamlDeserializer.Deserialize<PacketFormatDocument>(reader);

        foreach (var packet in document.Packets)
        {
            luaDocument.Packets.Add(new LuaPacketDefinition()
            {
                Original = packet.Value,
                Name = packet.Key,
                Fields = MapLuaFieldItems(packet.Value.Fields, structsToIndex)
            });
            packetIndexes.Add(packet.Key, nextPacketIndex++);
        }

        foreach (var structure in document.Structures)
        {
            luaDocument.Structures.Add(new NamedFieldsList()
            {
                Name = structure.Key,
                Fields = MapLuaFieldItems(structure.Value.Fields, structsToIndex)
            });
            structureIndexes.Add(structure.Key, nextStructureIndex++);
        }
    }

    foreach (var (packet, index) in luaDocument.Packets.WithIndex())
    {
        if (!(packet.Original.Id == 0 && packet.Original.SubId == 0))
        {
            Dictionary<int, int> idDict = luaDocument.ById.TryGetOrAdd(packet.Original.Id, x => new Dictionary<int, int>());
            idDict.Add(packet.Original.SubId, index + 1);
        }

        if (packet.Original.Inherit != null)
        {
            packet.Inherit = packetIndexes[packet.Original.Inherit];
        }
    }

    foreach (StructureFieldType type in structsToIndex)
    {
        type.Index = structureIndexes[type.Name];
    }

    using TextWriter writer = output.CreateText();
    await luaSerializer.Serialize(luaDocument, writer);
}

List<IFieldItem> MapLuaFieldItems(IEnumerable<IFieldItem> fields, IList<StructureFieldType> structsToIndex) =>
    fields.Select(x => MapLuaFieldItem(x, structsToIndex)).ToList();

IFieldItem MapLuaFieldItem(IFieldItem item, IList<StructureFieldType> structsToIndex)
{
    if (item is Field field)
    {
        return new Field()
        {
            Name = field.Name,
            Type = MapLuaFieldType(field.Type, structsToIndex)
        };
    }
    else if (item is Branch branch)
    {
        return new Branch()
        {
            Details = new BranchDetails()
            {
                Field = branch.Details.Field,
                TestEqual = branch.Details.TestEqual,
                TestFlag = branch.Details.TestFlag,
                IsTrue = branch.Details.IsTrue == null ? null : new FieldsList { Fields = MapLuaFieldItems(branch.Details.IsTrue.Fields, structsToIndex) },
                IsFalse = branch.Details.IsFalse == null ? null : new FieldsList { Fields = MapLuaFieldItems(branch.Details.IsFalse.Fields, structsToIndex) },
            }
        };
    }
    else
    {
        return item;
    }
}

IFieldType MapLuaFieldType(IFieldType type, IList<StructureFieldType> structsToIndex)
{
    if (type is ArrayFieldType arrayType)
    {
        return new LuaArrayFieldType()
        {
            Len = ParseIntMaybe(arrayType.Len),
            Type = MapPrimitiveStructureReference(new PrimitiveFieldType() { Value = arrayType.Type }, structsToIndex)
        };
    }
    else if (type is LimitedStringFieldType limitedStringType)
    {
        return new LuaLimitedStringFieldType()
        {
            Name = limitedStringType.Name,
            Maxlen = ParseIntMaybe(limitedStringType.Maxlen)
        };
    }
    else if (type is PrimitiveFieldType primitive)
    {
        return MapPrimitiveStructureReference(primitive, structsToIndex);
    }
    else
    {
        return type;
    }
}

object ParseIntMaybe(string str)
{
    if (int.TryParse(str, out int number))
        return number;
    return str;
}

IFieldType MapPrimitiveStructureReference(PrimitiveFieldType primitive, IList<StructureFieldType> structsToIndex)
{
    if (primitive.Value.StartsWith(':'))
    {
        StructureFieldType type = new()
        {
            Name = primitive.Value[1..]
        };
        structsToIndex.Add(type);
        return type;
    }
    else
    {
        return primitive;
    }
}

void PrintValidationError(EvaluationResults results, int indent = 1)
{
    if (results.IsValid)
        return;

    StringBuilder builder = new();

    builder.Append(' ', indent * 4);

    if (results.InstanceLocation.Segments.Length > 5)
        builder.Append("../");
    builder.AppendJoin('/', results.InstanceLocation.Segments.TakeLast(5).Select(x => x.Value));

    builder.Append(" -> ");

    if (results.EvaluationPath.Segments.Length > 5)
        builder.Append("../");
    builder.AppendJoin('/', results.EvaluationPath.Segments.TakeLast(5).Select(x => x.Value));

    if (results.Errors != null)
    {
        builder.Append(": ").AppendJoin("; ", results.Errors.Values);
    }
    
    Console.WriteLine(builder.ToString());

    foreach (EvaluationResults detail in results.Details)
    {
        PrintValidationError(detail, indent + 1);
    }
}

JsonDocument YamlToJson(FileInfo file)
{
    using TextReader reader = file.OpenText();
    object? dict = yamlDeserializerForJson.Deserialize(reader);
    return JsonSerializer.SerializeToDocument(dict);
}