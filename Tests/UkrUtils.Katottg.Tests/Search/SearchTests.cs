using UkrUtils.Katottg.Search;
using UkrUtils.Katottg.Tests.Fixtures;

namespace UkrUtils.Katottg.Tests.Search;

/// <summary>Запити з відомими відповідями на повному офіційному наборі.</summary>
public class SearchTests
{
    private static KatottgDataset Data => FixtureDataset.Official;

    private const string KryvyiRihCity = "UA12060170010065850";
    private const string KamianskeCity = "UA12040150010056523";
    private const string ViitovetskaCommunity = "UA68040030000027335";
    private const string KyivCity = "UA80000000000093317";
    private const string KyivVillage = "UA48040230080020671";
    private const string ZaporizhzhiaCity = "UA23060070010069526";
    private const string AndriivkaBerdianskyi = "UA23020030010042079";

    private static KatottgSearchHit Best(string text, KatottgSearchOptions? options = null)
    {
        var result = Data.Search(text, options);
        Assert.NotNull(result.Best);
        return result.Best;
    }

    [Theory]
    [InlineData("Кривий Ріг", KryvyiRihCity)]
    [InlineData("кривий-ріг", KryvyiRihCity)]
    [InlineData("  КРИВИЙ  РІГ ", KryvyiRihCity)]
    [InlineData("Камянське", KamianskeCity)]
    [InlineData("Кам'янське", KamianskeCity)]
    [InlineData("Вiйтовецька", ViitovetskaCommunity)] // латинська i
    [InlineData("Війтовецька", ViitovetskaCommunity)]
    [InlineData("Київ", KyivCity)]
    [InlineData("м. Київ", KyivCity)]
    [InlineData("с. Київ", KyivVillage)]
    [InlineData("Запоріжжя", ZaporizhzhiaCity)]
    public void ExactName_BestHit(string text, string expectedCode)
    {
        var best = Best(text);
        Assert.Equal(expectedCode, best.Unit.Code.ToString());
        Assert.Equal(MatchKind.Exact, best.Kind);
    }

    [Fact]
    public void City_RanksAboveSameNamedVillage()
    {
        var hits = Data.Search("Кривий Ріг").Hits;
        Assert.Equal(KatottgCategory.City, hits[0].Unit.Category);
        Assert.Contains(hits, h => h.Unit.Category == KatottgCategory.Village && h.Unit.Name == "Кривий Ріг");
        Assert.True(hits[0].Score > hits.First(h => h.Unit.Category == KatottgCategory.Village).Score);

        var kyiv = Data.Search("Київ").Hits;
        Assert.Equal(KyivCity, kyiv[0].Unit.Code.ToString());
        Assert.Contains(kyiv, h => h.Unit.Code.ToString() == KyivVillage);
    }

    [Fact]
    public void SameNameInOneCommunity_CityFirst()
    {
        var hits = Data.Search("Липовець", new KatottgSearchOptions { Limit = 50 }).Hits
            .Where(h => h.Unit.Category.IsPopulatedPlace() && h.Unit.Community?.Name == "Липовецька").ToList();
        Assert.Equal([KatottgCategory.City, KatottgCategory.Settlement], hits.Select(h => h.Unit.Category));
    }

    [Fact]
    public void Prefix_Autocomplete()
    {
        var result = Data.Search("Запоріж");
        Assert.Equal(ZaporizhzhiaCity, result.Best!.Unit.Code.ToString());
        Assert.Equal(MatchKind.Prefix, result.Best.Kind);
        Assert.All(result.Hits, h => Assert.Contains(
            KatottgNormalizer.Normalize(h.Unit.Name).Split(' '), word => word.StartsWith("запоріж", StringComparison.Ordinal)));
        Assert.All(result.Hits.TakeWhile(h => h.Kind == MatchKind.Prefix), h => Assert.StartsWith("Запоріж", h.Unit.Name));
        Assert.True(result.Hits.Count <= 20);
    }

    [Fact]
    public void ExactBeatsPrefix()
    {
        var hits = Data.Search("Київ", new KatottgSearchOptions { Limit = 100 }).Hits.ToList();
        var firstPrefix = hits.FindIndex(h => h.Kind != MatchKind.Exact);
        Assert.True(firstPrefix > 0);
        Assert.All(hits.Take(firstPrefix), h => Assert.Equal(MatchKind.Exact, h.Kind));
        Assert.All(hits.Skip(firstPrefix), h => Assert.NotEqual(MatchKind.Exact, h.Kind));
    }

    [Fact]
    public void WordPrefix_MatchesInsideHyphenatedName()
    {
        var hits = Data.Search("Подільський", new KatottgSearchOptions { Limit = 50 }).Hits;
        var mohyliv = Assert.Single(hits, h => h.Unit.Name == "Могилів-Подільський" && h.Unit.Category == KatottgCategory.City);
        Assert.Equal(MatchKind.WordPrefix, mohyliv.Kind);
    }

    [Fact]
    public void Limit_IsRespected()
    {
        Assert.Equal(5, Data.Search("Андріївка", new KatottgSearchOptions { Limit = 5 }).Hits.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => Data.Search("Андріївка", new KatottgSearchOptions { Limit = 0 }));
    }

    [Fact]
    public void CategoryOption_Filters()
    {
        var hits = Data.Search("Харківськ", new KatottgSearchOptions { Categories = [KatottgCategory.Region] }).Hits;
        var only = Assert.Single(hits);
        Assert.Equal("Харківська", only.Unit.Name);
    }

    [Fact]
    public void WithinOption_Filters()
    {
        var zaporizka = Data.Regions.Single(r => r.Name == "Запорізька");
        var hits = Data.Search("Андріївка", new KatottgSearchOptions { Within = zaporizka, Limit = 100 }).Hits;
        Assert.NotEmpty(hits);
        Assert.All(hits, h => Assert.Same(zaporizka, h.Unit.Region));
    }

    [Theory]
    [InlineData("Харківська обл.", KatottgCategory.Region)]
    [InlineData("Харківська область", KatottgCategory.Region)]
    [InlineData("Харківський р-н", KatottgCategory.District)]
    [InlineData("Харківська громада", KatottgCategory.Community)]
    [InlineData("Харківська ТГ", KatottgCategory.Community)]
    [InlineData("Харківська міська територіальна громада", KatottgCategory.Community)]
    [InlineData("м. Харків", KatottgCategory.City)]
    public void Markers_BecomeCategoryFilters(string text, KatottgCategory expected)
    {
        var hits = Data.Search(text).Hits;
        Assert.NotEmpty(hits);
        Assert.Equal(expected, hits[0].Unit.Category);
        Assert.StartsWith("Харків", hits[0].Unit.Name);
        Assert.All(hits, h => Assert.True(h.Unit.Category == expected || (expected == KatottgCategory.District && h.Unit.Category == KatottgCategory.CityDistrict)));
    }

    [Fact]
    public void Marker_NotStrippedWhenItIsTheName()
    {
        // «Нове Село» — «село» в кінці не маркер; «Село» саме по собі — назва
        Assert.Contains(Data.Search("Нове Село").Hits, h => h.Unit.Name == "Нове Село" && h.Kind == MatchKind.Exact);
    }

    [Fact]
    public void Marker_WithWrongCategory_FallsBackToAnyCategory()
    {
        // Андріївка в Бердянському районі — смт, а не село
        var best = Best("с. Андріївка, Бердянський р-н, Запорізька обл.");
        Assert.Equal(AndriivkaBerdianskyi, best.Unit.Code.ToString());
    }

    [Theory]
    [InlineData("Андріївка, Бердянський")]
    [InlineData("Андріївка, Бердянський р-н")]
    [InlineData("смт Андріївка, Бердянський р-н, Запорізька обл.")]
    [InlineData("Запорізька обл., Бердянський р-н, смт Андріївка")]
    [InlineData("Андріївка; Запорізька область; Бердянський район")]
    public void FreeText_AddressWithContext(string text)
    {
        var result = Data.Search(text);
        Assert.Equal(AndriivkaBerdianskyi, result.Best!.Unit.Code.ToString());
        Assert.All(result.Hits, h => Assert.Equal("Бердянський", h.Unit.District?.Name));
    }

    [Fact]
    public void FreeText_CityDistrictInCity()
    {
        var result = Data.Search("Шевченківський р-н, м. Харків");
        var best = result.Best!;
        Assert.Equal(KatottgCategory.CityDistrict, best.Unit.Category);
        Assert.Equal("Харків", best.Unit.Parent!.Name);
        Assert.Single(result.Hits);
    }

    [Fact]
    public void FreeText_UnresolvedContext_GivesNothing()
        => Assert.Empty(Data.Search("Андріївка, Неіснуючий р-н").Hits);

    [Fact]
    public void StructuredQuery()
    {
        var hits = Data.Search(new KatottgQuery
        {
            Name = "Андріївка",
            Region = "Запорізька",
            District = "Бердянський",
        }).ToList();

        Assert.Equal(AndriivkaBerdianskyi, hits[0].Unit.Code.ToString());
        Assert.All(hits, h => Assert.Equal("Бердянський", h.Unit.District?.Name));

        var villages = Data.Search(new KatottgQuery { Name = "Андріївка", Region = "Запорізька", Categories = [KatottgCategory.Village], Limit = 100 }).ToList();
        Assert.NotEmpty(villages);
        Assert.All(villages, h =>
        {
            Assert.Equal(KatottgCategory.Village, h.Unit.Category);
            Assert.Equal("Запорізька", h.Unit.Region.Name);
        });
    }

    [Fact]
    public void StructuredQuery_Community()
    {
        var hits = Data.Search(new KatottgQuery { Name = "Липовець", Community = "Липовецька" }).ToList();
        Assert.Equal(2, hits.Count);
        Assert.Equal(KatottgCategory.City, hits[0].Unit.Category);
    }

    [Theory]
    [InlineData("UA01020010010075540")]
    [InlineData("ua01020010010000000")] // хвіст втрачено
    public void CodeText_FindsUnit(string text)
    {
        var best = Best(text);
        Assert.Equal("UA01020010010075540", best.Unit.Code.ToString());
        Assert.Equal(MatchKind.Code, best.Kind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",,,")]
    [InlineData("Ыыыыы")]
    public void EmptyOrUnknown_GivesNoHits(string text) => Assert.Empty(Data.Search(text).Hits);

    [Fact]
    public void AutonomousRepublicAlias()
    {
        Assert.Equal("UA01000000000013043", Best("АР Крим").Unit.Code.ToString());
        Assert.Equal("UA01020010010075540", Best("с. Андріївка, Бахчисарайський р-н, АР Крим").Unit.Code.ToString());
    }

    [Fact]
    public void Results_AreDeterministic()
    {
        var a = Data.Search("Андріївка", new KatottgSearchOptions { Limit = 100 }).Hits.Select(h => h.Unit.Code);
        var b = Data.Search("Андріївка", new KatottgSearchOptions { Limit = 100 }).Hits.Select(h => h.Unit.Code);
        Assert.Equal(a, b);
    }
}
