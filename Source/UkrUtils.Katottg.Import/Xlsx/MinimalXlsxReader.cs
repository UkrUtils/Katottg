using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace UkrUtils.Katottg.Import.Xlsx;

/// <summary>Sheet row: number (from 1) and cell values by column index (A = 0).</summary>
internal readonly record struct XlsxRow(int Number, IReadOnlyList<string?> Cells)
{
    public string? this[int column] => column >= 0 && column < Cells.Count ? Cells[column] : null;

    public bool IsBlank => Cells.All(string.IsNullOrWhiteSpace);
}

/// <summary>
/// Minimal streaming xlsx reader (OOXML = zip with XML) with no third-party dependencies.
/// Reads only the cell values of the first sheet; styles, formulas, and number types are ignored.
/// </summary>
internal sealed class MinimalXlsxReader : IDisposable
{
    private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipsNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelationshipsNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    private static readonly XmlReaderSettings s_settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        CloseInput = true,
    };

    private readonly ZipArchive _zip;

    /// <exception cref="InvalidDataException">The stream is not a zip archive.</exception>
    public MinimalXlsxReader(Stream stream) => _zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

    public void Dispose() => _zip.Dispose();

    /// <summary>The <c>dcterms:modified</c> value from <c>docProps/core.xml</c>, if present.</summary>
    public string? ReadCoreModified()
    {
        var entry = FindEntry("docProps/core.xml");
        if (entry is null)
            return null;
        using var reader = XmlReader.Create(entry.Open(), s_settings);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "modified")
                return reader.ReadElementContentAsString().Trim();
        }
        return null;
    }

    /// <summary>Reads the rows of the workbook's first sheet as a stream.</summary>
    /// <exception cref="InvalidDataException">The workbook has no sheets or required parts are missing.</exception>
    public IEnumerable<XlsxRow> ReadFirstSheet()
    {
        var workbookPath = FindOfficeDocumentPath() ?? "xl/workbook.xml";
        var workbookEntry = FindEntry(workbookPath) ?? throw new InvalidDataException($"Workbook part '{workbookPath}' not found.");

        var sheetRelId = ReadFirstSheetRelationshipId(workbookEntry) ?? throw new InvalidDataException("Workbook contains no sheets.");
        var relationships = ReadRelationships(RelationshipsPathFor(workbookPath));

        var sheetPath = relationships.FirstOrDefault(r => r.Id == sheetRelId).Target is { } target
            ? ResolvePath(workbookPath, target)
            : throw new InvalidDataException($"Sheet relationship '{sheetRelId}' not found.");

        var sharedStringsTarget = relationships.FirstOrDefault(r => r.Type.EndsWith("/sharedStrings", StringComparison.Ordinal)).Target;
        var sharedStringsPath = sharedStringsTarget is null ? "xl/sharedStrings.xml" : ResolvePath(workbookPath, sharedStringsTarget);
        var sharedStrings = ReadSharedStrings(sharedStringsPath);

        var sheetEntry = FindEntry(sheetPath) ?? throw new InvalidDataException($"Sheet part '{sheetPath}' not found.");
        return ReadRows(sheetEntry, sharedStrings);
    }

    private static IEnumerable<XlsxRow> ReadRows(ZipArchiveEntry entry, string[] sharedStrings)
    {
        using var reader = XmlReader.Create(entry.Open(), s_settings);
        var previousRow = 0;
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "row" || reader.NamespaceURI != MainNs)
                continue;

            var number = int.TryParse(reader.GetAttribute("r"), NumberStyles.None, CultureInfo.InvariantCulture, out var r) ? r : previousRow + 1;
            previousRow = number;

            var cells = new List<string?>();
            if (!reader.IsEmptyElement)
            {
                using var row = reader.ReadSubtree();
                row.Read();
                var column = -1;
                while (row.Read())
                {
                    if (row.NodeType != XmlNodeType.Element || row.LocalName != "c")
                        continue;
                    var reference = row.GetAttribute("r");
                    column = reference is null ? column + 1 : ColumnIndex(reference);
                    var value = ReadCell(row, sharedStrings);
                    while (cells.Count <= column)
                        cells.Add(null);
                    cells[column] = value;
                }
            }
            yield return new XlsxRow(number, cells);
        }
    }

    private static string? ReadCell(XmlReader reader, string[] sharedStrings)
    {
        var type = reader.GetAttribute("t");
        if (reader.IsEmptyElement)
            return null;

        string? raw = null;
        string? inline = null;
        using (var cell = reader.ReadSubtree())
        {
            cell.Read();
            while (!cell.EOF)
            {
                if (cell.NodeType == XmlNodeType.Element)
                {
                    if (cell.LocalName == "v")
                    {
                        raw = cell.ReadElementContentAsString();
                        continue;
                    }
                    if (cell.LocalName == "is")
                    {
                        inline = ReadRichText(cell);
                        continue;
                    }
                }
                cell.Read();
            }
        }

        switch (type)
        {
            case "s":
                return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && (uint)index < (uint)sharedStrings.Length
                    ? sharedStrings[index]
                    : throw new InvalidDataException($"Invalid shared string index '{raw}'.");
            case "inlineStr":
                return inline ?? raw;
            default: // n, str, b, e, d
                return raw;
        }
    }

    /// <summary>Concatenates all <c>&lt;t&gt;</c> of the current element (<c>si</c> or <c>is</c>), skipping phonetic blocks.</summary>
    private static string ReadRichText(XmlReader reader)
    {
        var sb = new StringBuilder();
        using (var sub = reader.ReadSubtree())
        {
            sub.Read();
            while (!sub.EOF)
            {
                if (sub.NodeType == XmlNodeType.Element)
                {
                    if (sub.LocalName is "rPh" or "phoneticPr")
                    {
                        sub.Skip();
                        continue;
                    }
                    if (sub.LocalName == "t")
                    {
                        sb.Append(sub.ReadElementContentAsString());
                        continue;
                    }
                }
                sub.Read();
            }
        }
        // ReadSubtree залишає батьківський reader на кінцевому тезі; переходимо далі.
        reader.Read();
        return sb.ToString();
    }

    private string[] ReadSharedStrings(string path)
    {
        var entry = FindEntry(path);
        if (entry is null)
            return [];

        var result = new List<string>();
        using var reader = XmlReader.Create(entry.Open(), s_settings);
        reader.MoveToContent();
        while (!reader.EOF)
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "si")
            {
                result.Add(ReadRichText(reader));
                continue;
            }
            reader.Read();
        }
        return [.. result];
    }

    private static string? ReadFirstSheetRelationshipId(ZipArchiveEntry workbook)
    {
        using var reader = XmlReader.Create(workbook.Open(), s_settings);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "sheet" && reader.NamespaceURI == MainNs)
                return reader.GetAttribute("id", RelationshipsNs);
        }
        return null;
    }

    private string? FindOfficeDocumentPath()
    {
        var rel = ReadRelationships("_rels/.rels").FirstOrDefault(r => r.Type.EndsWith("/officeDocument", StringComparison.Ordinal));
        return rel.Target is null ? null : ResolvePath("", rel.Target);
    }

    private List<(string Id, string Type, string Target)> ReadRelationships(string path)
    {
        var result = new List<(string, string, string)>();
        var entry = FindEntry(path);
        if (entry is null)
            return result;
        using var reader = XmlReader.Create(entry.Open(), s_settings);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Relationship" && reader.NamespaceURI == PackageRelationshipsNs)
                result.Add((reader.GetAttribute("Id") ?? "", reader.GetAttribute("Type") ?? "", reader.GetAttribute("Target") ?? ""));
        }
        return result;
    }

    private ZipArchiveEntry? FindEntry(string path)
        => _zip.GetEntry(path) ?? _zip.Entries.FirstOrDefault(e => string.Equals(e.FullName.Replace('\\', '/'), path, StringComparison.OrdinalIgnoreCase));

    private static string RelationshipsPathFor(string partPath)
    {
        var slash = partPath.LastIndexOf('/');
        return slash < 0 ? $"_rels/{partPath}.rels" : $"{partPath[..slash]}/_rels/{partPath[(slash + 1)..]}.rels";
    }

    /// <summary>Path of a part relative to the source part, as in OPC: absolute (<c>/xl/...</c>) or relative with <c>..</c>.</summary>
    internal static string ResolvePath(string sourcePartPath, string target)
    {
        target = target.Replace('\\', '/');
        if (target.StartsWith('/'))
            return target.TrimStart('/');

        var slash = sourcePartPath.LastIndexOf('/');
        var segments = new List<string>(slash < 0 ? [] : sourcePartPath[..slash].Split('/'));
        foreach (var segment in target.Split('/'))
        {
            if (segment is "" or ".")
                continue;
            if (segment == "..")
            {
                if (segments.Count > 0)
                    segments.RemoveAt(segments.Count - 1);
                continue;
            }
            segments.Add(segment);
        }
        return string.Join('/', segments);
    }

    /// <summary>Column index from a cell reference: <c>A1</c> → 0, <c>AB12</c> → 27.</summary>
    internal static int ColumnIndex(string reference)
    {
        var index = 0;
        foreach (var c in reference)
        {
            var upper = char.ToUpperInvariant(c);
            if (upper is < 'A' or > 'Z')
                break;
            index = index * 26 + (upper - 'A' + 1);
        }
        return index == 0 ? throw new InvalidDataException($"Invalid cell reference '{reference}'.") : index - 1;
    }
}
