using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Json.Schema;
using Markdig;
using Markdig.Parsers;
using TreeHouse.Common;
using TreeHouse.PacketDocs;
using TreeHouse.PacketDocs.Lua;
using TreeHouse.PacketDocs.Markdown;
using TreeHouse.PacketDocs.Templates;
using TreeHouse.PacketDocs.Codegen;
using TreeHouse.PacketFormat;
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
    }.WithHandler(LuaHandler),
    new Command("codegen")
    {
        new Option<FileInfo>(new[] { "--output", "-o" }).Required()
    }.WithHandler(CodegenHandler)
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
        Console.WriteLine(error);
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

    MarkdownPipeline pagePipeline = MarkdownPage.CreatePipeline();
    MarkdownPipeline descriptionPipeline = MarkdownContent.CreatePipeline();

    IHeadingProvider[] pages = new IHeadingProvider[]
    {
        new MarkdownPage(
            pagePipeline,
            new HeadingItem("Readme", "Readme", "readme"),
            MarkdownParser.Parse(File.ReadAllText(Path.Join(defsDir.FullName, "README.MD")), pagePipeline)
        ),
        new PacketsPageTemplate(new HeadingItem("Packets", "Packets", "packets"), joinedDocument.Packets, descriptionPipeline),
        new StructuresPageTemplate(new HeadingItem("Structures", "Structures", "structures"), joinedDocument.Structures, descriptionPipeline)
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
    LuaDocumentMapper mapper = new();

    List<StructureFieldType> structsToIndex = new();

    foreach (FileInfo file in defsDir.EnumerateFiles("*.yaml", new EnumerationOptions() { RecurseSubdirectories = true }))
    {
        if (file.Name.EndsWith(".schema.yaml"))
            continue;

        using TextReader reader = file.OpenText();
        PacketFormatDocument document = yamlDeserializer.Deserialize<PacketFormatDocument>(reader);

        mapper.AddDocument(document);
    }

    mapper.SetIndexes();

    LuaDocumentMapper nativeparamMapper = new(singleByteIds: true);
    using TextReader nativeparamReader = new StreamReader(
        Assembly.GetExecutingAssembly().GetManifestResourceStream("nativeparam.yaml")!,
        leaveOpen: false
    );
    nativeparamMapper.AddDocument(yamlDeserializer.Deserialize<PacketFormatDocument>(nativeparamReader));
    nativeparamMapper.SetIndexes();

    using TextWriter writer = output.CreateText();
    await writer.WriteAsync("return ");
    await luaSerializer.Serialize(
        new PacketFormats()
        {
            Main = mapper.LuaDocument,
            Nativeparam = nativeparamMapper.LuaDocument
        },
        writer
    );
}

void CodegenHandler(DirectoryInfo defsDir, FileInfo output)
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

    StructurePreprocessor preprocessor = new();
    foreach (var item in joinedDocument.Structures.Values.Concat(joinedDocument.Packets.Values))
    {
        preprocessor.Preprocess(item);
    }

    File.WriteAllText(output.FullName, new CodegenTemplate(joinedDocument).Render());
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