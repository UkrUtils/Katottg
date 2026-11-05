namespace UkrUtils.Katottg.Search;

/// <summary>Частина запиту після нормалізації: текст без маркерів і категорії, які задав маркер.</summary>
internal sealed record QueryPart(string Text, string? Alias, KatottgCategory[]? Categories)
{
    public bool IsEmpty => Text.Length == 0;

    private static readonly KatottgCategory[] s_cities = [KatottgCategory.City, KatottgCategory.SpecialStatusCity];
    private static readonly KatottgCategory[] s_village = [KatottgCategory.Village];
    private static readonly KatottgCategory[] s_settlement = [KatottgCategory.Settlement, KatottgCategory.UrbanTypeSettlement];
    private static readonly KatottgCategory[] s_urbanType = [KatottgCategory.UrbanTypeSettlement];
    private static readonly KatottgCategory[] s_rural = [KatottgCategory.Village, KatottgCategory.Settlement, KatottgCategory.UrbanTypeSettlement];
    private static readonly KatottgCategory[] s_region = [KatottgCategory.Region];
    private static readonly KatottgCategory[] s_district = [KatottgCategory.District, KatottgCategory.CityDistrict];
    private static readonly KatottgCategory[] s_community = [KatottgCategory.Community];

    // Маркери перед назвою (нормалізовані токени). Довші — раніше.
    // «селище» і «с-ще» включають СМТ: закон скасовує категорію СМТ, і колишні СМТ часто пишуть як селища.
    private static readonly (string[] Tokens, KatottgCategory[] Categories)[] s_prefixMarkers =
    [
        (["селище", "міського", "типу"], s_urbanType),
        (["с", "ще"], s_settlement),
        (["смт"], s_urbanType),
        (["пгт"], s_urbanType),
        (["сще"], s_settlement),
        (["селище"], s_settlement),
        (["село"], s_village),
        (["сел"], s_rural),
        (["с"], s_village),
        (["місто"], s_cities),
        (["м"], s_cities),
    ];

    // Маркери після назви.
    private static readonly (string[] Tokens, KatottgCategory[] Categories)[] s_suffixMarkers =
    [
        (["р", "н"], s_district),
        (["рн"], s_district),
        (["район"], s_district),
        (["обл"], s_region),
        (["область"], s_region),
    ];

    private static readonly HashSet<string> s_communityWords = ["громада", "тг", "отг"];
    private static readonly HashSet<string> s_communityQualifiers = ["територіальна", "обєднана", "обʼєднана"];
    private static readonly HashSet<string> s_communityTypes = ["міська", "сільська", "селищна"];

    // Поширені скорочення, які не виводяться з назви.
    private static readonly Dictionary<string, string> s_aliases = new(StringComparer.Ordinal)
    {
        ["ар крим"] = "автономна республіка крим",
        ["арк"] = "автономна республіка крим",
    };

    /// <summary>Нормалізує частину запиту й відокремлює маркери категорії.</summary>
    public static QueryPart Parse(string raw)
    {
        var tokens = KatottgNormalizer.Normalize(raw).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        KatottgCategory[]? categories = null;
        var start = 0;
        var end = tokens.Length;

        foreach (var (marker, cats) in s_prefixMarkers)
        {
            if (StartsWith(tokens, marker))
            {
                start = marker.Length;
                categories = cats;
                break;
            }
        }

        if (categories is null)
        {
            foreach (var (marker, cats) in s_suffixMarkers)
            {
                if (EndsWith(tokens, marker))
                {
                    end = tokens.Length - marker.Length;
                    categories = cats;
                    break;
                }
            }
        }

        if (categories is null && tokens.Length > 0 && s_communityWords.Contains(tokens[^1]))
        {
            end = tokens.Length - 1;
            if (end > 0 && s_communityQualifiers.Contains(tokens[end - 1]))
                end--;
            if (end > 0 && s_communityTypes.Contains(tokens[end - 1]))
                end--;
            categories = s_community;
        }

        // Частина, що складається лише з маркера («м.»), нічого не шукає.
        var text = start < end ? string.Join(' ', tokens[start..end]) : "";
        return new QueryPart(text, s_aliases.GetValueOrDefault(text), categories);
    }

    /// <summary>Те саме, але категорії задає роль поля структурованого запиту, а не маркер.</summary>
    public static QueryPart Parse(string raw, KatottgCategory[] roleCategories)
        => Parse(raw) with { Categories = roleCategories };

    public static readonly KatottgCategory[] RegionRole = [KatottgCategory.Region, KatottgCategory.SpecialStatusCity];
    public static readonly KatottgCategory[] DistrictRole = [KatottgCategory.District];
    public static readonly KatottgCategory[] CommunityRole = [KatottgCategory.Community];

    private static bool StartsWith(string[] tokens, string[] marker)
        => tokens.Length >= marker.Length && tokens.AsSpan(0, marker.Length).SequenceEqual(marker);

    private static bool EndsWith(string[] tokens, string[] marker)
        => tokens.Length >= marker.Length && tokens.AsSpan(tokens.Length - marker.Length).SequenceEqual(marker);
}
