using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UniDelta.Myers;

namespace TreeHouse.LocalizationDiffer;

internal static class MarkdownSerializer
{
    public static async Task WriteAsync(TextWriter writer, int contextSize, EditScript<char> editScript, string source)
    {
        int pos = 0;
        bool firstEdit = true;

        foreach (var item in GetEditSections(editScript, source))
        {
            int maxContextDistance = firstEdit ? contextSize : contextSize * 2;

            if (item.start > pos + maxContextDistance)
            {
                if (!firstEdit)
                    await writer.WriteAsync(source.AsMemory(pos, contextSize));

                await writer.WriteAsync("&mldr;");
                await writer.WriteAsync(source.AsMemory(item.start - contextSize, contextSize));
            }
            else
            {
                await writer.WriteAsync(source.AsMemory(pos, item.start - pos));
            }

            StringBuilder escapedContent = new();
            if (item.content.StartsWith(' '))
                escapedContent.Append("&#32;");
            else
                escapedContent.Append(item.content[0]);
            if (item.content.Length > 2)
                escapedContent.Append(item.content[1..^1]);
            if (item.content.Length > 1)
            {
                if (item.content.EndsWith(' '))
                    escapedContent.Append("&#32;");
                else
                    escapedContent.Append(item.content[^1]);
            }

            if (item.delete)
            {
                await writer.WriteAsync($"[~~{escapedContent}~~]({item.start})");
                pos = item.start + item.content.Length;
            }
            else
            {
                await writer.WriteAsync($"[**{escapedContent}**]({item.start})");
                pos = item.start;
            }

            firstEdit = false;
        }

        if (pos < source.Length)
        {
            int remainingLength = source.Length - pos;

            if (remainingLength > contextSize)
            {
                await writer.WriteAsync(source.AsMemory(pos, contextSize));
                await writer.WriteAsync("&mldr;");
            }
            else
            {
                await writer.WriteAsync(source.AsMemory(pos, remainingLength));
            }
        }
    }

    private static IEnumerable<(int start, string content, bool delete)> GetEditSections<T>(EditScript<T> editScript, string source)
    {
        int iInsert = 0;
        int iDelete = 0;

        while (iDelete < editScript.Deletes.Count && iInsert < editScript.Inserts.Count)
        {
            if (editScript.Deletes[iDelete].Index < editScript.Inserts[iInsert].Index)
            {
                int start = editScript.Deletes[iDelete].Index;
                string content = source.Substring(start, editScript.Deletes[iDelete].Lenght);
                yield return (start, content, true);
                iDelete++;
            }
            else
            {
                yield return (editScript.Inserts[iInsert].Index, string.Concat(editScript.Inserts[iInsert].Values), false);
                iInsert++;
            }
        }

        while (iInsert < editScript.Inserts.Count)
        {
            yield return (editScript.Inserts[iInsert].Index, string.Concat(editScript.Inserts[iInsert].Values), false);
            iInsert++;
        }

        while (iDelete < editScript.Deletes.Count)
        {
            int start = editScript.Deletes[iDelete].Index;
            string content = source.Substring(start, editScript.Deletes[iDelete].Lenght);
            yield return (start, content, true);
            iDelete++;
        }
    }
}
