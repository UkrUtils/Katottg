using System.IO.Compression;
using System.Security;
using System.Text;

namespace UkrUtils.Katottg.Tests.Fixtures;

/// <summary>Як записувати текстові комірки у фікстурі.</summary>
public enum CellMode
{
    SharedString,
    InlineString,
    FormulaString,
}

/// <summary>
/// Мінімальний генератор xlsx для тестів: один аркуш, лише текстові комірки.
/// Дає змогу перевіряти різні варіанти OOXML, не тримаючи бінарні фікстури в репозиторії.
/// </summary>
internal sealed class XlsxBuilder
{
    private readonly List<(int Number, string?[] Cells)> _rows = [];

    public string SheetName { get; init; } = "Кодифікатор";

    /// <summary>Шлях аркуша відносно <c>xl/</c>.</summary>
    public string SheetTarget { get; init; } = "worksheets/sheet1.xml";

    public CellMode Mode { get; init; } = CellMode.SharedString;

    /// <summary>Не писати атрибут <c>r</c> у комірках (допустимо за стандартом).</summary>
    public bool OmitCellReferences { get; init; }

    /// <summary>Значення <c>dcterms:modified</c> у <c>docProps/core.xml</c>; <c>null</c> — не писати core.xml.</summary>
    public string? CoreModified { get; init; }

    public XlsxBuilder AddRow(params string?[] cells)
    {
        var number = _rows.Count == 0 ? 1 : _rows[^1].Number + 1;
        _rows.Add((number, cells));
        return this;
    }

    public XlsxBuilder SkipRows(int count)
    {
        var number = (_rows.Count == 0 ? 0 : _rows[^1].Number) + count;
        _rows.Add((number, []));
        return this;
    }

    public byte[] Build()
    {
        var shared = new List<string>();
        var sharedIndex = new Dictionary<string, int>(StringComparer.Ordinal);

        var sheet = new StringBuilder();
        sheet.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sheet.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheetData>""");
        foreach (var (number, cells) in _rows)
        {
            sheet.Append($"<row r=\"{number}\">");
            for (var col = 0; col < cells.Length; col++)
            {
                var value = cells[col];
                if (value is null)
                {
                    // Без атрибута r позиція визначається порядком, тож порожню комірку треба записати.
                    if (OmitCellReferences)
                        sheet.Append("<c/>");
                    continue;
                }
                var r = OmitCellReferences ? "" : $" r=\"{ColumnName(col)}{number}\"";
                switch (Mode)
                {
                    case CellMode.SharedString:
                        if (!sharedIndex.TryGetValue(value, out var index))
                        {
                            index = shared.Count;
                            shared.Add(value);
                            sharedIndex[value] = index;
                        }
                        sheet.Append($"<c{r} t=\"s\"><v>{index}</v></c>");
                        break;
                    case CellMode.InlineString:
                        sheet.Append($"<c{r} t=\"inlineStr\"><is><t xml:space=\"preserve\">{Escape(value)}</t></is></c>");
                        break;
                    case CellMode.FormulaString:
                        sheet.Append($"<c{r} t=\"str\"><f>A1</f><v>{Escape(value)}</v></c>");
                        break;
                }
            }
            sheet.Append("</row>");
        }
        sheet.Append("</sheetData></worksheet>");

        var sst = new StringBuilder();
        sst.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sst.Append($"""<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="{shared.Count}" uniqueCount="{shared.Count}">""");
        for (var i = 0; i < shared.Count; i++)
        {
            // Кожен третій рядок пишемо як rich text з кількох фрагментів і фонетичним блоком,
            // щоб перевірити склеювання <r><t> і пропуск <rPh>.
            var s = shared[i];
            if (i % 3 == 2 && s.Length > 1)
            {
                var half = s.Length / 2;
                sst.Append($"<si><r><t xml:space=\"preserve\">{Escape(s[..half])}</t></r><r><rPr><b/></rPr><t xml:space=\"preserve\">{Escape(s[half..])}</t></r><rPh sb=\"0\" eb=\"1\"><t>IGNORED</t></rPh></si>");
            }
            else
            {
                sst.Append($"<si><t xml:space=\"preserve\">{Escape(s)}</t></si>");
            }
        }
        sst.Append("</sst>");

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/></Types>""");
            Add(zip, "_rels/.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Add(zip, "xl/workbook.xml", $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="{Escape(SheetName)}" sheetId="7" r:id="rId9"/></sheets></workbook>""");
            Add(zip, "xl/_rels/workbook.xml.rels", $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/><Relationship Id="rId9" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="{SheetTarget}"/></Relationships>""");
            Add(zip, "xl/" + SheetTarget, sheet.ToString());
            if (shared.Count > 0)
                Add(zip, "xl/sharedStrings.xml", sst.ToString());
            if (CoreModified is not null)
                Add(zip, "docProps/core.xml", $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"><dcterms:modified xsi:type="dcterms:W3CDTF">{CoreModified}</dcterms:modified></cp:coreProperties>""");
        }
        return ms.ToArray();
    }

    private static void Add(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name);
        using var stream = entry.Open();
        var bytes = new UTF8Encoding(false).GetBytes(content);
        stream.Write(bytes);
    }

    private static string Escape(string s) => SecurityElement.Escape(s);

    private static string ColumnName(int index)
    {
        var name = "";
        index++;
        while (index > 0)
        {
            var rem = (index - 1) % 26;
            name = (char)('A' + rem) + name;
            index = (index - 1) / 26;
        }
        return name;
    }
}
