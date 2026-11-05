using UkrUtils.Katottg.Import;
using UkrUtils.Katottg.Tests.Fixtures;

namespace UkrUtils.Katottg.Tests.Import;

/// <summary>Інтеграційні тести на повному офіційному файлі з <c>OfficialData/</c>.</summary>
public class OfficialFileTests
{
    internal static readonly Lazy<KatottgImportResult> Imported = new(() => KatottgSheetParser.Parse(TestPaths.LatestOfficialXlsx));

    [Fact]
    public void ImportsWithoutErrors()
    {
        var report = Imported.Value.Report;

        Assert.False(report.HasErrors, string.Join("\n", report.Errors.Take(20)));
        Assert.Equal(31_751, Imported.Value.Records.Count);
        Assert.Equal(3, report.HeaderRow);

        // Єдине попередження — латинська «i» у «Вiйтовецька»
        var warning = Assert.Single(report.Warnings);
        Assert.Equal(ImportIssueKind.LatinCharactersInName, warning.Kind);
        Assert.Equal(319, report.Issues.Count(i => i.Kind == ImportIssueKind.NameHasSurroundingWhitespace));
        Assert.StartsWith("Примітка.", Assert.Single(report.Notes));
    }

    [Fact]
    public void CategoryCountsMatchPlan()
    {
        var counts = Imported.Value.Records.GroupBy(r => r.Category).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(25, counts[KatottgCategory.Region]);
        Assert.Equal(2, counts[KatottgCategory.SpecialStatusCity]);
        Assert.Equal(136, counts[KatottgCategory.District]);
        Assert.Equal(1_772, counts[KatottgCategory.Community]);
        Assert.Equal(459, counts[KatottgCategory.City]);
        Assert.Equal(881, counts[KatottgCategory.UrbanTypeSettlement]);
        Assert.Equal(1_164, counts[KatottgCategory.Settlement]);
        Assert.Equal(27_204, counts[KatottgCategory.Village]);
        Assert.Equal(108, counts[KatottgCategory.CityDistrict]);
        Assert.False(counts.ContainsKey(KatottgCategory.Unknown));
    }

    [Fact]
    public void EditionComesFromFileName()
    {
        var edition = Imported.Value.Edition;
        Assert.Equal(new DateOnly(2026, 11, 3), edition.Date);
        Assert.Equal("katottg-26-11-03.xlsx", edition.Source);
        Assert.Equal(64, edition.Sha256!.Length);
    }

    [Fact]
    public void CodesRoundTripThroughText()
    {
        foreach (var record in Imported.Value.Records)
            Assert.Equal(record.Code, KatottgCode.Parse(record.Code.ToString()));
    }

    [Fact]
    public void ParentPrefixMatchesActualParent()
    {
        var bySignificant = Imported.Value.Records.ToDictionary(r => r.Code.SignificantPart);
        foreach (var record in Imported.Value.Records)
        {
            var prefix = record.Code.ParentPrefix;
            if (record.ParentCode is null)
            {
                Assert.Null(prefix);
                continue;
            }
            Assert.NotNull(prefix);
            Assert.Equal(record.ParentCode, bySignificant[prefix.Value.SignificantPart].Code);
        }
    }
}
