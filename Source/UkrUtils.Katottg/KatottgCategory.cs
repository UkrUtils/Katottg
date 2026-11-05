namespace UkrUtils.Katottg;

/// <summary>
/// Категорія об’єкта кодифікатора. Значення збігається з ASCII-кодом латинської літери з офіційного файлу.
/// </summary>
/// <remarks>
/// Набір не закритий (наприклад, категорію <c>T</c> поступово скасовують), тому невідомі літери
/// дають <see cref="Unknown"/>.
/// </remarks>
public enum KatottgCategory : byte
{
    /// <summary>Невідома літера категорії.</summary>
    Unknown = 0,

    /// <summary><c>O</c> — область, Автономна Республіка Крим.</summary>
    Region = (byte)'O',

    /// <summary><c>K</c> — місто зі спеціальним статусом (Київ, Севастополь).</summary>
    SpecialStatusCity = (byte)'K',

    /// <summary><c>P</c> — район.</summary>
    District = (byte)'P',

    /// <summary><c>H</c> — територія територіальної громади.</summary>
    Community = (byte)'H',

    /// <summary><c>M</c> — місто.</summary>
    City = (byte)'M',

    /// <summary><c>T</c> — селище міського типу.</summary>
    UrbanTypeSettlement = (byte)'T',

    /// <summary><c>X</c> — селище.</summary>
    Settlement = (byte)'X',

    /// <summary><c>C</c> — село.</summary>
    Village = (byte)'C',

    /// <summary><c>B</c> — район у місті.</summary>
    CityDistrict = (byte)'B',
}

/// <summary>Допоміжні методи для <see cref="KatottgCategory"/>.</summary>
public static class KatottgCategoryExtensions
{
    /// <summary>Населений пункт: місто, СМТ, селище, село або місто зі спеціальним статусом.</summary>
    public static bool IsPopulatedPlace(this KatottgCategory category) => category is
        KatottgCategory.City or KatottgCategory.UrbanTypeSettlement or KatottgCategory.Settlement or
        KatottgCategory.Village or KatottgCategory.SpecialStatusCity;

    /// <summary>Українська назва категорії: «село», «місто»…</summary>
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
    /// Скорочення, яке пишуть <b>перед</b> назвою: «с.», «м.», «смт», «с-ще».
    /// Для областей, районів і громад — порожній рядок (див. <see cref="ToShortSuffix"/>).
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
    /// Скорочення, яке пишуть <b>після</b> назви: «обл.», «р-н», «громада».
    /// Для населених пунктів — порожній рядок (див. <see cref="ToShortPrefix"/>).
    /// </summary>
    public static string ToShortSuffix(this KatottgCategory category) => category switch
    {
        KatottgCategory.Region => "обл.",
        KatottgCategory.District or KatottgCategory.CityDistrict => "р-н",
        KatottgCategory.Community => "громада",
        _ => "",
    };

    /// <summary>
    /// Категорія за літерою з офіційного файлу. Приймає латиницю і візуально однакову кирилицю
    /// в обох регістрах; для інших символів повертає <see cref="KatottgCategory.Unknown"/>.
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

    /// <summary>Латинська літера категорії, як в офіційному файлі; <c>'\0'</c> для <see cref="KatottgCategory.Unknown"/>.</summary>
    public static char ToLetter(this KatottgCategory category) => (char)(byte)category;
}
