namespace UkrUtils.Katottg;

/// <summary>
/// Category of a codifier object. The value equals the ASCII code of the Latin letter from the official file.
/// </summary>
/// <remarks>
/// The set is not closed (for example, category <c>T</c> is being phased out), so unknown letters
/// yield <see cref="Unknown"/>.
/// </remarks>
public enum KatottgCategory : byte
{
    /// <summary>Unknown category letter.</summary>
    Unknown = 0,

    /// <summary><c>O</c> — region, Autonomous Republic of Crimea.</summary>
    Region = (byte)'O',

    /// <summary><c>K</c> — city with special status (Kyiv, Sevastopol).</summary>
    SpecialStatusCity = (byte)'K',

    /// <summary><c>P</c> — district.</summary>
    District = (byte)'P',

    /// <summary><c>H</c> — territory of a territorial community (hromada).</summary>
    Community = (byte)'H',

    /// <summary><c>M</c> — city.</summary>
    City = (byte)'M',

    /// <summary><c>T</c> — urban-type settlement.</summary>
    UrbanTypeSettlement = (byte)'T',

    /// <summary><c>X</c> — settlement (rural-type).</summary>
    Settlement = (byte)'X',

    /// <summary><c>C</c> — village.</summary>
    Village = (byte)'C',

    /// <summary><c>B</c> — city district.</summary>
    CityDistrict = (byte)'B',
}

/// <summary>Helper methods for <see cref="KatottgCategory"/>.</summary>
public static class KatottgCategoryExtensions
{
    /// <summary>Populated place: city, urban-type settlement, settlement, village, or city with special status.</summary>
    public static bool IsPopulatedPlace(this KatottgCategory category) => category is
        KatottgCategory.City or KatottgCategory.UrbanTypeSettlement or KatottgCategory.Settlement or
        KatottgCategory.Village or KatottgCategory.SpecialStatusCity;

    /// <summary>Ukrainian name of the category: "село" (village), "місто" (city), and so on.</summary>
    public static string ToUkrainianName(this KatottgCategory category) => category switch
    {
        KatottgCategory.Region => "область",
        KatottgCategory.SpecialStatusCity => "місто зі спеціальним статусом",
        KatottgCategory.District => "район",
        KatottgCategory.Community => "територіальна громада",
        KatottgCategory.City => "місто",
        KatottgCategory.UrbanTypeSettlement => "селище міського типу",
        KatottgCategory.Settlement => "селище",
        KatottgCategory.Village => "село",
        KatottgCategory.CityDistrict => "район у місті",
        _ => "невідома категорія",
    };

    /// <summary>
    /// Abbreviation written <b>before</b> the name: "с.", "м.", "смт", "с-ще".
    /// Empty string for regions, districts, and communities (see <see cref="ToShortSuffix"/>).
    /// </summary>
    public static string ToShortPrefix(this KatottgCategory category) => category switch
    {
        KatottgCategory.City or KatottgCategory.SpecialStatusCity => "м.",
        KatottgCategory.UrbanTypeSettlement => "смт",
        KatottgCategory.Settlement => "с-ще",
        KatottgCategory.Village => "с.",
        _ => "",
    };

    /// <summary>
    /// Abbreviation written <b>after</b> the name: "обл." (region), "р-н" (district), "громада" (community).
    /// Empty string for populated places (see <see cref="ToShortPrefix"/>).
    /// </summary>
    public static string ToShortSuffix(this KatottgCategory category) => category switch
    {
        KatottgCategory.Region => "обл.",
        KatottgCategory.District or KatottgCategory.CityDistrict => "р-н",
        KatottgCategory.Community => "громада",
        _ => "",
    };

    /// <summary>
    /// Category by the letter from the official file. Accepts the Latin letter and the visually identical Cyrillic one
    /// in either case; returns <see cref="KatottgCategory.Unknown"/> for any other character.
    /// </summary>
    public static KatottgCategory FromLetter(char letter) => char.ToUpperInvariant(letter) switch
    {
        'O' or 'О' => KatottgCategory.Region,
        'K' or 'К' => KatottgCategory.SpecialStatusCity,
        'P' or 'Р' => KatottgCategory.District,
        'H' or 'Н' => KatottgCategory.Community,
        'M' or 'М' => KatottgCategory.City,
        'T' or 'Т' => KatottgCategory.UrbanTypeSettlement,
        'X' or 'Х' => KatottgCategory.Settlement,
        'C' or 'С' => KatottgCategory.Village,
        'B' or 'В' => KatottgCategory.CityDistrict,
        _ => KatottgCategory.Unknown,
    };

    /// <summary>Latin category letter as in the official file; <c>'\0'</c> for <see cref="KatottgCategory.Unknown"/>.</summary>
    public static char ToLetter(this KatottgCategory category) => (char)(byte)category;
}
