using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UkrUtils.Katottg.Import.Xlsx;

namespace UkrUtils.Katottg.Import;

/// <summary>
/// Розбирає офіційний xlsx-файл кодифікатора (аркуш із колонками рівнів, категорії й назви).
/// </summary>
/// <remarks>
/// Шапку шукає за текстом, колонки визначає за шапкою. Рядок даних — той, де в колонці першого
/// рівня стоїть код. Код об’єкта — останній заповнений рівень, батько — останній відмінний від
/// нього код ліворуч (так обробляються повтори кодів у Києві, Севастополі, Прип’яті).
/// </remarks>
public static partial class KatottgSheetParser
{
    /// <summary>Розбирає файл за шляхом. Дату редакції бере з імені файлу.</summary>
    public static KatottgImportResult Parse(string path, KatottgImportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var stream = File.OpenRead(path);
        return Parse(stream, path, options);
    }

    /// <summary>Розбирає xlsx із потоку.</summary>
    /// <param name="xlsx">Вміст файлу. Читається повністю, щоб порахувати SHA-256.</param>
    /// <param name="fileName">Ім’я або шлях файлу — для дати редакції та <see cref="KatottgEdition.Source"/>.</param>
    /// <param name="options">Параметри імпорту.</param>
    /// <exception cref="InvalidDataException">Потік не є коректним xlsx.</exception>
    public static KatottgImportResult Parse(Stream xlsx, string? fileName = null, KatottgImportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(xlsx);
        options ??= new KatottgImportOptions();

        byte[] bytes;
        using (var buffer = new MemoryStream())
        {
            xlsx.CopyTo(buffer);
            bytes = buffer.ToArray();
        }
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));

        using var reader = new MinimalXlsxReader(new MemoryStream(bytes, writable: false));
        var state = new SheetState();
        string? coreModified;
        try
        {
            foreach (var row in reader.ReadFirstSheet())
                state.Process(row);
            coreModified = reader.ReadCoreModified();
        }
        catch (System.Xml.XmlException ex)
        {
            throw new InvalidDataException($"Malformed XML in xlsx: {ex.Message}", ex);
        }
        state.Finish();

        var name = FileNameOf(fileName);
        var date = options.EditionDate ?? DateFromFileName(name) ?? DateFromCoreProperties(coreModified);
        if (date is null)
            state.Issues.Add(new ImportIssue(ImportSeverity.Warning, ImportIssueKind.EditionDateUnknown, 0, null,
                "Edition date not found in file name (katottg-YY-MM-DD) nor in docProps/core.xml."));

        var edition = new KatottgEdition(date ?? default, options.Source ?? name, sha256);
        var report = new ImportReport(state.Issues, state.HeaderRow, state.Records.Count, state.SkippedRows, state.EmptyRows, state.Notes);
        return new KatottgImportResult(edition, state.Records, report);
    }

    private static string? FileNameOf(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        var cut = path.LastIndexOfAny(['/', '\\']);
        return cut < 0 ? path : path[(cut + 1)..];
    }

    [GeneratedRegex(@"katottg[-_ ]?(\d{4}|\d{2})[-_.](\d{2})[-_.](\d{2})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FileNameDateRegex();

    internal static DateOnly? DateFromFileName(string? fileName)
    {
        if (fileName is null)
            return null;
        var match = FileNameDateRegex().Match(fileName);
        if (!match.Success)
            return null;
        var year = int.Parse(match.Groups[1].ValueSpan, CultureInfo.InvariantCulture);
        if (year < 100)
            year += 2000;
        var month = int.Parse(match.Groups[2].ValueSpan, CultureInfo.InvariantCulture);
        var day = int.Parse(match.Groups[3].ValueSpan, CultureInfo.InvariantCulture);
        return month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month)
            ? new DateOnly(year, month, day)
            : null;
    }

    private static DateOnly? DateFromCoreProperties(string? modified)
        => DateTimeOffset.TryParse(modified, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value)
            ? DateOnly.FromDateTime(value.UtcDateTime)
            : null;

    private sealed class SheetState
    {
        private readonly Dictionary<KatottgCode, KatottgCategory> _known = [];
        private readonly HashSet<ulong> _significant = [];
        private readonly List<KatottgCode> _levels = new(5);

        private int[] _levelColumns = [];
        private int _categoryColumn = -1;
        private int _nameColumn = -1;

        public List<KatottgRecord> Records { get; } = [];
        public List<ImportIssue> Issues { get; } = [];
        public List<string> Notes { get; } = [];
        public int HeaderRow { get; private set; }
        public int SkippedRows { get; private set; }
        public int EmptyRows { get; private set; }

        public void Process(XlsxRow row)
        {
            if (row.IsBlank)
            {
                EmptyRows++;
                return;
            }

            if (HeaderRow == 0)
            {
                if (!TryReadHeader(row))
                    Skip(row);
                return;
            }

            // Рядок даних — будь-який рядок, де хоч одна колонка рівня схожа на код. Так рядок
            // із пошкодженим першим рівнем дає помилку, а не тихо пропадає як службовий.
            if (!_levelColumns.Any(c => LooksLikeCode(row[c])))
            {
                Skip(row);
                return;
            }

            ProcessData(row);
        }

        private static bool LooksLikeCode(string? cell)
        {
            var text = cell.AsSpan().Trim();
            return text.Length >= 2 && (KatottgCode.TryParse(text, out _) ||
                (text[0] is 'U' or 'u' or 'У' or 'у' && text[1] is 'A' or 'a' or 'А' or 'а'));
        }

        public void Finish()
        {
            if (HeaderRow == 0)
                Issues.Add(new ImportIssue(ImportSeverity.Error, ImportIssueKind.HeaderNotFound, 0, null,
                    "Header row with 'Перший рівень' and 'Назва об’єкта' columns not found."));
        }

        private void Skip(XlsxRow row)
        {
            SkippedRows++;
            var text = string.Join(" | ", row.Cells.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!.Trim()));
            if (text.StartsWith("Примітка", StringComparison.OrdinalIgnoreCase))
                Notes.Add(text);
            var preview = text.Length > 80 ? text[..80] + "…" : text;
            Issues.Add(new ImportIssue(ImportSeverity.Info, ImportIssueKind.SkippedRow, row.Number, null, preview.ReplaceLineEndings(" ")));
        }

        private bool TryReadHeader(XlsxRow row)
        {
            var levels = new SortedDictionary<int, int>();
            int category = -1, name = -1;
            for (var col = 0; col < row.Cells.Count; col++)
            {
                var label = NormalizeLabel(row.Cells[col]);
                if (label.Length == 0)
                    continue;
                var level = label switch
                {
                    "перший рівень" => 0,
                    "другий рівень" => 1,
                    "третій рівень" => 2,
                    "четвертий рівень" => 3,
                    "додатковий рівень" or "п’ятий рівень" => 4,
                    _ => -1,
                };
                if (level >= 0)
                    levels.TryAdd(level, col);
                else if (category < 0 && label.StartsWith("категорія", StringComparison.Ordinal))
                    category = col;
                else if (name < 0 && label.StartsWith("назва", StringComparison.Ordinal))
                    name = col;
            }

            if (!levels.ContainsKey(0) || category < 0 || name < 0)
                return false;

            _levelColumns = [.. levels.Values];
            _categoryColumn = category;
            _nameColumn = name;
            HeaderRow = row.Number;
            return true;
        }

        private static string NormalizeLabel(string? cell)
        {
            if (string.IsNullOrWhiteSpace(cell))
                return "";
            var words = cell.Trim().ToLowerInvariant()
                .Replace('\'', '’').Replace('ʼ', '’').Replace('`', '’')
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(' ', words);
        }

        private void ProcessData(XlsxRow row)
        {
            if (string.IsNullOrWhiteSpace(row[_levelColumns[0]]))
            {
                Error(row, ImportIssueKind.LevelGap, null, "First level is empty, but other levels contain codes.");
                return;
            }

            _levels.Clear();
            bool seenEmpty = false, gap = false;
            foreach (var column in _levelColumns)
            {
                var cell = row[column]?.Trim();
                if (string.IsNullOrEmpty(cell))
                {
                    seenEmpty = true;
                    continue;
                }
                if (!KatottgCode.TryParse(cell, out var level))
                {
                    Error(row, ImportIssueKind.InvalidCode, null, $"'{cell}' is not a valid code.");
                    return;
                }
                gap |= seenEmpty;
                _levels.Add(level);
            }

            var code = _levels[^1];
            KatottgCode? parent = null;
            for (var i = _levels.Count - 2; i >= 0; i--)
            {
                if (_levels[i] != code)
                {
                    parent = _levels[i];
                    break;
                }
            }

            var rawName = row[_nameColumn] ?? "";
            var name = rawName.Trim();
            if (name.Length == 0)
            {
                Error(row, ImportIssueKind.EmptyName, code, "Name is empty.");
                return;
            }

            var letter = row[_categoryColumn]?.Trim() ?? "";
            var category = letter.Length == 1 ? KatottgCategoryExtensions.FromLetter(letter[0]) : KatottgCategory.Unknown;

            if (_known.ContainsKey(code))
            {
                Error(row, ImportIssueKind.DuplicateCode, code, "Code already seen.");
                return;
            }
            if (_significant.Contains(code.SignificantPart))
            {
                Error(row, ImportIssueKind.DuplicateSignificantPart, code, "First 12 digits already used by another code.");
                return;
            }

            var parentCategory = KatottgCategory.Unknown;
            if (parent is { } p && !_known.TryGetValue(p, out parentCategory))
            {
                Error(row, ImportIssueKind.MissingParent, code, $"Parent {p} does not appear earlier in the file.");
                return;
            }

            if (gap)
                Warn(row, ImportIssueKind.LevelGap, code, "Empty level between filled levels.");
            if (category == KatottgCategory.Unknown)
                Warn(row, ImportIssueKind.UnknownCategory, code, $"Unknown category '{letter}'.");
            if (!ReferenceEquals(rawName, name) && rawName != name)
                Issues.Add(new ImportIssue(ImportSeverity.Info, ImportIssueKind.NameHasSurroundingWhitespace, row.Number, code, $"Name '{rawName}' trimmed."));
            if (HasMixedScript(name))
                Warn(row, ImportIssueKind.LatinCharactersInName, code, $"Name '{name}' mixes Cyrillic and Latin letters.");
            if (parent is { } pp)
            {
                if (!IsPrefixConsistent(pp, code))
                    Warn(row, ImportIssueKind.PrefixMismatch, code, $"Code is not nested in parent {pp}.");
                if (category != KatottgCategory.Unknown && parentCategory != KatottgCategory.Unknown && !IsExpectedChild(parentCategory, category))
                    Warn(row, ImportIssueKind.UnexpectedHierarchy, code, $"Category {category} under {parentCategory} is unexpected.");
            }
            else if (category != KatottgCategory.Unknown && category is not (KatottgCategory.Region or KatottgCategory.SpecialStatusCity))
            {
                Warn(row, ImportIssueKind.UnexpectedHierarchy, code, $"Category {category} at the root is unexpected.");
            }

            _known.Add(code, category);
            _significant.Add(code.SignificantPart);
            Records.Add(new KatottgRecord(code, parent, category, name));
        }

        private void Error(XlsxRow row, ImportIssueKind kind, KatottgCode? code, string message)
            => Issues.Add(new ImportIssue(ImportSeverity.Error, kind, row.Number, code, message));

        private void Warn(XlsxRow row, ImportIssueKind kind, KatottgCode? code, string message)
            => Issues.Add(new ImportIssue(ImportSeverity.Warning, kind, row.Number, code, message));
    }

    /// <summary>Усі ненульові сегменти батька збігаються з відповідними сегментами дитини.</summary>
    internal static bool IsPrefixConsistent(KatottgCode parent, KatottgCode child)
        => Matches(parent.Region, child.Region) && Matches(parent.District, child.District) &&
           Matches(parent.Community, child.Community) && Matches(parent.Settlement, child.Settlement) &&
           Matches(parent.CityDistrict, child.CityDistrict);

    private static bool Matches(int parentSegment, int childSegment) => parentSegment == 0 || parentSegment == childSegment;

    /// <summary>Допустимі пари «батько → дитина», включно з відомими винятками (Прип’ять, райони у містах).</summary>
    internal static bool IsExpectedChild(KatottgCategory parent, KatottgCategory child) => (parent, child) switch
    {
        (KatottgCategory.Region, KatottgCategory.District or KatottgCategory.City) => true,
        (KatottgCategory.District, KatottgCategory.Community) => true,
        (KatottgCategory.Community, KatottgCategory.City or KatottgCategory.UrbanTypeSettlement or KatottgCategory.Settlement or KatottgCategory.Village) => true,
        (KatottgCategory.City or KatottgCategory.SpecialStatusCity, KatottgCategory.CityDistrict) => true,
        _ => false,
    };

    private static bool HasMixedScript(string name)
    {
        bool latin = false, cyrillic = false;
        foreach (var c in name)
        {
            if (c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z'))
                latin = true;
            else if (c is >= 'Ѐ' and <= 'ӿ')
                cyrillic = true;
        }
        return latin && cyrillic;
    }
}
