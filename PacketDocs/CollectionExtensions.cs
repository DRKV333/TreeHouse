using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace PacketDocs;

internal static class CollectionExtensions
{
    public static bool TryAddOrGet<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value, [NotNullWhen(false)] out TValue? existing)
        where TKey : notnull
    {
        if (dict.TryAdd(key, value))
        {
            existing = default;
            return true;
        }

        existing = dict[key]!;
        return false;
    }

    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> col) => col.Select((x, i) => (x, i));

    public static IEnumerable<T> EnumerateBackwards<T>(this IReadOnlyList<T> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            yield return list[i];
        }
    }

    public static void AddRange<T>(this IList<T> list, IEnumerable<T> items)
    {
        foreach (T item in items)
        {
            list.Add(item);
        }
    }
}
