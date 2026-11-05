using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace UkrUtils.Katottg;

/// <summary>
/// Незмінний набір даних кодифікатора: усі об’єкти, ієрархія та індекси пошуку.
/// Безпечний для одночасного читання з багатьох потоків.
/// </summary>
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

    /// <summary>Відомості про редакцію кодифікатора.</summary>
    public KatottgEdition Edition { get; }

    /// <summary>Кількість об’єктів.</summary>
    public int Count => _units.Length;

    /// <summary>Корені: області, АР Крим і міста зі спеціальним статусом.</summary>
    public IReadOnlyList<KatottgUnit> Regions { get; }

    /// <summary>Усі об’єкти в порядку офіційного файлу (батьки раніше за дітей).</summary>
    public IReadOnlyList<KatottgUnit> All { get; }

    /// <summary>Об’єкт з точно таким кодом.</summary>
    public KatottgUnit? Find(KatottgCode code) => _byCode.TryGetValue(code.Value, out var i) ? _units[i] : null;

    /// <summary>Об’єкт, у якого 12 значущих цифр збігаються з <paramref name="code"/>; хвіст ігнорується.</summary>
    public KatottgUnit? FindBySignificantPart(KatottgCode code)
        => _bySignificantPart.TryGetValue(code.SignificantPart, out var i) ? _units[i] : null;

    /// <summary>Розбирає код і шукає об’єкт з точно таким кодом.</summary>
    public bool TryFind(ReadOnlySpan<char> code, [NotNullWhen(true)] out KatottgUnit? unit)
    {
        unit = KatottgCode.TryParse(code, out var parsed) ? Find(parsed) : null;
        return unit is not null;
    }

    /// <summary>Об’єкти заданої категорії в порядку файлу.</summary>
    public IReadOnlyList<KatottgUnit> GetByCategory(KatottgCategory category)
        => _byCategory.TryGetValue(category, out var list) ? list : ReadOnlyCollection<KatottgUnit>.Empty;

    /// <summary>
    /// Будує набір із пласких записів. Батько кожного запису має йти раніше за нього.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Дублікат коду або 12 значущих цифр, батька немає серед попередніх записів, порожня назва.
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

    /// <summary>Плаский список записів у порядку <see cref="All"/>.</summary>
    public IEnumerable<KatottgRecord> ToRecords()
        => _units.Select(u => new KatottgRecord(u.Code, u.Parent?.Code, u.Category, u.Name));
}
