namespace UkrUtils.Katottg.Import;

/// <summary>Наскільки серйозна проблема, знайдена під час імпорту.</summary>
public enum ImportSeverity
{
    /// <summary>Інформація: дані виправлено автоматично (наприклад, обрізано пробіли).</summary>
    Info,

    /// <summary>Підозріле місце в даних; запис імпортовано.</summary>
    Warning,

    /// <summary>Запис не імпортовано або файл не вдалося розібрати.</summary>
    Error,
}

/// <summary>Вид проблеми імпорту.</summary>
public enum ImportIssueKind
{
    /// <summary>Не знайдено рядок шапки з колонками «Перший рівень» і «Назва об’єкта».</summary>
    HeaderNotFound,

    /// <summary>Службовий рядок (гриф, заголовок, примітка), пропущено.</summary>
    SkippedRow,

    /// <summary>Комірка рівня не є кодом <c>UA</c> + 17 цифр.</summary>
    InvalidCode,

    /// <summary>Код уже зустрічався раніше.</summary>
    DuplicateCode,

    /// <summary>12 значущих цифр коду вже зустрічалися в іншому коді.</summary>
    DuplicateSignificantPart,

    /// <summary>Батьківський код не з’явився раніше у файлі.</summary>
    MissingParent,

    /// <summary>Ненульові сегменти коду батька не збігаються з кодом дитини.</summary>
    PrefixMismatch,

    /// <summary>Невідома літера категорії.</summary>
    UnknownCategory,

    /// <summary>Категорія не очікувана під батьком такої категорії.</summary>
    UnexpectedHierarchy,

    /// <summary>Між заповненими рівнями є порожній.</summary>
    LevelGap,

    /// <summary>Порожня назва.</summary>
    EmptyName,

    /// <summary>У кириличній назві є латинські літери (наприклад, «Вiйтовецька»).</summary>
    LatinCharactersInName,

    /// <summary>Назва мала пробіли по краях; їх обрізано.</summary>
    NameHasSurroundingWhitespace,

    /// <summary>Дату редакції не вдалося визначити ні з імені файлу, ні з властивостей документа.</summary>
    EditionDateUnknown,
}

/// <summary>Одна проблема, знайдена під час імпорту.</summary>
/// <param name="Severity">Серйозність.</param>
/// <param name="Kind">Вид проблеми.</param>
/// <param name="Row">Номер рядка в аркуші (з 1); 0 — проблема всього файлу.</param>
/// <param name="Code">Код об’єкта, якщо відомий.</param>
/// <param name="Message">Опис для людини.</param>
public sealed record ImportIssue(ImportSeverity Severity, ImportIssueKind Kind, int Row, KatottgCode? Code, string Message)
{
    /// <inheritdoc/>
    public override string ToString() => Code is null
        ? $"[{Severity}] row {Row}: {Kind}: {Message}"
        : $"[{Severity}] row {Row} {Code}: {Kind}: {Message}";
}

/// <summary>Звіт про імпорт офіційного файлу.</summary>
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

    /// <summary>Усі знайдені проблеми в порядку рядків.</summary>
    public IReadOnlyList<ImportIssue> Issues { get; }

    /// <summary>Номер рядка шапки (з 1); 0, якщо шапку не знайдено.</summary>
    public int HeaderRow { get; }

    /// <summary>Кількість імпортованих записів.</summary>
    public int DataRows { get; }

    /// <summary>Кількість непорожніх службових рядків (гриф, заголовок, примітка).</summary>
    public int SkippedRows { get; }

    /// <summary>Кількість порожніх рядків, записаних в аркуші.</summary>
    public int EmptyRows { get; }

    /// <summary>Тексти приміток після даних (рядки, що починаються з «Примітка»).</summary>
    public IReadOnlyList<string> Notes { get; }

    /// <summary>Чи є хоч одна помилка.</summary>
    public bool HasErrors => Issues.Any(i => i.Severity == ImportSeverity.Error);

    /// <summary>Лише помилки.</summary>
    public IEnumerable<ImportIssue> Errors => Issues.Where(i => i.Severity == ImportSeverity.Error);

    /// <summary>Лише попередження.</summary>
    public IEnumerable<ImportIssue> Warnings => Issues.Where(i => i.Severity == ImportSeverity.Warning);
}
