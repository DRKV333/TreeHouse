using System.Collections.Generic;
using Markdig.Syntax;
using TreeHouse.PacketDocs.Templates;

namespace TreeHouse.PacketDocs.Markdown;

internal static class HeadingSlicerDocumentExtensions
{
    private static readonly object PageHeadingIdKey = new();

    private static readonly object HeadingListKey = new();

    public static void SetPageHeadingId(this MarkdownDocument document, string pageHeading) =>
        document.SetData(PageHeadingIdKey, pageHeading);

    public static string? GetPageHeadingId(this MarkdownDocument document) =>
        document.GetData(PageHeadingIdKey) as string;

    public static IList<HeadingItem> GetHeadingList(this MarkdownDocument document)
    {
        IList<HeadingItem>? headingList = document.GetData(HeadingListKey) as IList<HeadingItem>;
        
        if (headingList == null)
        {
            headingList = new List<HeadingItem>();
            document.SetData(HeadingListKey, headingList);
        }

        return headingList;
    }
}