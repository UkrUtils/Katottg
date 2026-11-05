using UkrUtils.Katottg.Tests.Fixtures;

namespace UkrUtils.Katottg.Tests;

/// <summary>Інваріанти ієрархії на повному офіційному наборі.</summary>
public class HierarchyInvariantTests
{
    private static KatottgDataset Data => FixtureDataset.Official;

    [Fact]
    public void RootsAreRegionsAndSpecialCities()
    {
        Assert.Equal(27, Data.Regions.Count);
        Assert.All(Data.Regions, r => Assert.True(r.Category is KatottgCategory.Region or KatottgCategory.SpecialStatusCity));
        Assert.Equal(27, Data.All.Count(u => u.Parent is null));
    }

    [Fact]
    public void TreeCoversAllUnits()
        => Assert.Equal(Data.Count, Data.Regions.Sum(r => 1 + r.Descendants().Count()));

    [Fact]
    public void DepthIsAtMostFour_AndConsistent()
    {
        foreach (var unit in Data.All)
        {
            Assert.InRange(unit.Depth, 0, 4);
            Assert.Equal(unit.Parent is null ? 0 : unit.Parent.Depth + 1, unit.Depth);
            Assert.Equal(unit.Depth, unit.Ancestors().Count());
        }
    }

    [Fact]
    public void ParentChildLinksAgree()
    {
        foreach (var unit in Data.All)
        {
            if (unit.Parent is not null)
                Assert.Contains(unit, unit.Parent.Children);
            Assert.All(unit.Children, c => Assert.Same(unit, c.Parent));
        }
    }

    [Fact]
    public void CategoryPlacement()
    {
        foreach (var unit in Data.All)
        {
            switch (unit.Category)
            {
                case KatottgCategory.CityDistrict:
                    Assert.True(unit.Parent!.Category is KatottgCategory.City or KatottgCategory.SpecialStatusCity);
                    break;
                case KatottgCategory.Community:
                    Assert.Equal(KatottgCategory.District, unit.Parent!.Category);
                    break;
                case KatottgCategory.District:
                    Assert.Equal(KatottgCategory.Region, unit.Parent!.Category);
                    break;
            }
            Assert.True(unit.Region.Category is KatottgCategory.Region or KatottgCategory.SpecialStatusCity);
            Assert.Null(unit.Region.Parent);
        }
    }

    [Fact]
    public void CategoryCounts()
    {
        Assert.Equal(25, Data.GetByCategory(KatottgCategory.Region).Count);
        Assert.Equal(2, Data.GetByCategory(KatottgCategory.SpecialStatusCity).Count);
        Assert.Equal(136, Data.GetByCategory(KatottgCategory.District).Count);
        Assert.Equal(1_772, Data.GetByCategory(KatottgCategory.Community).Count);
        Assert.Equal(459, Data.GetByCategory(KatottgCategory.City).Count);
        Assert.Equal(881, Data.GetByCategory(KatottgCategory.UrbanTypeSettlement).Count);
        Assert.Equal(1_164, Data.GetByCategory(KatottgCategory.Settlement).Count);
        Assert.Equal(27_204, Data.GetByCategory(KatottgCategory.Village).Count);
        Assert.Equal(108, Data.GetByCategory(KatottgCategory.CityDistrict).Count);
    }

    [Fact]
    public void SignificantPartLookupFindsEveryUnit()
    {
        foreach (var unit in Data.All)
        {
            Assert.Same(unit, Data.Find(unit.Code));
            Assert.Same(unit, Data.FindBySignificantPart(unit.Code));
            if (unit.Code.ParentPrefix is { } prefix)
                Assert.Same(unit.Parent, Data.FindBySignificantPart(prefix));
        }
    }

    [Fact]
    public void TemporarilyNotInForce_OnlyCrimeaAndSevastopol()
    {
        var flagged = Data.All.Where(u => u.IsTemporarilyNotInForce).Select(u => u.Region.Name).Distinct().Order().ToList();
        Assert.Equal(["Автономна Республіка Крим", "Севастополь"], flagged);
    }
}
