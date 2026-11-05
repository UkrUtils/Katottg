namespace UkrUtils.Katottg.Import;

/// <summary>How serious a problem found during import is.</summary>
public enum ImportSeverity
{
    /// <summary>Information: the data was fixed automatically (for example, whitespace was trimmed).</summary>
    Info,

    /// <summary>A suspicious place in the data; the record was imported.</summary>
    Warning,

    /// <summary>The record was not imported or the file could not be parsed.</summary>
    Error,
}

/// <summary>Kind of import problem.</summary>
public enum ImportIssueKind
{
    /// <summary>The header row with the columns "Перший рівень" (first level) and "Назва об’єкта" (object name) was not found.</summary>
    HeaderNotFound,

    /// <summary>Service row (classification stamp, title, note), skipped.</summary>
    SkippedRow,

    /// <summary>The level cell is not a <c>UA</c> + 17 digits code.</summary>
    InvalidCode,

    /// <summary>The code has already occurred earlier.</summary>
    DuplicateCode,

    /// <summary>The 12 significant digits of the code have already occurred in another code.</summary>
    DuplicateSignificantPart,

    /// <summary>The parent code did not appear earlier in the file.</summary>
    MissingParent,

    /// <summary>The non-zero segments of the parent's code do not match the child's code.</summary>
    PrefixMismatch,

    /// <summary>Unknown category letter.</summary>
    UnknownCategory,

    /// <summary>The category is not expected under a parent of this category.</summary>
    UnexpectedHierarchy,

    /// <summary>There is an empty level between filled levels.</summary>
    LevelGap,

    /// <summary>Empty name.</summary>
    EmptyName,

    /// <summary>A Cyrillic name contains Latin letters (for example, "Вiйтовецька").</summary>
    LatinCharactersInName,

    /// <summary>The name had surrounding whitespace; it was trimmed.</summary>
    NameHasSurroundingWhitespace,

    /// <summary>The edition date could not be determined from either the file name or the document properties.</summary>
    EditionDateUnknown,
}

/// <summary>One problem found during import.</summary>
/// <param name="Severity">Severity.</param>
/// <param name="Kind">Kind of problem.</param>
/// <param name="Row">Row number in the sheet (from 1); 0 — a problem with the whole file.</param>
/// <param name="Code">Object code, if known.</param>
/// <param name="Message">Human-readable description.</param>
public sealed record ImportIssue(ImportSeverity Severity, ImportIssueKind Kind, int Row, KatottgCode? Code, string Message)
{
    /// <inheritdoc/>
    public override string ToString() => Code is null
        ? $"[{Severity}] row {Row}: {Kind}: {Message}"
        : $"[{Severity}] row {Row} {Code}: {Kind}: {Message}";
}

/// <summary>Report on the import of the official file.</summary>
public sealed class ImportReport
{
    internal ImportReport(IReadOnlyList<ImportIssue> issues, int headerRow, int dataRows, int skippedRows, int emptyRows, IReadOnlyList<string> notes)
    {
        Issues = issues;
        HeaderRow = headerRow;
        DataRows = dataRows;
        SkippedRows = skippedRows;
        EmptyRows = emptyRows;
        Notes = notes;
    }

    /// <summary>All problems found, in row order.</summary>
    public IReadOnlyList<ImportIssue> Issues { get; }

    /// <summary>Header row number (from 1); 0 if the header was not found.</summary>
    public int HeaderRow { get; }

    /// <summary>Number of imported records.</summary>
    public int DataRows { get; }

    /// <summary>Number of non-empty service rows (classification stamp, title, note).</summary>
    public int SkippedRows { get; }

    /// <summary>Number of empty rows recorded in the sheet.</summary>
    public int EmptyRows { get; }

    /// <summary>Texts of the notes after the data (rows starting with "Примітка", i.e. "Note").</summary>
    public IReadOnlyList<string> Notes { get; }

    /// <summary>Whether there is at least one error.</summary>
    public bool HasErrors => Issues.Any(i => i.Severity == ImportSeverity.Error);

    /// <summary>Errors only.</summary>
    public IEnumerable<ImportIssue> Errors => Issues.Where(i => i.Severity == ImportSeverity.Error);

    /// <summary>Warnings only.</summary>
    public IEnumerable<ImportIssue> Warnings => Issues.Where(i => i.Severity == ImportSeverity.Warning);
}
