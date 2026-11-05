namespace UkrUtils.Katottg.Search;

/// <summary>
/// Prefix index of normalized names: a sorted array of "suffixes from the start of each word"
/// and binary search. More compact than a trie and just as fast for ~30 thousand names.
/// </summary>
internal sealed class NameIndex
{
    private readonly string[] _keys;
    private readonly Entry[] _entries;

    /// <param name="Key">Index into <see cref="_keys"/>.</param>
    /// <param name="Offset">Start of the word in the key; 0 — the whole name.</param>
    /// <param name="Unit">Index of the object in the dataset.</param>
    private readonly record struct Entry(int Key, int Offset, int Unit);

    public NameIndex(IReadOnlyList<KatottgUnit> units)
    {
        var keyIds = new Dictionary<string, int>(StringComparer.Ordinal);
        var keys = new List<string>();
        var entries = new List<Entry>(units.Count * 5 / 4);

        for (var i = 0; i < units.Count; i++)
        {
            var key = KatottgNormalizer.Normalize(units[i].Name);
            if (key.Length == 0)
                continue;
            if (!keyIds.TryGetValue(key, out var id))
            {
                id = keys.Count;
                keys.Add(key);
                keyIds.Add(key, id);
            }
            entries.Add(new Entry(id, 0, i));
            for (var p = key.IndexOf(' '); p >= 0; p = key.IndexOf(' ', p + 1))
                entries.Add(new Entry(id, p + 1, i));
        }

        _keys = [.. keys];
        _entries = [.. entries];
        Array.Sort(_entries, Compare);
    }

    private ReadOnlySpan<char> Suffix(in Entry e) => _keys[e.Key].AsSpan(e.Offset);

    private int Compare(Entry a, Entry b)
    {
        var c = Suffix(a).SequenceCompareTo(Suffix(b));
        if (c != 0)
            return c;
        c = a.Offset.CompareTo(b.Offset);
        return c != 0 ? c : a.Unit.CompareTo(b.Unit);
    }

    /// <summary>
    /// Matches for the normalized query: for each object, the best match kind and the base score.
    /// </summary>
    public void Match(string query, bool includeWordPrefix, Dictionary<int, (MatchKind Kind, double Score)> results)
    {
        if (query.Length == 0)
            return;

        var q = query.AsSpan();
        for (var i = LowerBound(q); i < _entries.Length; i++)
        {
            var e = _entries[i];
            var suffix = Suffix(e);
            if (!suffix.StartsWith(q, StringComparison.Ordinal))
                break;

            MatchKind kind;
            double score;
            if (e.Offset == 0)
            {
                kind = suffix.Length == q.Length ? MatchKind.Exact : MatchKind.Prefix;
                score = kind == MatchKind.Exact ? 1.0 : 0.7 + 0.2 * q.Length / suffix.Length;
            }
            else
            {
                if (!includeWordPrefix)
                    continue;
                kind = MatchKind.WordPrefix;
                score = 0.5 + 0.2 * q.Length / suffix.Length;
            }

            if (!results.TryGetValue(e.Unit, out var existing) || existing.Score < score)
                results[e.Unit] = (kind, score);
        }
    }

    private int LowerBound(ReadOnlySpan<char> q)
    {
        int lo = 0, hi = _entries.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (Suffix(_entries[mid]).SequenceCompareTo(q) < 0)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }
}
