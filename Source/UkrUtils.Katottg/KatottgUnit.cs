using System.Text;

namespace UkrUtils.Katottg;

/// <summary>
/// Об’єкт кодифікатора: область, район, громада, населений пункт або район у місті.
/// Незмінний; посилання на батька й дітей будуються один раз під час завантаження набору.
/// </summary>
public sealed class KatottgUnit
{
    private static readonly IReadOnlyList<KatottgUnit> s_noChildren = Array.Empty<KatottgUnit>().AsReadOnly();

    internal KatottgUnit(int index, KatottgCode code, KatottgCategory category, string name, KatottgUnit? parent)
    {
        Index = index;
        Code = code;
        Category = category;
        Name = name;
        Parent = parent;
        Depth = parent is null ? 0 : parent.Depth + 1;
        Region = parent?.Region ?? this;
        District = category == KatottgCategory.District ? this : parent?.District;
        Community = category == KatottgCategory.Community ? this : parent?.Community;
        PopulatedPlace = category.IsPopulatedPlace() ? this : parent?.PopulatedPlace;
    }

    /// <summary>Позиція в <see cref="KatottgDataset.All"/>.</summary>
    internal int Index { get; }

    /// <summary>Код об’єкта.</summary>
    public KatottgCode Code { get; }

    /// <summary>Категорія об’єкта.</summary>
    public KatottgCategory Category { get; }

    /// <summary>Назва, як в офіційному файлі (без пробілів по краях).</summary>
    public string Name { get; }

    /// <summary>Батьківський об’єкт; <c>null</c> для областей і міст зі спеціальним статусом.</summary>
    public KatottgUnit? Parent { get; }

    /// <summary>Дочірні об’єкти в порядку офіційного файлу.</summary>
    public IReadOnlyList<KatottgUnit> Children { get; private set; } = s_noChildren;

    /// <summary>Фактична глибина в дереві: 0 — область або місто зі спецстатусом.</summary>
    public int Depth { get; }

    /// <summary>Корінь гілки: область (<c>O</c>) або місто зі спеціальним статусом (<c>K</c>). Для кореня — він сам.</summary>
    public KatottgUnit Region { get; }

    /// <summary>Район (<c>P</c>), до якого належить об’єкт (або він сам); <c>null</c>, якщо району немає (Київ, Прип’ять).</summary>
    public KatottgUnit? District { get; }

    /// <summary>Громада (<c>H</c>), до якої належить об’єкт (або він сам); <c>null</c>, якщо громади немає.</summary>
    public KatottgUnit? Community { get; }

    /// <summary>
    /// Населений пункт (<c>M</c>, <c>T</c>, <c>X</c>, <c>C</c>, <c>K</c>): сам об’єкт, а для району в місті — місто.
    /// <c>null</c> для областей, районів і громад.
    /// </summary>
    public KatottgUnit? PopulatedPlace { get; }

    /// <summary>
    /// Коди АР Крим і Севастополя набирають чинності з моменту повернення цих територій під
    /// юрисдикцію України (примітка до кодифікатора).
    /// </summary>
    public bool IsTemporarilyNotInForce => Region.Code.Region is CrimeaRegion or SevastopolRegion;

    private const int CrimeaRegion = 1;
    private const int SevastopolRegion = 85;

    /// <summary>Назва зі скороченням категорії: «с. Андріївка», «Бахчисарайський р-н», «Харківська обл.».</summary>
    public string DisplayName
    {
        get
        {
            if (Category == KatottgCategory.Region && Code.Region == CrimeaRegion)
                return "АР Крим";
            var prefix = Category.ToShortPrefix();
            var suffix = Category.ToShortSuffix();
            return (prefix.Length, suffix.Length) switch
            {
                (0, 0) => Name,
                (_, 0) => $"{prefix} {Name}",
                (0, _) => $"{Name} {suffix}",
                _ => $"{prefix} {Name} {suffix}",
            };
        }
    }

    /// <summary>
    /// Повна адреса від об’єкта до кореня: «с. Андріївка, Андріївська громада, Бахчисарайський р-н, АР Крим».
    /// Обчислюється при кожному зверненні.
    /// </summary>
    public string FullName
    {
        get
        {
            var sb = new StringBuilder(DisplayName);
            for (var unit = Parent; unit is not null; unit = unit.Parent)
                sb.Append(", ").Append(unit.DisplayName);
            return sb.ToString();
        }
    }

    /// <summary>Предки від батька до кореня.</summary>
    public IEnumerable<KatottgUnit> Ancestors()
    {
        for (var unit = Parent; unit is not null; unit = unit.Parent)
            yield return unit;
    }

    /// <summary>Усі нащадки, обхід у глибину в порядку файлу (без самого об’єкта).</summary>
    public IEnumerable<KatottgUnit> Descendants()
    {
        if (Children.Count == 0)
            yield break;

        var stack = new Stack<(KatottgUnit Unit, int Next)>();
        stack.Push((this, 0));
        while (stack.Count > 0)
        {
            var (unit, next) = stack.Pop();
            if (next >= unit.Children.Count)
                continue;
            stack.Push((unit, next + 1));
            var child = unit.Children[next];
            yield return child;
            if (child.Children.Count > 0)
                stack.Push((child, 0));
        }
    }

    /// <summary>Чи є цей об’єкт предком <paramref name="unit"/> (або ним самим).</summary>
    public bool Contains(KatottgUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        for (var u = unit; u is not null; u = u.Parent)
        {
            if (ReferenceEquals(u, this))
                return true;
        }
        return false;
    }

    internal void SetChildren(KatottgUnit[] children) => Children = children.AsReadOnly();

    /// <inheritdoc/>
    public override string ToString() => $"{DisplayName} ({Code})";
}
