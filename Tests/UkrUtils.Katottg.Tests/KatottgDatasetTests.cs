using UkrUtils.Katottg.Tests.Fixtures;

namespace UkrUtils.Katottg.Tests;

public class KatottgDatasetTests
{
    private static KatottgDataset Data => FixtureDataset.Value;

    private const string Andriivka = "UA01020010010075540";
    private const string KharkivIndustrialnyi = "UA63120270010158723";
    private const string Kharkiv = "UA63120270010096107";
    private const string KyivCity = "UA80000000000093317";
    private const string KyivHolosiivskyi = "UA80000000000126643";
    private const string Prypiat = "UA32000000010085013";
    private const string SevastopolBalaklavskyi = "UA85000000000155841";

    [Fact]
    public void Counts()
    {
        Assert.Equal(FixtureData.RowCount, Data.Count);
        Assert.Equal(FixtureData.RowCount, Data.All.Count);
        Assert.Equal(9, Data.Regions.Count);
        Assert.All(Data.Regions, r => Assert.Null(r.Parent));
        Assert.Equal(new DateOnly(2026, 11, 3), Data.Edition.Date);
    }

    [Fact]
    public void Find_ExactCode()
    {
        var unit = Data.Find(KatottgCode.Parse(Andriivka));
        Assert.NotNull(unit);
        Assert.Equal("Андріївка", unit.Name);
        Assert.Equal(KatottgCategory.Village, unit.Category);

        Assert.Null(Data.Find(KatottgCode.Parse("UA01020010010000000")));
    }

    [Fact]
    public void FindBySignificantPart_IgnoresTail()
    {
        var unit = Data.FindBySignificantPart(KatottgCode.Parse("UA01020010010000000"));
        Assert.NotNull(unit);
        Assert.Equal(Andriivka, unit.Code.ToString());

        Assert.Null(Data.FindBySignificantPart(KatottgCode.Parse("UA99000000000000000")));
    }

    [Fact]
    public void TryFind_Text()
    {
        Assert.True(Data.TryFind(" ua01020010010075540 ", out var unit));
        Assert.Equal("Андріївка", unit.Name);
        Assert.False(Data.TryFind("UA01020010010000000", out unit));
        Assert.Null(unit);
        Assert.False(Data.TryFind("not a code", out _));
    }

    [Fact]
    public void Hierarchy_CityDistrictInsideCity()
    {
        var unit = Data.Unit(KharkivIndustrialnyi);

        Assert.Equal(KatottgCategory.CityDistrict, unit.Category);
        Assert.Equal(4, unit.Depth);
        Assert.Equal(Kharkiv, unit.Parent!.Code.ToString());
        Assert.Same(unit.Parent, unit.PopulatedPlace);
        Assert.Equal("Харківська", unit.Community!.Name);
        Assert.Equal(KatottgCategory.Community, unit.Community.Category);
        Assert.Equal("Харківський", unit.District!.Name);
        Assert.Equal("Харківська", unit.Region.Name);
        Assert.Equal(KatottgCategory.Region, unit.Region.Category);
        Assert.Equal(["Харків", "Харківська", "Харківський", "Харківська"], unit.Ancestors().Select(a => a.Name));
    }

    [Fact]
    public void Hierarchy_SpecialStatusCity()
    {
        var kyiv = Data.Unit(KyivCity);
        var district = Data.Unit(KyivHolosiivskyi);

        Assert.Equal(0, kyiv.Depth);
        Assert.Same(kyiv, kyiv.Region);
        Assert.Same(kyiv, kyiv.PopulatedPlace);
        Assert.Null(kyiv.District);
        Assert.Null(kyiv.Community);

        Assert.Same(kyiv, district.Parent);
        Assert.Equal(1, district.Depth);
        Assert.Same(kyiv, district.Region);
        Assert.Same(kyiv, district.PopulatedPlace);
        Assert.Null(district.District);
        Assert.Null(district.Community);
        Assert.Equal(2, kyiv.Children.Count);
    }

    [Fact]
    public void Hierarchy_ExclusionZoneCity()
    {
        var prypiat = Data.Unit(Prypiat);

        Assert.Equal(1, prypiat.Depth);
        Assert.Equal("Київська", prypiat.Region.Name);
        Assert.Same(prypiat.Region, prypiat.Parent);
        Assert.Null(prypiat.District);
        Assert.Null(prypiat.Community);
        Assert.Same(prypiat, prypiat.PopulatedPlace);
    }

    [Fact]
    public void Hierarchy_UpperLevelsHaveNoPopulatedPlace()
    {
        var community = Data.Unit("UA01020010000048857");
        Assert.Null(community.PopulatedPlace);
        Assert.Same(community, community.Community);
        Assert.Equal("Бахчисарайський", community.District!.Name);
        Assert.Same(community.District, community.Parent);
    }

    [Theory]
    [InlineData(Andriivka, "с. Андріївка, Андріївська громада, Бахчисарайський р-н, АР Крим")]
    [InlineData(KharkivIndustrialnyi, "Індустріальний р-н, м. Харків, Харківська громада, Харківський р-н, Харківська обл.")]
    [InlineData(KyivHolosiivskyi, "Голосіївський р-н, м. Київ")]
    [InlineData(Prypiat, "м. Прип’ять, Київська обл.")]
    [InlineData("UA68040030010028083", "смт Війтівці, Вiйтовецька громада, Хмельницький р-н, Хмельницька обл.")]
    [InlineData("UA05020110350031399", "с-ще Липовець, Липовецька громада, Вінницький р-н, Вінницька обл.")]
    public void FullName(string code, string expected) => Assert.Equal(expected, Data.Unit(code).FullName);

    [Fact]
    public void DisplayName_And_ToString()
    {
        var unit = Data.Unit(Andriivka);
        Assert.Equal("с. Андріївка", unit.DisplayName);
        Assert.Equal("с. Андріївка (UA01020010010075540)", unit.ToString());
        Assert.Equal("АР Крим", unit.Region.DisplayName);
    }

    [Theory]
    [InlineData(Andriivka, true)]
    [InlineData("UA01000000000013043", true)]
    [InlineData(SevastopolBalaklavskyi, true)]
    [InlineData("UA85000000000065278", true)]
    [InlineData(Kharkiv, false)]
    [InlineData(KyivCity, false)]
    public void IsTemporarilyNotInForce(string code, bool expected)
        => Assert.Equal(expected, Data.Unit(code).IsTemporarilyNotInForce);

    [Fact]
    public void Descendants_DepthFirstInFileOrder()
    {
        var region = Data.Unit("UA63000000000041885");
        Assert.Equal(["Харківський", "Харківська", "Харків", "Індустріальний", "Київський"], region.Descendants().Select(d => d.Name));
        Assert.Empty(Data.Unit(KharkivIndustrialnyi).Descendants());
        Assert.Empty(Data.Unit(KharkivIndustrialnyi).Children);
    }

    [Fact]
    public void GetByCategory()
    {
        Assert.Equal(10, Data.GetByCategory(KatottgCategory.CityDistrict).Count);
        Assert.Equal(2, Data.GetByCategory(KatottgCategory.SpecialStatusCity).Count);
        Assert.Empty(Data.GetByCategory(KatottgCategory.Unknown));
    }

    [Fact]
    public void CollectionsAreReadOnly()
    {
        Assert.False(Data.All is KatottgUnit[]);
        Assert.False(Data.Regions is KatottgUnit[]);
        Assert.False(Data.Unit(KyivCity).Children is KatottgUnit[]);
        Assert.False(Data.GetByCategory(KatottgCategory.Village) is KatottgUnit[]);
    }

    private static readonly KatottgEdition Edition = new(new DateOnly(2026, 1, 1), "test", null);
    private static readonly KatottgCode R = KatottgCode.Parse("UA05000000000010236");
    private static readonly KatottgCode D = KatottgCode.Parse("UA05020000000026686");

    [Fact]
    public void Create_RejectsDuplicateCode()
        => Assert.Throws<ArgumentException>(() => KatottgDataset.Create(Edition,
            [new(R, null, KatottgCategory.Region, "А"), new(R, null, KatottgCategory.Region, "Б")]));

    [Fact]
    public void Create_RejectsDuplicateSignificantPart()
        => Assert.Throws<ArgumentException>(() => KatottgDataset.Create(Edition,
            [new(R, null, KatottgCategory.Region, "А"), new(KatottgCode.Parse("UA05000000000099999"), null, KatottgCategory.Region, "Б")]));

    [Fact]
    public void Create_RejectsParentAfterChild()
        => Assert.Throws<ArgumentException>(() => KatottgDataset.Create(Edition,
            [new(D, R, KatottgCategory.District, "Р"), new(R, null, KatottgCategory.Region, "О")]));

    [Fact]
    public void Create_RejectsEmptyName()
        => Assert.Throws<ArgumentException>(() => KatottgDataset.Create(Edition, [new(R, null, KatottgCategory.Region, " ")]));

    [Fact]
    public void Create_Empty()
    {
        var empty = KatottgDataset.Create(Edition, []);
        Assert.Equal(0, empty.Count);
        Assert.Empty(empty.Regions);
    }
}
