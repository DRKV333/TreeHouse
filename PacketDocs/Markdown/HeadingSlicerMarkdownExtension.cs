using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Common;
using Markdig;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using PacketDocs.Templates;

namespace PacketDocs.Markdown;

// This is very jank, but it's not my fault.

internal class HeadingSlicerMarkdownExtension : IMarkdownExtension
{
    private class HeadingSliceBlock : ContainerBlock
    {
        public required HeadingItem Heading { get; init; }

        public HeadingSliceBlock(BlockParser? parser) : base(parser)
        {
        }
    }

    private class HeadingSliceBlockHTMLRenderer : HtmlObjectRenderer<HeadingSliceBlock>
    {
        protected override void Write(HtmlRenderer renderer, HeadingSliceBlock obj)
        {
            string? pageHeadingId = GetParentDocument(obj).GetPageHeadingId();
            if (pageHeadingId == null)
                throw new InvalidOperationException("Page heading id was not set on document");

            if (renderer.EnableHtmlForBlock)
            {
                renderer.Write($"<div id=\"{pageHeadingId}-{obj.Heading.Id}\">");
                renderer.PushIndent("  ");
            }

            renderer.WriteChildren(obj);

            if (renderer.EnableHtmlForBlock)
            {
                renderer.PopIndent();
                renderer.WriteLine("</div>");
            }
        }

        private static MarkdownDocument GetParentDocument(Block block)
        {
            while (block.Parent != null)
            {
                block = block.Parent;
            
                if (block is MarkdownDocument doc)
                    return doc;
            }

            throw new InvalidOperationException("Did not find block parent document");
        }
    }

    private readonly HtmlRenderer plainTextHeaderRenderer;
    private readonly StringBuilder plainTextHeaderBuffer;

    public HeadingSlicerMarkdownExtension(MarkdownPipeline plainTextHeaderPipeline)
    {
        plainTextHeaderBuffer = new StringBuilder();
        StringWriter plainTextWriter = new(plainTextHeaderBuffer);

        plainTextHeaderRenderer = new HtmlRenderer(plainTextWriter)
        {
            EnableHtmlForBlock = false,
            EnableHtmlForInline = false,
            EnableHtmlEscape = false
        };
        plainTextHeaderPipeline.Setup(plainTextHeaderRenderer);
    }

    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        pipeline.DocumentProcessed -= HandleDocumentProcessed;
        pipeline.DocumentProcessed += HandleDocumentProcessed;
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer is HtmlRenderer)
        {
            renderer.ObjectRenderers.AddIfNotAlready<HeadingSliceBlockHTMLRenderer>();
        }
    }

    private void HandleDocumentProcessed(MarkdownDocument document)
    {
        IList<HeadingItem> headings = document.GetHeadingList();
        headings.Clear();

        List<List<Block>> slices = new();
        List<Block> currentSlice = new();

        foreach (Block block in document.EnumerateBackwards())
        {
            currentSlice.Add(block);
            if (block is HeadingBlock)
            {
                slices.Add(currentSlice);
                currentSlice = new List<Block>();
            }
        }

        document.Clear();
        document.AddRange(currentSlice.EnumerateBackwards());
        
        foreach (List<Block> slice in slices.EnumerateBackwards())
        {
            HeadingBlock headingBlock = (HeadingBlock)slice[^1];

            HeadingItem headingItem = GetHeadingItemFromBlock(headingBlock);
            headings.Add(headingItem);

            HeadingSliceBlock sliceBlock = new(headingBlock.Parser)
            {
                Heading = headingItem
            };

            sliceBlock.AddRange(slice.EnumerateBackwards());
            document.Add(sliceBlock);
        }
    }

    private HeadingItem GetHeadingItemFromBlock(HeadingBlock heading)
    {
        plainTextHeaderBuffer.Clear();
        plainTextHeaderRenderer.WriteLeafInline(heading);

        string plainText = plainTextHeaderBuffer.ToString();
        string id = string.Join(null, plainText.ToLower().Replace(' ', '-').Where(x => x == '-' || x.IsAlphaNumeric()));

        return new HeadingItem(plainText, plainText, id);
    }
}