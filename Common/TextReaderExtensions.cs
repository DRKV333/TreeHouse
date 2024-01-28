using System.Collections.Generic;
using System.IO;

namespace Common;

public static class TextReaderExtensions
{
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
