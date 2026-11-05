namespace UkrUtils.Katottg.Import;

/// <summary>Import options.</summary>
public sealed class KatottgImportOptions
{
    /// <summary>Edition date. If not set, taken from the file name (<c>katottg-YY-MM-DD</c>), then from <c>docProps/core.xml</c>.</summary>
    public DateOnly? EditionDate { get; init; }

    /// <summary>Source for <see cref="KatottgEdition.Source"/>. If not set, the file name.</summary>
    public string? Source { get; init; }
}

/// <summary>Import result: records in file order, the edition, and the report.</summary>
public sealed class KatottgImportResult
{
    internal KatottgImportResult(KatottgEdition edition, IReadOnlyList<KatottgRecord> records, ImportReport report)
    {
        Edition = edition;
        Records = records;
        Report = report;
    }

    /// <summary>Information about the edition.</summary>
    public KatottgEdition Edition { get; }

    /// <summary>Imported records in file order (parents before children). Rows with errors are not included.</summary>
    public IReadOnlyList<KatottgRecord> Records { get; }

    /// <summary>Import report.</summary>
    public ImportReport Report { get; }

    /// <summary>
    /// Builds a <see cref="KatottgDataset"/> from the imported records. Check
    /// <see cref="ImportReport.HasErrors"/> first: rows with errors are not included in the dataset.
    /// </summary>
    public KatottgDataset CreateDataset() => KatottgDataset.Create(Edition, Records);
}
