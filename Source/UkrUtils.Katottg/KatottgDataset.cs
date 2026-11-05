using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace UkrUtils.Katottg;

/// <summary>
/// Immutable codifier dataset: all objects, the hierarchy, and the search indexes.
/// Safe for concurrent reads from many threads. Start with <see cref="Embedded"/>.
/// </summary>
/// <remarks>
/// Main rules for working with KATOTTG:
/// <list type="bullet">
/// <item>The key is the <see cref="KatottgCode"/>, never the name: names repeat even within one community.
/// When showing a unit to a person, use <see cref="KatottgUnit.DisplayName"/> or <see cref="KatottgUnit.FullName"/>.</item>
/// <item>In a database store only the lowest-level code; get the parents from <see cref="Find"/> or
/// <see cref="KatottgUnit.ToAddress"/>, and in SQL filter by <see cref="KatottgCode.HierarchyPrefix"/>.</item>
/// <item>Pass a free-text address to <see cref="Search(string, KatottgSearchOptions?)"/>
/// as is; do not parse it yourself.</item>
/// <item>Hierarchy levels may be missing (Kyiv, Sevastopol, Prypiat): check for <c>null</c>.</item>
/// </list>
/// Guide for AI agents: <c>AgentGuide.md</c> in the package root or
/// https://github.com/UkrUtils/Katottg/blob/main/Docs/AgentGuide.md
/// </remarks>
public sealed partial class KatottgDataset
{
    private readonly KatottgUnit[] _units;
    private readonly Dictionary<ulong, int> _byCode;
    private readonly Dictionary<ulong, int> _bySignificantPart;
    private readonly Dictionary<KatottgCategory, ReadOnlyCollection<KatottgUnit>> _byCategory;

    private KatottgDataset(KatottgEdition edition, KatottgUnit[] units, Dictionary<ulong, int> byCode, Dictionary<ulong, int> bySignificantPart)
    {
        Edition = edition;
        _units = units;
        _byCode = byCode;
        _bySignificantPart = bySignificantPart;
        All = units.AsReadOnly();
        Regions = units.Where(u => u.Parent is null).ToArray().AsReadOnly();
        _byCategory = units.GroupBy(u => u.Category).ToDictionary(g => g.Key, g => g.ToArray().AsReadOnly());
    }

    /// <summary>Information about the codifier edition.</summary>
    public KatottgEdition Edition { get; }

    /// <summary>Number of objects.</summary>
    public int Count => _units.Length;

    /// <summary>Roots: regions, AR Crimea, and cities with special status.</summary>
    public IReadOnlyList<KatottgUnit> Regions { get; }

    /// <summary>All objects in the order of the official file (parents before children).</summary>
    public IReadOnlyList<KatottgUnit> All { get; }

    /// <summary>The object with exactly this code.</summary>
    public KatottgUnit? Find(KatottgCode code) => _byCode.TryGetValue(code.Value, out var i) ? _units[i] : null;

    /// <summary>The object whose 12 significant digits match <paramref name="code"/>; the tail is ignored.</summary>
    public KatottgUnit? FindBySignificantPart(KatottgCode code)
        => _bySignificantPart.TryGetValue(code.SignificantPart, out var i) ? _units[i] : null;

    /// <summary>Parses a code and looks up the object with exactly this code.</summary>
    public bool TryFind(ReadOnlySpan<char> code, [NotNullWhen(true)] out KatottgUnit? unit)
    {
        unit = KatottgCode.TryParse(code, out var parsed) ? Find(parsed) : null;
        return unit is not null;
    }

    /// <summary>Objects of the given category in file order.</summary>
    public IReadOnlyList<KatottgUnit> GetByCategory(KatottgCategory category)
        => _byCategory.TryGetValue(category, out var list) ? list : ReadOnlyCollection<KatottgUnit>.Empty;

    /// <summary>
    /// Builds a dataset from flat records. The parent of each record must come before it.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Duplicate code or 12 significant digits, the parent is not among the preceding records, or an empty name.
    /// </exception>
    public static KatottgDataset Create(KatottgEdition edition, IEnumerable<KatottgRecord> records)
    {
        ArgumentNullException.ThrowIfNull(edition);
        ArgumentNullException.ThrowIfNull(records);

        var list = records as IReadOnlyList<KatottgRecord> ?? records.ToList();
        var units = new KatottgUnit[list.Count];
        var byCode = new Dictionary<ulong, int>(list.Count);
        var bySignificant = new Dictionary<ulong, int>(list.Count);
        var childCounts = new int[list.Count];

        for (var i = 0; i < list.Count; i++)
        {
            var record = list[i];
            if (string.IsNullOrWhiteSpace(record.Name))
                throw new ArgumentException($"Record {record.Code} has an empty name.", nameof(records));
            if (!byCode.TryAdd(record.Code.Value, i))
                throw new ArgumentException($"Duplicate code {record.Code}.", nameof(records));
            if (!bySignificant.TryAdd(record.Code.SignificantPart, i))
                throw new ArgumentException($"Code {record.Code} has the same first 12 digits as {units[bySignificant[record.Code.SignificantPart]].Code}.", nameof(records));

            KatottgUnit? parent = null;
            if (record.ParentCode is { } parentCode)
            {
                if (!byCode.TryGetValue(parentCode.Value, out var parentIndex) || parentIndex == i)
                    throw new ArgumentException($"Parent {parentCode} of {record.Code} must appear earlier.", nameof(records));
                parent = units[parentIndex];
                childCounts[parentIndex]++;
            }
            units[i] = new KatottgUnit(i, record.Code, record.Category, record.Name, parent);
        }

        var children = new KatottgUnit[list.Count][];
        var filled = new int[list.Count];
        for (var i = 0; i < units.Length; i++)
        {
            if (childCounts[i] > 0)
                children[i] = new KatottgUnit[childCounts[i]];
        }
        foreach (var unit in units)
        {
            if (unit.Parent is { } parent)
                children[parent.Index][filled[parent.Index]++] = unit;
        }
        for (var i = 0; i < units.Length; i++)
        {
            if (children[i] is { } c)
                units[i].SetChildren(c);
        }

        return new KatottgDataset(edition, units, byCode, bySignificant);
    }

    /// <summary>Flat list of records in the <see cref="All"/> order.</summary>
    public IEnumerable<KatottgRecord> ToRecords()
        => _units.Select(u => new KatottgRecord(u.Code, u.Parent?.Code, u.Category, u.Name));
}
