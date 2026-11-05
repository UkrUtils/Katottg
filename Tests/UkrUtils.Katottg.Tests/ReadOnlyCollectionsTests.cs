using UkrUtils.Katottg.Import;
using UkrUtils.Katottg.Tests.Fixtures;

namespace UkrUtils.Katottg.Tests;

/// <summary>Публічні колекції не можна змінити приведенням до масиву чи списку.</summary>
public class ReadOnlyCollectionsTests
{
    private static void AssertReadOnly<T>(IReadOnlyList<T> list)
    {
        Assert.False(list is T[], "exposed as array");
        Assert.False(list is List<T>, "exposed as List<T>");
    }

    [Fact]
    public void ImportResult()
    {
        var result = KatottgSheetParser.Parse(new MemoryStream(FixtureData.CreateOfficialLike().Build()), "katottg-26-11-03.xlsx");
        AssertReadOnly(result.Records);
        AssertReadOnly(result.Report.Issues);
        AssertReadOnly(result.Report.Notes);
    }

    [Theory]
    [InlineData("Андріївка")]
    [InlineData("UA01020010010075540")]
    [InlineData("Ыыыыы")]
    public void SearchResults(string text)
    {
        AssertReadOnly(FixtureDataset.Value.Search(text).Hits);
        AssertReadOnly(FixtureDataset.Value.Search(new KatottgQuery { Name = text }));
    }

    [Fact]
    public void TreeAndPath()
    {
        var roots = FixtureDataset.Value.Search("Андріївка").ToTree();
        AssertReadOnly(roots);
        AssertReadOnly(roots[0].Children);
        AssertReadOnly(FixtureDataset.Value.All[5].Path);
    }
}
