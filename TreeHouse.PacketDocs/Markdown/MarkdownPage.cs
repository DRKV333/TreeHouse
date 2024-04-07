using System.Collections.Generic;
using System.IO;
using Markdig;
using Markdig.Syntax;
using TreeHouse.PacketDocs.Templates;

namespace TreeHouse.PacketDocs.Markdown;

internal class MarkdownPage : MarkdownContent, IHeadingProvider
{
    public HeadingItem PageHeading { get; }

    public IEnumerable<HeadingItem>? Headings => document.GetHeadingList();

    public MarkdownPage(MarkdownPipeline pipeline, HeadingItem pageHeading, MarkdownDocument document)
        : base(pipeline, document)
    {
        PageHeading = pageHeading;
    }

    public override void WriteTo(TextWriter textWriter)
    {
        document.SetPageHeadingId(PageHeading.Id);
        base.WriteTo(textWriter);
    }

    public static new MarkdownPipeline CreatePipeline()
    {
        MarkdownPipeline plainTextPipeline = new MarkdownPipelineBuilder().Build();
        return new MarkdownPipelineBuilder()
            .UseBootstrap()
            .Use(new HeadingSlicerMarkdownExtension(plainTextPipeline))
            .Build();
    }
}