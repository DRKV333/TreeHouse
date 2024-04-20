using System.Diagnostics.CodeAnalysis;

namespace TreeHouse.Common;

public static class StringExtensions
{
    [return: NotNullIfNotNull(nameof(str))]
    public static string? Capitalize(this string? str)
    {
        if (str == null)
            return null;

        if (str.Length == 0)
            return str;

        if (str.Length == 1)
            return str.ToUpper();

        return char.ToUpper(str[0]) + str[1..];
    }
}
