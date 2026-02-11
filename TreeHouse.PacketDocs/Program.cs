using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Markdig;
using Markdig.Parsers;
using TreeHouse.Common;
using TreeHouse.Common.CommandLine;
using TreeHouse.PacketDocs.Lua;
using TreeHouse.PacketDocs.Markdown;
using TreeHouse.PacketDocs.Templates;
using TreeHouse.PacketDocs.Codegen;
using TreeHouse.PacketFormat;
using WebMarkupMin.Core;
using YamlDotNet.Serialization;
using Corvus.Json.Validator;
using Corvus.Json;

IDeserializer yamlDeserializer = PacketFormatDocument.CreateDeserializer();
IDeserializer yamlDeserializerForJson = new DeserializerBuilder().WithAttemptingUnquotedStringTypeDeserialization().Build();

await new RootCommand()
{
    new Command("validate").WithHandler(ValidateHandler),
    new Command("check").WithHandler(CheckHandler),
    new Command("build")
    {
        new Option<FileInfo>(["--output", "-o"]).Required(),
        new Option<bool>("--skip-minify")
    }.WithHandler(BuildHandler),
    new Command("lua")
    {
        new Option<FileInfo>(["--output", "-o"]).Required()
    }.WithHandler(LuaHandler),
    new Command("codegen")
    {
        new Option<FileInfo>(["--output", "-o"]).Required()
    }.WithHandler(CodegenHandler)
}
.WithGlobalOption(new Option<DirectoryInfo>(["--definitions", "-d"]).Required().ExistingOnly())
.InvokeAsync(args);

void ValidateHandler(DirectoryInfo defsDir)
{
    FileInfo schemaFile = new(Path.Join(defsDir.FullName, "otherland.packet.schema.yaml"));
    using JsonDocument schemaDoc = YamlToJson(schemaFile);

    string schemaId = "https://github.com/plehmkuhl/otherland-packet-formats/otherland.packet.schema.yaml";

    PrepopulatedDocumentResolver resolver = new();
    resolver.AddDocument(schemaId, schemaDoc);

    JsonSchema jsonSchema = JsonSchema.From(
        schemaId,
        new JsonSchema.Options(
            allowFileSystemAndHttpResolution: false,
            additionalDocumentResolver: resolver
        )
    );

    bool hadError = false;

    foreach (FileInfo file in defsDir.EnumerateFiles("*.yaml", new EnumerationOptions() { RecurseSubdirectories = true }))
    {
        if (file.Name.EndsWith(".schema.yaml"))
            continue;

        using JsonDocument doc = YamlToJson(file);
        ValidationContext context = jsonSchema.Validate(doc.RootElement, ValidationLevel.Detailed);

        if (!context.IsValid)
        {
            hadError = true;

            Console.WriteLine($"=== {file.Name} failed to validate! ===");

            foreach (ValidationResult result in context.Results)
            {
                Console.WriteLine(result);
            }
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

JsonDocument YamlToJson(FileInfo file)
{
    using TextReader reader = file.OpenText();
    object? dict = yamlDeserializerForJson.Deserialize(reader);
    return JsonSerializer.SerializeToDocument(dict);
}