using Json.Schema;
using PacketDocs;
using PacketDocs.Yaml;
using PacketDocs.Templates;
using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using YamlDotNet.Serialization;
using WebMarkupMin.Core;
using PacketDocs.Markdown;
using Markdig.Parsers;
using Markdig;

IDeserializer yamlDeserializer = PacketFormatDocument.CreateDeserializer();
IDeserializer yamlDeserializerForJson = new DeserializerBuilder().WithAttemptingUnquotedStringTypeDeserialization().Build();

new RootCommand()
{
    new Command("validate").WithHandler(ValidateHandler),
    new Command("check").WithHandler(CheckHandler),
    new Command("build")
    {
        new Option<FileInfo>(new[] { "--output", "-o" }).Required(),
        new Option<bool>("--skip-minify")
    }.WithHandler(BuildHandler)
}
.WithGlobalOption(new Option<DirectoryInfo>(new[] { "--definitions", "-d" }).Required().ExistingOnly())
.Invoke(args);

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

        foreach (var (key, value) in document.Packets)
        {
            joinedDocument.Packets.Add(key, value);
        }

        foreach (var (key, value) in document.Structures)
        {
            joinedDocument.Structures.Add(key, value);
        }
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