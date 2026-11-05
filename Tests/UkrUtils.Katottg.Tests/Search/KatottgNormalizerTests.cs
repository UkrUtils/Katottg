using UkrUtils.Katottg.Search;

namespace UkrUtils.Katottg.Tests.Search;

public class KatottgNormalizerTests
{
    [Theory]
    [InlineData("Кам’янське", "камянське")]   // U+2019
    [InlineData("Кам'янське", "камянське")]   // ASCII
    [InlineData("Камʼянське", "камянське")]   // U+02BC
    [InlineData("Кам`янське", "камянське")]
    [InlineData("Кам‘янське", "камянське")]   // U+2018
    [InlineData("Камянське", "камянське")]
    public void Apostrophes_AreRemovedByDefault(string input, string expected)
        => Assert.Equal(expected, KatottgNormalizer.Normalize(input));

    [Theory]
    [InlineData("Кам’янське", "камʼянське")]
    [InlineData("Кам'янське", "камʼянське")]
    [InlineData("'Київ'", "київ")] // лапки по краях слова — не апостроф
    public void Apostrophes_CanBeUnified(string input, string expected)
        => Assert.Equal(expected, KatottgNormalizer.Normalize(input, ApostropheHandling.Unify));

    [Theory]
    [InlineData("Кривий-Ріг", "кривий ріг")]
    [InlineData("  Кривий   Ріг. ", "кривий ріг")]
    [InlineData("м.Київ", "м київ")]
    [InlineData("с-ще Сонячний", "с ще сонячний")]
    [InlineData("Кривий–Ріг", "кривий ріг")] // en dash
    [InlineData("", "")]
    [InlineData("  .,- ", "")]
    public void Separators_CollapseToSingleSpace(string input, string expected)
        => Assert.Equal(expected, KatottgNormalizer.Normalize(input));

    [Theory]
    [InlineData("Вiйтовецька", "війтовецька")] // «Вiйтовецька», латинська i
    [InlineData("Хaрків", "харків")]                                     // латинська a
    [InlineData("Cоняшник", "соняшник")]                       // латинська C
    [InlineData("Kиïв", "київ")]                                                    // латинські K та ï
    public void MixedScriptWords_MapLatinLookalikesToCyrillic(string input, string expected)
    {
        var normalized = KatottgNormalizer.Normalize(input);
        Assert.Equal(expected, normalized);
        Assert.DoesNotContain(normalized, c => c < 0x80 && char.IsLetter(c));
    }

    [Theory]
    [InlineData("Kyiv", "kyiv")]
    [InlineData("Zaporizhzhia", "zaporizhzhia")]
    [InlineData("Kyiv Київ", "kyiv київ")]
    public void PurelyLatinWords_AreKept(string input, string expected)
        => Assert.Equal(expected, KatottgNormalizer.Normalize(input));

    [Theory]
    [InlineData("Ґорґани", "горгани")]
    [InlineData("Ёлкино", "елкино")]
    [InlineData("Крыжополь", "крижополь")]
    [InlineData("Эдем", "едем")]
    [InlineData("Подъячево", "подячево")]
    public void RussianLayoutAndGhe_AreFolded(string input, string expected)
        => Assert.Equal(expected, KatottgNormalizer.Normalize(input));

    [Fact]
    public void DecomposedCharacters_AreComposed()
    {
        // «Київ» з ї як і + U+0308, «Гайове» з й як и + U+0306
        Assert.Equal("київ", KatottgNormalizer.Normalize("Київ"));
        Assert.Equal("гайове", KatottgNormalizer.Normalize("Гайове"));
    }

    [Fact]
    public void Digits_AreKept() => Assert.Equal("1 травня", KatottgNormalizer.Normalize("1-Травня"));

    [Fact]
    public void Null_Throws() => Assert.Throws<ArgumentNullException>(() => KatottgNormalizer.Normalize(null!));
}
