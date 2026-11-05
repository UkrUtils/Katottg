using UkrUtils.Katottg.Tests.Fixtures;

namespace UkrUtils.Katottg.Tests.Search;

/// <summary>Регресійні тести за результатами рев’ю пошуку адрес.</summary>
public class AddressSearchRegressionTests
{
    private static KatottgDataset Data => FixtureDataset.Official;

    private static string? BestCode(string text) => Data.Search(text).Best?.Unit.Code.ToString();

    [Theory]
    [InlineData("Львівський р-н, Львівська обл.", "UA46060000000042587")]
    [InlineData("Щастинський р-н, Луганська обл.", "UA44160000000057142")]
    public void MarkerHonouringCombination_BeatsFallback(string text, string expected)
        => Assert.Equal(expected, BestCode(text));

    [Fact]
    public void CommunityWithDistrictContext_IsCommunity()
    {
        var best = Data.Search("Кропивницька громада, Кропивницький р-н").Best!;
        Assert.Equal(KatottgCategory.Community, best.Unit.Category);
        Assert.Equal("Кропивницька", best.Unit.Name);
    }

    [Fact]
    public void EveryUnit_IsFoundByItsFullName()
    {
        var failures = new List<string>();
        foreach (var unit in Data.All)
        {
            var best = Data.Search(unit.FullName).Best;
            // Однакові FullName (тезки однієї категорії в одній громаді) — нерозрізненні, це нормально.
            if (best is null || best.Unit.FullName != unit.FullName)
                failures.Add($"{unit.FullName} -> {best?.Unit.FullName ?? "nothing"}");
        }
        Assert.True(failures.Count == 0, $"{failures.Count} failures:\n" + string.Join("\n", failures.Take(30)));
    }

    [Theory]
    [InlineData("Селище")]
    [InlineData("Громада")]
    public void NameEqualToMarker_IsFound(string name)
        => Assert.Contains(Data.Search(name).Hits, h => h.Unit.Name == name && h.Kind == MatchKind.Exact);

    [Theory]
    [InlineData("С")]
    [InlineData("М")]
    [InlineData("Сел")]
    [InlineData("Обл")]
    public void AutocompleteHasNoDeadSpots(string text) => Assert.NotEmpty(Data.Search(text).Hits);

    [Theory]
    [InlineData("м. Київ, вул. Хрещатик", "UA80000000000093317")]
    [InlineData("01001, м. Київ, вул. Хрещатик, 1", "UA80000000000093317")]
    [InlineData("Кривий Ріг, Дніпропетровська обл., 50000", "UA12060170010065850")]
    [InlineData("с. Андріївка, Бахчисарайський р-н, АР Крим, вул. Шкільна 5", "UA01020010010075540")]
    public void UnrecognisedAddressParts_AreIgnored(string text, string expected)
        => Assert.Equal(expected, BestCode(text));

    [Fact]
    public void SettlementMarker_PrefersSettlementOverUrbanType()
    {
        Assert.Equal("UA05060050320051164", BestCode("с-ще Браїлів"));
        Assert.Equal("UA05060050020042300", BestCode("смт Браїлів"));
    }
}
