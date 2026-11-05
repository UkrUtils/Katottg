using UkrUtils.Katottg.Search;

namespace UkrUtils.Katottg;

public sealed partial class KatottgDataset
{
    private NameIndex? _nameIndex;

    /// <summary>Індекс назв будується ліниво при першому пошуку.</summary>
    private NameIndex NameIndex => LazyInitializer.EnsureInitialized(ref _nameIndex, () => new NameIndex(_units));

    /// <summary>
    /// Пошук за вільним рядком: назвою, кодом або адресою через коми
    /// («с. Андріївка, Бердянський р-н, Запорізька обл.»).
    /// </summary>
    /// <remarks>
    /// Маркери («с.», «м.», «смт», «р-н», «обл.», «громада»…) стають фільтром за категорією. Якщо
    /// з таким фільтром нічого не знайдено, пошук повторюється без нього: категорію в адресі часто
    /// вказують неточно. Решта частин після ком — контекст: результат має лежати всередині
    /// об’єкта з такою назвою. Перемагає та частина як ціль, що дає найконкретніший результат.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="KatottgSearchOptions.Limit"/> менший за 1.</exception>
    public KatottgSearchResult Search(string text, KatottgSearchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        options ??= new KatottgSearchOptions();
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Limit, 1, nameof(options));

        if (KatottgCode.TryParse(text, out var code))
        {
            var unit = Find(code) ?? FindBySignificantPart(code);
            var hits = unit is not null && Accepts(unit, options.Categories, options.Within)
                ? [new KatottgSearchHit(unit, 2.0, MatchKind.Code)]
                : Array.Empty<KatottgSearchHit>();
            return new KatottgSearchResult(text, hits);
        }

        var parts = text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(QueryPart.Parse)
            .Where(p => !p.IsEmpty)
            .ToList();

        List<KatottgSearchHit> best = [];
        (int Depth, double Score) bestKey = (-1, 0);
        for (var i = 0; i < parts.Count; i++)
        {
            var contexts = parts.Where((_, j) => j != i).ToList();
            var hits = Run(parts[i], contexts, options.Categories, options.Within, options.Limit);
            if (hits.Count == 0)
                continue;
            var key = (hits[0].Unit.Depth, hits[0].Score);
            if (key.Depth > bestKey.Depth || (key.Depth == bestKey.Depth && key.Score > bestKey.Score))
            {
                best = hits;
                bestKey = key;
            }
        }
        return new KatottgSearchResult(text, best);
    }

    /// <summary>Структурований пошук: назва плюс необов’язкові область, район і громада.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="KatottgQuery.Limit"/> менший за 1.</exception>
    public IReadOnlyList<KatottgSearchHit> Search(KatottgQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.Limit, 1, nameof(query));

        var target = QueryPart.Parse(query.Name);
        var contexts = new List<QueryPart>();
        if (!string.IsNullOrWhiteSpace(query.Region))
            contexts.Add(QueryPart.Parse(query.Region, QueryPart.RegionRole));
        if (!string.IsNullOrWhiteSpace(query.District))
            contexts.Add(QueryPart.Parse(query.District, QueryPart.DistrictRole));
        if (!string.IsNullOrWhiteSpace(query.Community))
            contexts.Add(QueryPart.Parse(query.Community, QueryPart.CommunityRole));

        return Run(target, contexts, query.Categories, query.Within, query.Limit);
    }

    private List<KatottgSearchHit> Run(QueryPart target, IReadOnlyList<QueryPart> contexts,
        IReadOnlyCollection<KatottgCategory>? categories, KatottgUnit? within, int limit)
    {
        if (target.IsEmpty)
            return [];

        var scopes = new List<HashSet<KatottgUnit>>(contexts.Count);
        foreach (var context in contexts)
        {
            if (context.IsEmpty)
                continue;
            var scope = ResolveScope(context);
            if (scope.Count == 0)
                return [];
            scopes.Add(scope);
        }

        var matches = Match(target, includeWordPrefix: true);
        var hits = Collect(matches, Intersect(target.Categories, categories), within, scopes, limit);
        if (hits.Count == 0 && target.Categories is not null)
            hits = Collect(matches, categories, within, scopes, limit);
        return hits;
    }

    /// <summary>Об’єкти, всередині яких може лежати результат (для частини-контексту).</summary>
    private HashSet<KatottgUnit> ResolveScope(QueryPart context)
    {
        var matches = Match(context, includeWordPrefix: false);
        var scope = new HashSet<KatottgUnit>(ReferenceEqualityComparer.Instance);
        foreach (var (index, _) in matches)
        {
            var unit = _units[index];
            if (context.Categories is null || context.Categories.Contains(unit.Category))
                scope.Add(unit);
        }
        if (scope.Count == 0 && context.Categories is not null)
        {
            foreach (var (index, _) in matches)
                scope.Add(_units[index]);
        }
        return scope;
    }

    private Dictionary<int, (MatchKind Kind, double Score)> Match(QueryPart part, bool includeWordPrefix)
    {
        var results = new Dictionary<int, (MatchKind, double)>();
        NameIndex.Match(part.Text, includeWordPrefix, results);
        if (part.Alias is not null)
            NameIndex.Match(part.Alias, includeWordPrefix, results);
        return results;
    }

    private List<KatottgSearchHit> Collect(Dictionary<int, (MatchKind Kind, double Score)> matches,
        IReadOnlyCollection<KatottgCategory>? categories, KatottgUnit? within, List<HashSet<KatottgUnit>> scopes, int limit)
    {
        var hits = new List<KatottgSearchHit>();
        foreach (var (index, (kind, score)) in matches)
        {
            var unit = _units[index];
            if (!Accepts(unit, categories, within) || !InAllScopes(unit, scopes))
                continue;
            hits.Add(new KatottgSearchHit(unit, score + CategoryWeight(unit.Category), kind));
        }

        hits.Sort(static (a, b) =>
        {
            var c = b.Score.CompareTo(a.Score);
            if (c != 0) return c;
            c = CategoryWeight(b.Unit.Category).CompareTo(CategoryWeight(a.Unit.Category));
            if (c != 0) return c;
            c = a.Unit.Name.Length.CompareTo(b.Unit.Name.Length);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Unit.Name, b.Unit.Name);
            return c != 0 ? c : a.Unit.Code.CompareTo(b.Unit.Code);
        });

        if (hits.Count > limit)
            hits.RemoveRange(limit, hits.Count - limit);
        return hits;
    }

    private static bool Accepts(KatottgUnit unit, IReadOnlyCollection<KatottgCategory>? categories, KatottgUnit? within)
        => (categories is null || categories.Contains(unit.Category)) && (within is null || within.Contains(unit));

    private static bool InAllScopes(KatottgUnit unit, List<HashSet<KatottgUnit>> scopes)
    {
        foreach (var scope in scopes)
        {
            var inside = false;
            for (var a = unit.Parent; a is not null && !inside; a = a.Parent)
                inside = scope.Contains(a);
            if (!inside)
                return false;
        }
        return true;
    }

    private static IReadOnlyCollection<KatottgCategory>? Intersect(KatottgCategory[]? marker, IReadOnlyCollection<KatottgCategory>? options)
        => marker is null ? options
         : options is null ? marker
         : marker.Where(options.Contains).ToArray();

    /// <summary>Невелика надбавка за важливість категорії: місто вище за однойменне село.</summary>
    private static double CategoryWeight(KatottgCategory category) => category switch
    {
        KatottgCategory.SpecialStatusCity => 0.09,
        KatottgCategory.Region => 0.08,
        KatottgCategory.City => 0.07,
        KatottgCategory.District => 0.06,
        KatottgCategory.Community => 0.05,
        KatottgCategory.UrbanTypeSettlement => 0.04,
        KatottgCategory.Settlement => 0.03,
        KatottgCategory.Village => 0.02,
        KatottgCategory.CityDistrict => 0.01,
        _ => 0,
    };
}
