namespace UkrUtils.Katottg;

/// <summary>Як саме назва збіглася із запитом. Чим менше значення, тим кращий збіг.</summary>
public enum MatchKind
{
    /// <summary>Запит — це код КАТОТТГ (можливо, з неправильним хвостом).</summary>
    Code,

    /// <summary>Нормалізована назва дорівнює запиту.</summary>
    Exact,

    /// <summary>Назва починається із запиту.</summary>
    Prefix,

    /// <summary>Одне зі слів назви (не перше) починається із запиту: «Подільський» → «Могилів-Подільський».</summary>
    WordPrefix,
}

/// <summary>Знайдений об’єкт.</summary>
/// <param name="Unit">Об’єкт кодифікатора.</param>
/// <param name="Score">Оцінка; більша — краща. Порівнювати варто лише в межах одного пошуку.</param>
/// <param name="Kind">Вид збігу.</param>
public sealed record KatottgSearchHit(KatottgUnit Unit, double Score, MatchKind Kind);

/// <summary>Результат пошуку за вільним рядком.</summary>
public sealed class KatottgSearchResult
{
    internal KatottgSearchResult(string text, IReadOnlyList<KatottgSearchHit> hits)
    {
        Text = text;
        Hits = hits;
    }

    /// <summary>Рядок запиту.</summary>
    public string Text { get; }

    /// <summary>Знайдені об’єкти від кращого до гіршого.</summary>
    public IReadOnlyList<KatottgSearchHit> Hits { get; }

    /// <summary>Найкращий збіг або <c>null</c>.</summary>
    public KatottgSearchHit? Best => Hits.Count > 0 ? Hits[0] : null;

    /// <summary>
    /// Результати у вигляді дерева: знайдені об’єкти (<see cref="KatottgTreeNode.IsMatch"/>) разом
    /// з областями, районами й громадами, у яких вони лежать.
    /// </summary>
    public IReadOnlyList<KatottgTreeNode> ToTree() => KatottgTreeNode.Build(Hits.Select(h => h.Unit));
}

/// <summary>Параметри пошуку за вільним рядком.</summary>
public sealed class KatottgSearchOptions
{
    /// <summary>Найбільша кількість результатів. За замовчуванням 20.</summary>
    public int Limit { get; init; } = 20;

    /// <summary>Шукати лише серед цих категорій. Поєднується з маркерами в запиті («с.», «м.»…).</summary>
    public IReadOnlyCollection<KatottgCategory>? Categories { get; init; }

    /// <summary>Шукати лише всередині цього об’єкта (включно з ним самим).</summary>
    public KatottgUnit? Within { get; init; }
}

/// <summary>Структурований запит: назва плюс необов’язковий контекст.</summary>
public sealed class KatottgQuery
{
    /// <summary>Назва об’єкта. Може містити маркер категорії: «с. Андріївка».</summary>
    public required string Name { get; init; }

    /// <summary>Назва області (або міста зі спецстатусом), у якій шукати.</summary>
    public string? Region { get; init; }

    /// <summary>Назва району, у якому шукати.</summary>
    public string? District { get; init; }

    /// <summary>Назва громади, у якій шукати.</summary>
    public string? Community { get; init; }

    /// <summary>Шукати лише серед цих категорій.</summary>
    public IReadOnlyCollection<KatottgCategory>? Categories { get; init; }

    /// <summary>Шукати лише всередині цього об’єкта (включно з ним самим).</summary>
    public KatottgUnit? Within { get; init; }

    /// <summary>Найбільша кількість результатів. За замовчуванням 20.</summary>
    public int Limit { get; init; } = 20;
}
