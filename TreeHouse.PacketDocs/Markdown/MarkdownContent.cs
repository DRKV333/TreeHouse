using System.IO;
using Markdig;
using Markdig.Syntax;
using RazorBlade;

namespace TreeHouse.PacketDocs.Markdown;

public class MarkdownContent : IEncodedContent
{
    private readonly MarkdownPipeline pipeline;

    protected readonly MarkdownDocument document;

    public MarkdownContent(MarkdownPipeline pipeline, MarkdownDocument document)
    {
        this.pipeline = pipeline;
        this.document = document;
    }

    public virtual void WriteTo(TextWriter textWriter)
    {
        Markdig.Markdown.ToHtml(document, textWriter, pipeline);
    }

    public static MarkdownPipeline CreatePipeline() =>
        new MarkdownPipelineBuilder()
            .UseBootstrap()
            .Build();
}
