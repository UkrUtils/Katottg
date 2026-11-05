namespace UkrUtils.Katottg.Tests;

/// <summary>
/// Приклади з README.md і Docs/AgentGuide.md. Якщо тест падає — оновіть документацію разом із кодом.
/// </summary>
public class DocsExamplesTests
{
    private static KatottgDataset Katottg => KatottgDataset.Embedded;

    [Fact]
    public void QuickStart()
    {
        var unit = Katottg.Find(KatottgCode.Parse("UA01020010010075540"));
        Assert.Equal("с. Андріївка, Андріївська громада, Бахчисарайський р-н, АР Крим", unit!.FullName);

        var hit = Katottg.Search("смт Андріївка, Бердянський р-н, Запорізька обл.").Best!;
        Assert.Equal("UA23020030010042079 смт Андріївка, Андріївська громада, Бердянський р-н, Запорізька обл.",
            $"{hit.Unit.Code} {hit.Unit.FullName}");
        Assert.Equal(new DateOnly(2026, 11, 3), Katottg.Edition.Date);
        Assert.Equal(31_751, Katottg.Count);
    }

    [Fact]
    public void CodeSection()
    {
        var code = KatottgCode.Parse(" ua01020030010043419 ");
        Assert.Equal(1, code.Region);
        Assert.Equal(010200300100UL, code.SignificantPart);
        Assert.Equal("UA0102003001", code.HierarchyPrefix);
        Assert.True(code.IsDescendantOf(KatottgCode.Parse("UA01020000000022387")));
        Assert.Equal("UA01020030000000000", code.ParentPrefix.ToString());
    }

    [Fact]
    public void HierarchySection()
    {
        var district = Katottg.Find(KatottgCode.Parse("UA63120270010948820"))!;
        Assert.Equal(KatottgCategory.CityDistrict, district.Category);
        Assert.Equal("Шевченківський р-н", district.DisplayName);
        Assert.Equal("м. Харків", district.Parent!.DisplayName);
        Assert.Equal("Харківська обл.", district.Region.DisplayName);
        Assert.Equal("Харківський р-н", district.District!.DisplayName);
        Assert.Equal("Харківська громада", district.Community!.DisplayName);
        Assert.Equal("м. Харків", district.PopulatedPlace!.DisplayName);
        Assert.Same(district, district.CityDistrict);
        Assert.Equal(["Харківська", "Харківський", "Харківська", "Харків", "Шевченківський"], district.Path.Select(u => u.Name));
    }

    [Fact]
    public void DatabaseSection_PrefixOfBerdianskyiDistrict()
    {
        var berdianskyi = Katottg.Find(KatottgCode.Parse("UA23020000000032289"))!;
        Assert.Equal("UA2302", berdianskyi.Code.HierarchyPrefix);

        var code = KatottgCode.Parse("UA23020030010042079");
        Assert.Equal(code, KatottgCode.FromValue((ulong)(long)code.Value));
    }

    [Fact]
    public void SearchSection()
    {
        Assert.Equal(KatottgCategory.City, Katottg.Search("Кривий Ріг").Best!.Unit.Category);
        Assert.Equal("Кам’янське", Katottg.Search("кам'янське").Best!.Unit.Name);
        Assert.Equal(MatchKind.Prefix, Katottg.Search("Запор").Best!.Kind);
        Assert.Equal("Харків", Katottg.Search("Шевченківський р-н, м. Харків").Best!.Unit.Parent!.Name);
        Assert.Equal(KatottgCategory.SpecialStatusCity, Katottg.Search("01001, м. Київ, вул. Хрещатик, 1").Best!.Unit.Category);
        Assert.Equal(MatchKind.Code, Katottg.Search("UA01020010010000000").Best!.Kind);

        var villages = Katottg.Search("Андріївка", new KatottgSearchOptions
        {
            Limit = 50,
            Categories = [KatottgCategory.Village],
            Within = Katottg.Regions.Single(r => r.Name == "Запорізька"),
        }).Hits;
        Assert.NotEmpty(villages);

        var hits = Katottg.Search(new KatottgQuery { Name = "Андріївка", Region = "Запорізька", District = "Бердянський" });
        Assert.Equal("UA23020030010042079", hits[0].Unit.Code.ToString());
    }

    [Fact]
    public void TreeSection()
    {
        var roots = Katottg.Search("Андріївка").ToTree();
        Assert.NotEmpty(roots);
        var lines = new List<string>();
        void Print(KatottgTreeNode node, int level)
        {
            lines.Add($"{new string(' ', level * 2)}{(node.IsMatch ? "* " : "")}{node.Unit.DisplayName} [{node.Unit.Code}]");
            foreach (var child in node.Children) Print(child, level + 1);
        }
        foreach (var root in roots) Print(root, 0);
        Assert.Equal(20, lines.Count(l => l.Contains("* ")));
    }
}
