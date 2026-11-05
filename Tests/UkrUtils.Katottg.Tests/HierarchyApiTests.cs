using UkrUtils.Katottg.Tests.Fixtures;

namespace UkrUtils.Katottg.Tests;

/// <summary>Зручне API ієрархії: шлях, пласка адреса, перевірки за кодом, дерево для відображення.</summary>
public class HierarchyApiTests
{
    private static KatottgDataset Fixture => FixtureDataset.Value;
    private static KatottgDataset Official => FixtureDataset.Official;

    private const string Andriivka = "UA01020010010075540";
    private const string KharkivIndustrialnyi = "UA63120270010158723";
    private const string KyivHolosiivskyi = "UA80000000000126643";
    private const string Prypiat = "UA32000000010085013";

    [Fact]
    public void Path_FromRootToSelf()
    {
        Assert.Equal(["Автономна Республіка Крим", "Бахчисарайський", "Андріївська", "Андріївка"],
            Fixture.Unit(Andriivka).Path.Select(u => u.Name));
        Assert.Equal(["Київ", "Голосіївський"], Fixture.Unit(KyivHolosiivskyi).Path.Select(u => u.Name));
        var root = Fixture.Unit("UA80000000000093317");
        Assert.Same(root, Assert.Single(root.Path));
    }

    [Fact]
    public void CityDistrict_IsSelfForB_NullOtherwise()
    {
        var b = Fixture.Unit(KharkivIndustrialnyi);
        Assert.Same(b, b.CityDistrict);
        Assert.Null(b.Parent!.CityDistrict);
        Assert.Null(Fixture.Unit(Andriivka).CityDistrict);
    }

    [Fact]
    public void ToAddress_Village()
    {
        var address = Fixture.Unit(Andriivka).ToAddress();

        Assert.Equal(KatottgCode.Parse(Andriivka), address.Code);
        Assert.Equal(KatottgCategory.Village, address.Category);
        Assert.Equal("Андріївка", address.Name);
        Assert.Equal("с. Андріївка, Андріївська громада, Бахчисарайський р-н, АР Крим", address.FullName);
        Assert.Equal("UA01000000000013043", address.Region.Code.ToString());
        Assert.Equal("АР Крим", address.Region.DisplayName);
        Assert.Equal("UA01020000000022387", address.District!.Code.ToString());
        Assert.Equal("Бахчисарайський", address.District.Name);
        Assert.Equal("UA01020010000048857", address.Community!.Code.ToString());
        Assert.Equal(KatottgCategory.Community, address.Community.Category);
        Assert.Equal(Andriivka, address.PopulatedPlace!.Code.ToString());
        Assert.Null(address.CityDistrict);
        Assert.True(address.IsTemporarilyNotInForce);
    }

    [Fact]
    public void ToAddress_CityDistrict()
    {
        var address = Fixture.Unit(KharkivIndustrialnyi).ToAddress();
        Assert.Equal("Харківська", address.Region.Name);
        Assert.Equal("Харківський", address.District!.Name);
        Assert.Equal("Харківська", address.Community!.Name);
        Assert.Equal("Харків", address.PopulatedPlace!.Name);
        Assert.Equal(KharkivIndustrialnyi, address.CityDistrict!.Code.ToString());
        Assert.False(address.IsTemporarilyNotInForce);
    }

    [Fact]
    public void ToAddress_MissingLevelsAreNull()
    {
        var prypiat = Fixture.Unit(Prypiat).ToAddress();
        Assert.Equal("Київська", prypiat.Region.Name);
        Assert.Null(prypiat.District);
        Assert.Null(prypiat.Community);
        Assert.Equal("Прип’ять", prypiat.PopulatedPlace!.Name);

        var kyivDistrict = Fixture.Unit(KyivHolosiivskyi).ToAddress();
        Assert.Equal("Київ", kyivDistrict.Region.Name);
        Assert.Equal("Київ", kyivDistrict.PopulatedPlace!.Name);
        Assert.Null(kyivDistrict.District);
    }

    [Fact]
    public void ToAddress_HasValueEquality()
        => Assert.Equal(Fixture.Unit(Andriivka).ToAddress(), Fixture.Unit(Andriivka).ToAddress());

    [Fact]
    public void SearchHit_ExposesFullHierarchy()
    {
        var hit = Official.Search("с. Андріївка, Бахчисарайський р-н").Best!;
        var address = hit.Unit.ToAddress();
        Assert.Equal("АР Крим", address.Region.DisplayName);
        Assert.Equal("Андріївська", address.Community!.Name);
    }

    // ---- Перевірки за самим кодом (без набору даних) ----

    [Fact]
    public void IsDescendantOf_MatchesTreeForEveryRegionAndDistrict()
    {
        var ancestors = Official.GetByCategory(KatottgCategory.Region)
            .Concat(Official.GetByCategory(KatottgCategory.SpecialStatusCity))
            .Concat(Official.GetByCategory(KatottgCategory.District))
            .ToList();
        foreach (var unit in Official.All)
        {
            foreach (var ancestor in ancestors)
            {
                var expected = !ReferenceEquals(unit, ancestor) && ancestor.Contains(unit);
                if (unit.Code.IsDescendantOf(ancestor.Code) != expected)
                    Assert.Fail($"{unit} IsDescendantOf {ancestor}: expected {expected}");
            }
        }
    }

    [Fact]
    public void IsDescendantOf_AllAncestors_AndNotSelfOrChild()
    {
        foreach (var unit in Official.All)
        {
            Assert.False(unit.Code.IsDescendantOf(unit.Code));
            foreach (var ancestor in unit.Ancestors())
            {
                Assert.True(unit.Code.IsDescendantOf(ancestor.Code), $"{unit} under {ancestor}");
                Assert.False(ancestor.Code.IsDescendantOf(unit.Code));
            }
        }
        Assert.False(KatottgCode.Parse(Andriivka).IsDescendantOf(default));
    }

    [Theory]
    [InlineData("UA01000000000013043", "UA01")]
    [InlineData("UA01020000000022387", "UA0102")]
    [InlineData("UA01020010000048857", "UA0102001")]
    [InlineData("UA01020010010075540", "UA0102001001")]
    [InlineData("UA63120270010158723", "UA631202700101")]
    [InlineData("UA80000000000093317", "UA80")]
    [InlineData("UA80000000000126643", "UA800000000001")]
    public void HierarchyPrefix(string code, string expected)
        => Assert.Equal(expected, KatottgCode.Parse(code).HierarchyPrefix);

    [Fact]
    public void HierarchyPrefix_SelectsExactlyTheSubtree()
    {
        foreach (var root in Official.GetByCategory(KatottgCategory.Region).Concat(Official.GetByCategory(KatottgCategory.District)))
        {
            var prefix = root.Code.HierarchyPrefix;
            var byPrefix = Official.All.Count(u => u.Code.ToString().StartsWith(prefix, StringComparison.Ordinal));
            Assert.Equal(1 + root.Descendants().Count(), byPrefix);
        }
    }

    [Fact]
    public void HierarchyPrefix_SelectsExactlyTheSubtree_ForEveryUnit()
    {
        // Прип’ять (UA32 00 000 001 00) — приклад, де посегментне порівняння дало б хибних «нащадків».
        var codes = Official.All.Select(u => u.Code.ToString()).Order(StringComparer.Ordinal).ToArray();
        foreach (var unit in Official.All)
        {
            var prefix = unit.Code.HierarchyPrefix;
            var lo = LowerBound(codes, prefix);
            var hi = lo;
            while (hi < codes.Length && codes[hi].StartsWith(prefix, StringComparison.Ordinal))
                hi++;
            var expected = 1 + unit.Descendants().Count();
            if (hi - lo != expected)
                Assert.Fail($"{unit}: prefix {prefix} selects {hi - lo}, subtree has {expected}");
        }

        var prypiat = Official.Unit(Prypiat);
        Assert.DoesNotContain(Official.All, u => u.Code.IsDescendantOf(prypiat.Code));

        static int LowerBound(string[] sorted, string value)
        {
            int lo = 0, hi = sorted.Length;
            while (lo < hi)
            {
                var mid = (lo + hi) / 2;
                if (string.CompareOrdinal(sorted[mid], value) < 0) lo = mid + 1; else hi = mid;
            }
            return lo;
        }
    }

    // ---- Дерево для відображення ----

    [Fact]
    public void BuildTree_PrunedToMatchesAndAncestors()
    {
        var hits = Official.Search("Андріївка", new KatottgSearchOptions { Limit = 200 }).Hits
            .Where(h => h.Kind == MatchKind.Exact).Select(h => h.Unit).ToList();

        var roots = KatottgTreeNode.Build(hits);

        var all = roots.SelectMany(r => r.DescendantsAndSelf()).ToList();
        Assert.Equal(hits.Count, all.Count(n => n.IsMatch));
        Assert.Equal(hits.ToHashSet(), all.Where(n => n.IsMatch).Select(n => n.Unit).ToHashSet());
        Assert.All(roots, r => Assert.Null(r.Unit.Parent));
        Assert.All(all.Where(n => !n.IsMatch), n => Assert.NotEmpty(n.Children));
        Assert.All(all, n => Assert.All(n.Children, c => Assert.Same(n.Unit, c.Unit.Parent)));
        // Порядок — як в офіційному файлі
        Assert.Equal(roots.Select(r => r.Unit.Code).Order(), roots.Select(r => r.Unit.Code));
        // Кожен об’єкт — один раз
        Assert.Equal(all.Count, all.Select(n => n.Unit).Distinct().Count());
    }

    [Fact]
    public void BuildTree_MatchThatIsAlsoAncestor()
    {
        var community = Fixture.Unit("UA01020010000048857");
        var village = Fixture.Unit(Andriivka);

        var root = Assert.Single(KatottgTreeNode.Build([village, community, village]));
        var communityNode = root.Children.Single().Children.Single();
        Assert.True(communityNode.IsMatch);
        Assert.True(Assert.Single(communityNode.Children).IsMatch);
        Assert.False(root.IsMatch);
        Assert.Equal(2, communityNode.Unit.Depth);
    }

    [Fact]
    public void BuildTree_IncludeDescendants()
    {
        var community = Fixture.Unit("UA01020010000048857");
        var root = Assert.Single(KatottgTreeNode.Build([community], includeDescendants: true));
        var communityNode = root.Children.Single().Children.Single();
        Assert.Equal(community.Children.Count, communityNode.Children.Count);
        Assert.All(communityNode.Children, c => Assert.False(c.IsMatch));
    }

    [Fact]
    public void BuildTree_FullDataset()
    {
        var roots = KatottgTreeNode.Build(Official.Regions, includeDescendants: true);
        Assert.Equal(27, roots.Count);
        Assert.Equal(Official.Count, roots.Sum(r => r.DescendantsAndSelf().Count()));
    }

    [Fact]
    public void BuildTree_Empty() => Assert.Empty(KatottgTreeNode.Build([]));

    [Fact]
    public void SearchResult_ToTree()
    {
        var result = Official.Search("Шевченківський р-н, м. Харків");
        var root = Assert.Single(result.ToTree());
        Assert.Equal("Харківська", root.Unit.Name);
        var leaf = root.DescendantsAndSelf().Single(n => n.IsMatch);
        Assert.Equal(KatottgCategory.CityDistrict, leaf.Unit.Category);
        Assert.Equal(["Харківська", "Харківський", "Харківська", "Харків", "Шевченківський"],
            root.DescendantsAndSelf().Select(n => n.Unit.Name));
    }
}
