using System.Security.Cryptography;
using UkrUtils.Katottg.Import;
using UkrUtils.Katottg.Tests.Fixtures;

namespace UkrUtils.Katottg.Tests.Import;

public class KatottgSheetParserTests
{
    private static KatottgImportResult ParseFixture(XlsxBuilder? builder = null, string? fileName = "katottg-26-11-03.xlsx", KatottgImportOptions? options = null)
    {
        var bytes = (builder ?? FixtureData.CreateOfficialLike()).Build();
        return KatottgSheetParser.Parse(new MemoryStream(bytes), fileName, options);
    }

    private static KatottgRecord Get(KatottgImportResult result, string code)
        => result.Records.Single(r => r.Code == KatottgCode.Parse(code));

    [Fact]
    public void ParsesAllDataRows()
    {
        var result = ParseFixture();

        Assert.False(result.Report.HasErrors, string.Join("\n", result.Report.Errors));
        Assert.Equal(FixtureData.RowCount, result.Records.Count);
        Assert.Equal(FixtureData.RowCount, result.Report.DataRows);
    }

    [Fact]
    public void ResolvesParents_IncludingRepeatedLevelCodes()
    {
        var result = ParseFixture();

        Assert.Null(Get(result, "UA01000000000013043").ParentCode);
        Assert.Null(Get(result, "UA80000000000093317").ParentCode);
        Assert.Equal(KatottgCode.Parse("UA01020010000048857"), Get(result, "UA01020010010075540").ParentCode);
        // Прип’ять — прямо під Київською областю
        Assert.Equal(KatottgCode.Parse("UA32000000000030281"), Get(result, "UA32000000010085013").ParentCode);
        // Район у Києві — під Києвом, попри повтор коду на рівнях 1–4
        Assert.Equal(KatottgCode.Parse("UA80000000000093317"), Get(result, "UA80000000000126643").ParentCode);
        // Район у Харкові — під містом Харків
        Assert.Equal(KatottgCode.Parse("UA63120270010096107"), Get(result, "UA63120270010158723").ParentCode);
    }

    [Fact]
    public void MapsCategoriesAndTrimsNames()
    {
        var result = ParseFixture();

        var community = Get(result, "UA01020010000048857");
        Assert.Equal(KatottgCategory.Community, community.Category);
        Assert.Equal("Андріївська", community.Name);
        Assert.Equal(KatottgCategory.SpecialStatusCity, Get(result, "UA80000000000093317").Category);
        Assert.Equal(KatottgCategory.CityDistrict, Get(result, "UA80000000000126643").Category);
        Assert.Equal("Прип’ять", Get(result, "UA32000000010085013").Name);

        var trimmed = result.Report.Issues.Where(i => i.Kind == ImportIssueKind.NameHasSurroundingWhitespace).ToList();
        Assert.Equal(2, trimmed.Count);
        Assert.All(trimmed, i => Assert.Equal(ImportSeverity.Info, i.Severity));
    }

    [Fact]
    public void WarnsAboutLatinLettersInName_ButKeepsOriginal()
    {
        var result = ParseFixture();

        Assert.Equal("V\u0069йтовецька".Replace("V", "В"), Get(result, "UA68040030000027335").Name);
        var issue = Assert.Single(result.Report.Warnings);
        Assert.Equal(ImportIssueKind.LatinCharactersInName, issue.Kind);
        Assert.Equal(KatottgCode.Parse("UA68040030000027335"), issue.Code);
    }

    [Fact]
    public void ReportsServiceRowsAndNotes()
    {
        var result = ParseFixture();
        var report = result.Report;

        // гриф і заголовок — до шапки, примітка — після даних
        Assert.Equal(3, report.SkippedRows);
        Assert.Equal(1, report.EmptyRows);
        Assert.Equal(3, report.HeaderRow);
        var note = Assert.Single(report.Notes);
        Assert.StartsWith("Примітка.", note);
    }

    [Fact]
    public void Edition_FromFileName()
    {
        var bytes = FixtureData.CreateOfficialLike().Build();
        var result = KatottgSheetParser.Parse(new MemoryStream(bytes), @"C:\data\katottg-26-11-03.xlsx");

        Assert.Equal(new DateOnly(2026, 11, 3), result.Edition.Date);
        Assert.Equal("katottg-26-11-03.xlsx", result.Edition.Source);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), result.Edition.Sha256);
    }

    [Theory]
    [InlineData("katottg-2026-07-22.xlsx", 2026, 7, 22)]
    [InlineData("KATOTTG_25_01_31.xlsx", 2025, 1, 31)]
    public void Edition_FromFileNameVariants(string fileName, int y, int m, int d)
        => Assert.Equal(new DateOnly(y, m, d), ParseFixture(fileName: fileName).Edition.Date);

    [Fact]
    public void Edition_FallsBackToCoreProperties()
    {
        var result = ParseFixture(FixtureData.CreateOfficialLike(coreModified: "2026-06-22T10:00:00Z"), fileName: null);
        Assert.Equal(new DateOnly(2026, 6, 22), result.Edition.Date);
        Assert.DoesNotContain(result.Report.Issues, i => i.Kind == ImportIssueKind.EditionDateUnknown);
    }

    [Fact]
    public void Edition_UnknownDate_IsWarning()
    {
        var result = ParseFixture(fileName: "data.xlsx");
        Assert.Equal(default, result.Edition.Date);
        Assert.Contains(result.Report.Warnings, i => i.Kind == ImportIssueKind.EditionDateUnknown);
    }

    [Fact]
    public void Edition_Override()
    {
        var result = ParseFixture(options: new KatottgImportOptions { EditionDate = new DateOnly(2020, 1, 2), Source = "manual" });
        Assert.Equal(new DateOnly(2020, 1, 2), result.Edition.Date);
        Assert.Equal("manual", result.Edition.Source);
    }

    [Theory]
    [InlineData(CellMode.SharedString, false)]
    [InlineData(CellMode.SharedString, true)]
    [InlineData(CellMode.InlineString, false)]
    [InlineData(CellMode.FormulaString, true)]
    public void CellEncodings_GiveSameRecords(CellMode mode, bool omitRefs)
    {
        var expected = ParseFixture().Records;
        var actual = ParseFixture(FixtureData.CreateOfficialLike(mode, omitRefs)).Records;
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SheetIsResolvedThroughRelationships()
    {
        var builder = new XlsxBuilder { SheetName = "Аркуш1", SheetTarget = "worksheets/data/other.xml" };
        builder.AddRow(FixtureData.Header);
        foreach (var row in FixtureData.Rows)
            builder.AddRow(row);

        var result = ParseFixture(builder);
        Assert.Equal(FixtureData.RowCount, result.Records.Count);
        Assert.False(result.Report.HasErrors);
    }

    [Fact]
    public void ColumnsAreFoundByHeader()
    {
        // Назва, категорія, потім рівні у зворотному порядку, з порожньою колонкою на початку
        static string?[] Shuffle(string?[] r) => [null, r[6], r[5], r[4], r[3], r[2], r[1], r[0]];

        var builder = new XlsxBuilder();
        builder.AddRow(Shuffle(FixtureData.Header));
        foreach (var row in FixtureData.Rows)
            builder.AddRow(Shuffle(row));

        Assert.Equal(ParseFixture().Records, ParseFixture(builder).Records);
    }

    [Fact]
    public void MissingHeader_IsError()
    {
        var builder = new XlsxBuilder();
        foreach (var row in FixtureData.Rows)
            builder.AddRow(row);

        var result = ParseFixture(builder);
        Assert.Empty(result.Records);
        Assert.Contains(result.Report.Errors, i => i.Kind == ImportIssueKind.HeaderNotFound);
    }

    private static KatottgImportResult ParseRows(params string?[][] rows)
    {
        var builder = new XlsxBuilder();
        builder.AddRow(FixtureData.Header);
        foreach (var row in rows)
            builder.AddRow(row);
        return ParseFixture(builder);
    }

    private static readonly string?[] Region = ["UA05000000000010236", null, null, null, null, "O", "Вінницька"];
    private static readonly string?[] District = ["UA05000000000010236", "UA05020000000026686", null, null, null, "P", "Вінницький"];

    [Fact]
    public void DuplicateCode_IsError_AndSecondRowSkipped()
    {
        var result = ParseRows(Region, District, ["UA05000000000010236", "UA05020000000026686", null, null, null, "P", "Інший"]);

        Assert.Equal(2, result.Records.Count);
        Assert.Equal("Вінницький", Get(result, "UA05020000000026686").Name);
        var error = Assert.Single(result.Report.Errors);
        Assert.Equal(ImportIssueKind.DuplicateCode, error.Kind);
        Assert.Equal(4, error.Row);
    }

    [Fact]
    public void DuplicateSignificantPart_IsError()
    {
        var result = ParseRows(Region, District, ["UA05000000000010236", "UA05020000000099999", null, null, null, "P", "Інший"]);

        Assert.Equal(2, result.Records.Count);
        Assert.Equal(ImportIssueKind.DuplicateSignificantPart, Assert.Single(result.Report.Errors).Kind);
    }

    [Fact]
    public void MissingParent_IsError()
    {
        var result = ParseRows(District);

        Assert.Empty(result.Records);
        Assert.Equal(ImportIssueKind.MissingParent, Assert.Single(result.Report.Errors).Kind);
    }

    [Fact]
    public void InvalidCodeInLevel_IsError()
    {
        var result = ParseRows(Region, ["UA05000000000010236", "UA0502000000002668X", null, null, null, "P", "Вінницький"]);

        Assert.Single(result.Records);
        Assert.Equal(ImportIssueKind.InvalidCode, Assert.Single(result.Report.Errors).Kind);
    }

    [Fact]
    public void EmptyName_IsError()
    {
        var result = ParseRows(Region, ["UA05000000000010236", "UA05020000000026686", null, null, null, "P", "  "]);

        Assert.Single(result.Records);
        Assert.Equal(ImportIssueKind.EmptyName, Assert.Single(result.Report.Errors).Kind);
    }

    [Fact]
    public void UnknownCategory_IsWarning()
    {
        var result = ParseRows(Region, ["UA05000000000010236", "UA05020000000026686", null, null, null, "Z", "Вінницький"]);

        Assert.Equal(KatottgCategory.Unknown, Get(result, "UA05020000000026686").Category);
        Assert.Equal(ImportIssueKind.UnknownCategory, Assert.Single(result.Report.Warnings).Kind);
    }

    [Fact]
    public void CyrillicCategoryLetter_IsAccepted()
    {
        var result = ParseRows(Region, ["UA05000000000010236", "UA05020000000026686", null, null, null, "\u0420", "Вінницький"]);

        Assert.Equal(KatottgCategory.District, Get(result, "UA05020000000026686").Category);
        Assert.Empty(result.Report.Warnings);
    }

    [Fact]
    public void PrefixMismatch_IsWarning()
    {
        var result = ParseRows(Region, ["UA05000000000010236", "UA07020000000026686", null, null, null, "P", "Чужий"]);

        Assert.Equal(2, result.Records.Count);
        Assert.Equal(ImportIssueKind.PrefixMismatch, Assert.Single(result.Report.Warnings).Kind);
    }

    [Fact]
    public void UnexpectedHierarchy_IsWarning()
    {
        // село прямо під районом, без громади
        var result = ParseRows(Region, District, ["UA05000000000010236", "UA05020000000026686", "UA05020000010012345", null, null, "C", "Село"]);

        Assert.Equal(3, result.Records.Count);
        Assert.Equal(ImportIssueKind.UnexpectedHierarchy, Assert.Single(result.Report.Warnings).Kind);
    }

    [Fact]
    public void GapBetweenLevels_IsWarning()
    {
        var result = ParseRows(Region, ["UA05000000000010236", null, "UA05020000000026686", null, null, "P", "Вінницький"]);

        Assert.Equal(2, result.Records.Count);
        Assert.Equal(ImportIssueKind.LevelGap, Assert.Single(result.Report.Warnings).Kind);
    }

    [Fact]
    public void CyrillicUaPrefixInFirstLevel_IsData()
    {
        var result = ParseRows(Region, ["УА05000000000010236", "UA05020000000026686", null, null, null, "P", "Вінницький"]);

        Assert.Equal(2, result.Records.Count);
        Assert.Equal(KatottgCode.Parse("UA05000000000010236"), Get(result, "UA05020000000026686").ParentCode);
        Assert.False(result.Report.HasErrors);
    }

    [Fact]
    public void CodesWithEmptyFirstLevel_IsError_NotSilentlySkipped()
    {
        var result = ParseRows(Region, [null, "UA05020000000026686", null, null, null, "P", "Вінницький"]);

        Assert.Single(result.Records);
        Assert.Equal(ImportIssueKind.LevelGap, Assert.Single(result.Report.Errors).Kind);
    }

    [Fact]
    public void MalformedSheetXml_ThrowsInvalidData()
    {
        var bytes = FixtureData.CreateOfficialLike().Build();
        using var ms = new MemoryStream();
        ms.Write(bytes);
        using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Update, leaveOpen: true))
        {
            zip.GetEntry("xl/worksheets/sheet1.xml")!.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("xl/worksheets/sheet1.xml").Open());
            writer.Write("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\"><c");
        }
        ms.Position = 0;
        Assert.Throws<InvalidDataException>(() => KatottgSheetParser.Parse(ms, "x.xlsx"));
    }

    [Fact]
    public void NotAZip_Throws()
        => Assert.Throws<InvalidDataException>(() => KatottgSheetParser.Parse(new MemoryStream([1, 2, 3]), "x.xlsx"));
}
