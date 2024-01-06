using System.Collections.Generic;
using RazorBlade;

namespace PacketDocs.Templates;

internal record class HeadingItem(
    string Title,
    string DisplayTitle,
    string Id
);

internal interface IHeadingProvider : IEncodedContent
{
    HeadingItem PageHeading { get; }

    IEnumerable<HeadingItem>? Headings { get; }
}