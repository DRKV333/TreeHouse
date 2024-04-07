using System.Collections.Generic;
using System.Linq;
using System.Text;
using Markdig;
using RazorBlade;
using RazorBlade.Support;
using TreeHouse.PacketFormat;

namespace TreeHouse.PacketDocs.Templates;

internal abstract class FieldsListPageTemplateBase<T> : HtmlTemplate, IHeadingProvider
    where T : FieldsList
{
    public HeadingItem PageHeading { get; }

    protected readonly List<(HeadingItem heading, T definition)> Definitions;

    protected readonly MarkdownPipeline DescriptionPipeline;

    public IEnumerable<HeadingItem>? Headings => Definitions.Select(x => x.heading);
    
    [TemplateConstructor]
    protected FieldsListPageTemplateBase(HeadingItem pageHeading, IDictionary<string, T> definitions, MarkdownPipeline descriptionPipeline)
    {
        PageHeading = pageHeading;
        DescriptionPipeline = descriptionPipeline;

        Definitions = definitions
            .OrderBy(x => x.Key)
            .Select(x => (new HeadingItem(x.Key, BreakCamelCase(x.Key), x.Key.ToLower()), x.Value))
            .ToList();
    }

    private static string BreakCamelCase(string str)
    {
        if (str.Length == 0)
            return str;    

        StringBuilder builder = new();
        builder.Append(str[0]);
        for (int i = 1; i < str.Length; i++)
        {
            if (char.IsLower(str[i - 1]) && char.IsUpper(str[i]))
                builder.Append('\u200B');
            builder.Append(str[i]);
        }

        return builder.ToString();
    }
}