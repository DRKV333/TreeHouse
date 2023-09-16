using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace OtherParams;

internal static class Extensions
{
    public static bool TryMatch(this Regex regex, string str, out Match match)
    {
        match = regex.Match(str);
        return match.Success;
    }

    public static string? ValueIfSuccess(this Group group) => group.Success ? group.Value : null;

    public static async IAsyncEnumerable<string> ReadAllLinesAsync(this TextReader reader)
    {
        string? line = await reader.ReadLineAsync();
        while (line != null)
        {
            yield return line;
            line = await reader.ReadLineAsync();
        }
    }
}
