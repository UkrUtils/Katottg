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
    /// об’єкта з такою назвою. Контекст із маркером («Бердянський р-н») обов’язковий; частини без
    /// маркера, які нічого не звужують (вулиця, індекс, номер будинку), ігноруються. Кожну частину
    /// пробуємо як ціль; перемагає комбінація, що не відкидала маркер цілі, врахувала більше
    /// контекстів і дала найконкретніший об’єкт. Нечіткий пошук і транслітерація — у наступних етапах.
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
            return new KatottgSearchResult(text, hits.AsReadOnly());
        }

        var parts = text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(QueryPart.Parse)
            .Where(p => !p.IsEmpty)
            .ToList();

        // Кожну частину пробуємо як ціль, решта — контекст. Перемагає комбінація, яка
        // (1) не відкидала маркер цілі, (2) врахувала більше контекстів, (3) дала глибший об’єкт,
        // (4) має вищу оцінку. Рівність — на користь ранішої частини.
        RunResult? best = null;
        for (var i = 0; i < parts.Count; i++)
        {
            var contexts = parts.Where((_, j) => j != i).ToList();
            var run = Run(parts[i], contexts, options.Categories, options.Within, options.Limit);
            if (run.Hits.Count > 0 && (best is null || run.CompareTo(best.Value) > 0))
                best = run;
        }
        return new KatottgSearchResult(text, (best?.Hits ?? []).AsReadOnly());
    }

    private readonly record struct RunResult(List<KatottgSearchHit> Hits, bool UsedFallback, int AppliedContexts)
    {
        public int CompareTo(RunResult other)
        {
            var c = (!UsedFallback).CompareTo(!other.UsedFallback);
            if (c != 0) return c;
            c = AppliedContexts.CompareTo(other.AppliedContexts);
            if (c != 0) return c;
            c = Hits[0].Unit.Depth.CompareTo(other.Hits[0].Unit.Depth);
            return c != 0 ? c : Hits[0].Score.CompareTo(other.Hits[0].Score);
        }
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

        return Run(target, contexts, query.Categories, query.Within, query.Limit).Hits.AsReadOnly();
    }

    /// <summary>
    /// Шукає ціль з урахуванням контекстів. Контекст із маркером («р-н», «обл.») або з поля
    /// структурованого запиту обов’язковий: якщо його не знайдено, результатів немає. Контекст без
    /// маркера (вулиця, індекс, будинок, просто назва) застосовується, лише якщо після нього
    /// щось лишається.
    /// </summary>
    private RunResult Run(QueryPart target, IReadOnlyList<QueryPart> contexts,
        IReadOnlyCollection<KatottgCategory>? categories, KatottgUnit? within, int limit)
    {
        if (target.IsEmpty)
            return new RunResult([], false, 0);

        var required = new List<HashSet<KatottgUnit>>();
        var optional = new List<HashSet<KatottgUnit>>();
        foreach (var context in contexts)
        {
            if (context.IsEmpty)
                continue;
            var scope = ResolveScope(context);
            if (context.IsMarked)
            {
                if (scope.Count == 0)
                    return new RunResult([], false, 0);
                required.Add(scope);
            }
            else if (scope.Count > 0)
            {
                optional.Add(scope);
            }
        }

        var matches = Match(target, includeWordPrefix: true);
        var (hits, applied) = Collect(matches, Intersect(target.Categories, categories), target.PrimaryCategory, within, required, optional, limit);
        var usedFallback = false;
        if (hits.Count == 0 && target.Categories is not null)
        {
            (hits, applied) = Collect(matches, categories, null, within, required, optional, limit);
            usedFallback = true;
        }
        return new RunResult(hits, usedFallback, applied);
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

    /// <summary>Надбавка за основну категорію маркера; більша за різницю ваг сусідніх категорій.</summary>
    private const double PrimaryCategoryBonus = 0.015;

    private (List<KatottgSearchHit> Hits, int AppliedContexts) Collect(Dictionary<int, (MatchKind Kind, double Score)> matches,
        IReadOnlyCollection<KatottgCategory>? categories, KatottgCategory? primary, KatottgUnit? within,
        List<HashSet<KatottgUnit>> required, List<HashSet<KatottgUnit>> optional, int limit)
    {
        var candidates = new List<(KatottgUnit Unit, MatchKind Kind, double Score)>();
        foreach (var (index, (kind, score)) in matches)
        {
            var unit = _units[index];
            if (Accepts(unit, categories, within) && required.TrueForAll(scope => IsInside(unit, scope)))
                candidates.Add((unit, kind, score));
        }

        var applied = required.Count;
        foreach (var scope in optional)
        {
            var narrowed = candidates.FindAll(c => IsInside(c.Unit, scope));
            if (narrowed.Count > 0)
            {
                candidates = narrowed;
                applied++;
            }
        }

        var hits = new List<KatottgSearchHit>(candidates.Count);
        foreach (var (unit, kind, score) in candidates)
        {
            var bonus = unit.Category == primary ? PrimaryCategoryBonus : 0;
            hits.Add(new KatottgSearchHit(unit, score + CategoryWeight(unit.Category) + bonus, kind));
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
        return (hits, applied);
    }

    private static bool Accepts(KatottgUnit unit, IReadOnlyCollection<KatottgCategory>? categories, KatottgUnit? within)
        => (categories is null || categories.Contains(unit.Category)) && (within is null || within.Contains(unit));

    /// <summary>Чи є серед строгих предків <paramref name="unit"/> хоч один об’єкт зі <paramref name="scope"/>.</summary>
    private static bool IsInside(KatottgUnit unit, HashSet<KatottgUnit> scope)
    {
        for (var a = unit.Parent; a is not null; a = a.Parent)
        {
            if (scope.Contains(a))
                return true;
        }
        return false;
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
