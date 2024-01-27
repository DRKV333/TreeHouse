using System.Collections.Generic;
using System.IO;
using Markdig;
using Markdig.Syntax;
using PacketDocs.Templates;

namespace PacketDocs.Markdown;

internal class MarkdownPage : IHeadingProvider
{
    public HeadingItem PageHeading { get; }

    public IEnumerable<HeadingItem>? Headings => document.GetHeadingList();

    private readonly MarkdownPipeline pipeline;

    private readonly MarkdownDocument document;

    public MarkdownPage(MarkdownPipeline pipeline, HeadingItem pageHeading, MarkdownDocument document)
    {
        this.pipeline = pipeline;
        PageHeading = pageHeading;
        this.document = document;
    }

    public void WriteTo(TextWriter textWriter)
    {
        document.SetPageHeadingId(PageHeading.Id);
        Markdig.Markdown.ToHtml(document, textWriter, pipeline);
    }

    public static MarkdownPipeline CreatePipeline()
    {
        MarkdownPipeline plainTextPipeline = new MarkdownPipelineBuilder().Build();
        return new MarkdownPipelineBuilder()
            .UseBootstrap()
            .Use(new HeadingSlicerMarkdownExtension(plainTextPipeline))
            .Build();
    }
}