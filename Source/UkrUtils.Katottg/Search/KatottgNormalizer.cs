using System.Globalization;
using System.Text;

namespace UkrUtils.Katottg.Search;

/// <summary>What to do with apostrophes during normalization.</summary>
public enum ApostropheHandling
{
    /// <summary>Remove them: "Кам’янське" = "Камянське". The default.</summary>
    Remove,

    /// <summary>Unify all variants (<c>' ’ ʼ ` ‘</c>, and also <c>ъ</c>) to <c>ʼ</c> (U+02BC).</summary>
    Unify,
}

/// <summary>
/// Normalization of names for search. Applied identically to names in the index and to the query.
/// </summary>
/// <remarks>
/// <list type="number">
/// <item>Lower case; Unicode composition (NFC).</item>
/// <item>In words with mixed alphabets (such as "Вiйтовецька" with a Latin i) the Latin look-alikes
/// become Cyrillic. Words written entirely in Latin are left unchanged.</item>
/// <item>Apostrophes are removed or unified (<see cref="ApostropheHandling"/>).</item>
/// <item>Hyphens, periods, commas, and several consecutive spaces become a single space.</item>
/// <item><c>ґ→г</c>, <c>ё→е</c>, <c>ы→и</c>, <c>э→е</c>, <c>ъ→ʼ</c> — tolerance for the Russian keyboard layout.</item>
/// </list>
/// </remarks>
public static class KatottgNormalizer
{
    /// <summary>Unified apostrophe (U+02BC, modifier letter apostrophe, recommended for Ukrainian).</summary>
    public const char Apostrophe = 'ʼ';

    /// <summary>Normalizes text; see the class description.</summary>
    public static string Normalize(string text, ApostropheHandling apostrophes = ApostropheHandling.Remove)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!text.IsNormalized(NormalizationForm.FormC))
            text = text.Normalize(NormalizationForm.FormC);

        var sb = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            if (!IsWordChar(text[i]))
            {
                i++;
                continue;
            }
            var start = i;
            while (i < text.Length && IsWordChar(text[i]))
                i++;
            AppendWord(sb, text.AsSpan(start, i - start), apostrophes);
        }
        return sb.ToString();
    }

    private static void AppendWord(StringBuilder sb, ReadOnlySpan<char> word, ApostropheHandling apostrophes)
    {
        // Апострофи по краях слова — це лапки, а не частина назви.
        while (word.Length > 0 && IsApostrophe(word[0]))
            word = word[1..];
        while (word.Length > 0 && IsApostrophe(word[^1]))
            word = word[..^1];
        if (word.Length == 0)
            return;

        bool cyrillic = false, latin = false;
        foreach (var c in word)
        {
            if (IsCyrillic(c))
                cyrillic = true;
            else if (IsLatinLetter(c))
                latin = true;
        }
        var mixed = cyrillic && latin;

        var lengthBefore = sb.Length;
        if (sb.Length > 0)
            sb.Append(' ');
        var separatorLength = sb.Length - lengthBefore;

        var appended = 0;
        foreach (var original in word)
        {
            var c = mixed ? LatinToCyrillic(original) : original;
            if (IsApostrophe(c) || c is 'ъ' or 'Ъ')
            {
                if (apostrophes == ApostropheHandling.Unify)
                {
                    sb.Append(Apostrophe);
                    appended++;
                }
                continue;
            }
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            c = char.ToLowerInvariant(c) switch
            {
                'ґ' => 'г',
                'ё' => 'е',
                'ы' => 'и',
                'э' => 'е',
                var lower => lower,
            };
            sb.Append(c);
            appended++;
        }

        if (appended == 0)
            sb.Length -= separatorLength;
    }

    private static bool IsWordChar(char c)
        => char.IsLetterOrDigit(c) || IsApostrophe(c) || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark;

    internal static bool IsApostrophe(char c) => c is '\'' or '’' or 'ʼ' or '`' or '‘' or '′' or '´' or 'ʹ' or 'ʻ';

    private static bool IsCyrillic(char c) => c is >= 'Ѐ' and <= 'ӿ';

    private static bool IsLatinLetter(char c) => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= 'À' and <= 'ɏ');

    private static char LatinToCyrillic(char c) => c switch
    {
        'A' => 'А', 'B' => 'В', 'C' => 'С', 'E' => 'Е', 'H' => 'Н', 'I' => 'І', 'K' => 'К',
        'M' => 'М', 'O' => 'О', 'P' => 'Р', 'T' => 'Т', 'X' => 'Х', 'Y' => 'У',
        'a' => 'а', 'c' => 'с', 'e' => 'е', 'i' => 'і', 'k' => 'к', 'o' => 'о', 'p' => 'р',
        'x' => 'х', 'y' => 'у', 'ï' => 'ї', 'Ï' => 'Ї',
        _ => c,
    };
}
