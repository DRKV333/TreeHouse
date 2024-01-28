using System.Text.RegularExpressions;

namespace Common;

public static class RegexExtensions
{
    public static bool TryMatch(this Regex regex, string str, out Match match)
    {
        match = regex.Match(str);
        return match.Success;
    }

    public static string? ValueIfSuccess(this Group group) => group.Success ? group.Value : null;
}
