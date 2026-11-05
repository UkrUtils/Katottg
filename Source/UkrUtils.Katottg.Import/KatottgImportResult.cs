namespace UkrUtils.Katottg.Import;

/// <summary>Параметри імпорту.</summary>
public sealed class KatottgImportOptions
{
    /// <summary>Дата редакції. Якщо не задано — з імені файлу (<c>katottg-YY-MM-DD</c>), потім з <c>docProps/core.xml</c>.</summary>
    public DateOnly? EditionDate { get; init; }

    /// <summary>Джерело для <see cref="KatottgEdition.Source"/>. Якщо не задано — ім’я файлу.</summary>
    public string? Source { get; init; }
}

/// <summary>Результат імпорту: записи у порядку файлу, редакція і звіт.</summary>
public sealed class KatottgImportResult
{
    internal KatottgImportResult(KatottgEdition edition, IReadOnlyList<KatottgRecord> records, ImportReport report)
    {
        Edition = edition;
        Records = records;
        Report = report;
    }

    /// <summary>Відомості про редакцію.</summary>
    public KatottgEdition Edition { get; }

    /// <summary>Імпортовані записи в порядку файлу (батьки раніше за дітей). Рядки з помилками сюди не потрапляють.</summary>
    public IReadOnlyList<KatottgRecord> Records { get; }

    /// <summary>Звіт про імпорт.</summary>
    public ImportReport Report { get; }
}
