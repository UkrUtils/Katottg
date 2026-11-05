using System.Text;

namespace UkrUtils.Katottg;

/// <summary>
/// Codifier object: region, district, community, populated place, or city district.
/// Immutable; references to the parent and children are built once when the dataset is loaded.
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
        CityDistrict = category == KatottgCategory.CityDistrict ? this : null;
    }

    /// <summary>Position in <see cref="KatottgDataset.All"/>.</summary>
    internal int Index { get; }

    /// <summary>Object code.</summary>
    public KatottgCode Code { get; }

    /// <summary>Object category.</summary>
    public KatottgCategory Category { get; }

    /// <summary>Name as in the official file (without surrounding whitespace).</summary>
    public string Name { get; }

    /// <summary>Parent object; <c>null</c> for regions and cities with special status.</summary>
    public KatottgUnit? Parent { get; }

    /// <summary>Child objects in the order of the official file.</summary>
    public IReadOnlyList<KatottgUnit> Children { get; private set; } = s_noChildren;

    /// <summary>Actual depth in the tree: 0 — a region or a city with special status.</summary>
    public int Depth { get; }

    /// <summary>Root of the branch: region (<c>O</c>) or city with special status (<c>K</c>). For a root, itself.</summary>
    public KatottgUnit Region { get; }

    /// <summary>District (<c>P</c>) the object belongs to (or itself); <c>null</c> if there is no district (Kyiv, Prypiat).</summary>
    public KatottgUnit? District { get; }

    /// <summary>Community (<c>H</c>) the object belongs to (or itself); <c>null</c> if there is no community.</summary>
    public KatottgUnit? Community { get; }

    /// <summary>
    /// Populated place (<c>M</c>, <c>T</c>, <c>X</c>, <c>C</c>, <c>K</c>): the object itself, and for a city district, the city.
    /// <c>null</c> for regions, districts, and communities.
    /// </summary>
    public KatottgUnit? PopulatedPlace { get; }

    /// <summary>City district (<c>B</c>): the object itself if it is a city district; otherwise <c>null</c>.</summary>
    public KatottgUnit? CityDistrict { get; }

    /// <summary>Path from the root (region) to the object itself, inclusive. A new array on every access.</summary>
    public IReadOnlyList<KatottgUnit> Path
    {
        get
        {
            var path = new KatottgUnit[Depth + 1];
            var unit = this;
            for (var i = Depth; i >= 0; i--, unit = unit.Parent!)
                path[i] = unit;
            return path.AsReadOnly();
        }
    }

    /// <summary>
    /// Flat address with the codes of all levels: region, district, community, populated place, city district.
    /// It does not reference the dataset, so it is convenient to serialize or return from an API.
    /// </summary>
    public KatottgAddress ToAddress() => new()
    {
        Code = Code,
        Category = Category,
        Name = Name,
        FullName = FullName,
        IsTemporarilyNotInForce = IsTemporarilyNotInForce,
        Region = KatottgAddressPart.From(Region),
        District = District is null ? null : KatottgAddressPart.From(District),
        Community = Community is null ? null : KatottgAddressPart.From(Community),
        PopulatedPlace = PopulatedPlace is null ? null : KatottgAddressPart.From(PopulatedPlace),
        CityDistrict = CityDistrict is null ? null : KatottgAddressPart.From(CityDistrict),
    };

    /// <summary>
    /// The codes of AR Crimea and Sevastopol take effect from the moment these territories return under
    /// the jurisdiction of Ukraine (a note to the codifier).
    /// </summary>
    public bool IsTemporarilyNotInForce => Region.Code.Region is CrimeaRegion or SevastopolRegion;

    private const int CrimeaRegion = 1;
    private const int SevastopolRegion = 85;

    /// <summary>Name with the category abbreviation: "с. Андріївка", "Бахчисарайський р-н", "Харківська обл.".</summary>
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
    /// Full address from the object up to the root: "с. Андріївка, Андріївська громада, Бахчисарайський р-н, АР Крим".
    /// Computed on every access.
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

    /// <summary>Ancestors from the parent up to the root.</summary>
    public IEnumerable<KatottgUnit> Ancestors()
    {
        for (var unit = Parent; unit is not null; unit = unit.Parent)
            yield return unit;
    }

    /// <summary>All descendants, depth-first in file order (without the object itself).</summary>
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

    /// <summary>Whether this object is an ancestor of <paramref name="unit"/> (or the same object).</summary>
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
