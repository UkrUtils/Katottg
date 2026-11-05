namespace UkrUtils.Katottg;

/// <summary>How exactly the name matched the query. The smaller the value, the better the match.</summary>
public enum MatchKind
{
    /// <summary>The query is a KATOTTG code (possibly with a wrong tail).</summary>
    Code,

    /// <summary>The normalized name equals the query.</summary>
    Exact,

    /// <summary>The name starts with the query.</summary>
    Prefix,

    /// <summary>One of the name's words (not the first) starts with the query: "Подільський" → "Могилів-Подільський".</summary>
    WordPrefix,
}

/// <summary>A found object.</summary>
/// <param name="Unit">Codifier object.</param>
/// <param name="Score">Score; higher is better. Worth comparing only within a single search.</param>
/// <param name="Kind">Match kind.</param>
public sealed record KatottgSearchHit(KatottgUnit Unit, double Score, MatchKind Kind);

/// <summary>Result of a free-text search.</summary>
public sealed class KatottgSearchResult
{
    internal KatottgSearchResult(string text, IReadOnlyList<KatottgSearchHit> hits)
    {
        Text = text;
        Hits = hits;
    }

    /// <summary>The query string.</summary>
    public string Text { get; }

    /// <summary>Found objects from best to worst.</summary>
    public IReadOnlyList<KatottgSearchHit> Hits { get; }

    /// <summary>The best match or <c>null</c>.</summary>
    public KatottgSearchHit? Best => Hits.Count > 0 ? Hits[0] : null;

    /// <summary>
    /// Results as a tree: the found objects (<see cref="KatottgTreeNode.IsMatch"/>) together
    /// with the regions, districts, and communities they lie in.
    /// </summary>
    public IReadOnlyList<KatottgTreeNode> ToTree() => KatottgTreeNode.Build(Hits.Select(h => h.Unit));
}

/// <summary>Options for a free-text search.</summary>
public sealed class KatottgSearchOptions
{
    /// <summary>Maximum number of results. 20 by default.</summary>
    public int Limit { get; init; } = 20;

    /// <summary>Search only among these categories. Combined with markers in the query ("с.", "м."…).</summary>
    public IReadOnlyCollection<KatottgCategory>? Categories { get; init; }

    /// <summary>Search only inside this object (including the object itself).</summary>
    public KatottgUnit? Within { get; init; }
}

/// <summary>Structured query: a name plus optional context.</summary>
public sealed class KatottgQuery
{
    /// <summary>Object name. May contain a category marker: "с. Андріївка".</summary>
    public required string Name { get; init; }

    /// <summary>Name of the region (or city with special status) to search in.</summary>
    public string? Region { get; init; }

    /// <summary>Name of the district to search in.</summary>
    public string? District { get; init; }

    /// <summary>Name of the community to search in.</summary>
    public string? Community { get; init; }

    /// <summary>Search only among these categories.</summary>
    public IReadOnlyCollection<KatottgCategory>? Categories { get; init; }

    /// <summary>Search only inside this object (including the object itself).</summary>
    public KatottgUnit? Within { get; init; }

    /// <summary>Maximum number of results. 20 by default.</summary>
    public int Limit { get; init; } = 20;
}
