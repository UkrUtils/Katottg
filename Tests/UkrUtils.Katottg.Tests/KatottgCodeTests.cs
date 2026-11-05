using System.Text;

namespace UkrUtils.Katottg.Tests;

public class KatottgCodeTests
{
    [Fact]
    public void Parse_ExtractsSegments()
    {
        var code = KatottgCode.Parse("UA01020030010043419");

        Assert.Equal(1, code.Region);
        Assert.Equal(2, code.District);
        Assert.Equal(3, code.Community);
        Assert.Equal(1, code.Settlement);
        Assert.Equal(0, code.CityDistrict);
        Assert.Equal(43419u, code.Tail);
        Assert.Equal(010200300100UL, code.SignificantPart);
        Assert.Equal(01020030010043419UL, code.Value);
    }

    [Theory]
    [InlineData("UA01020030010043419")]
    [InlineData("ua01020030010043419")]
    [InlineData("  UA01020030010043419 \t")]
    [InlineData("УА01020030010043419")] // кирилиця
    [InlineData("Ua01020030010043419")]
    public void Parse_AcceptsTolerantInput(string input)
    {
        Assert.True(KatottgCode.TryParse(input, out var code));
        Assert.Equal("UA01020030010043419", code.ToString());
        Assert.Equal(code, KatottgCode.Parse(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("UA")]
    [InlineData("UA0102003001004341")]   // 16 цифр
    [InlineData("UA010200300100434190")] // 18 цифр
    [InlineData("UB01020030010043419")]
    [InlineData("01020030010043419")]
    [InlineData("UA0102003001004341X")]
    [InlineData("UA 01020030010043419")]
    [InlineData("UA٠١٠٢٠٠٣٠٠١٠٠٤٣٤١٩")] // не-ASCII цифри
    public void TryParse_RejectsInvalid(string? input)
    {
        Assert.False(KatottgCode.TryParse(input, out var code));
        Assert.Equal(default, code);
        if (input is not null)
            Assert.Throws<FormatException>(() => KatottgCode.Parse(input));
    }

    [Fact]
    public void Parse_Null_Throws() => Assert.Throws<ArgumentNullException>(() => KatottgCode.Parse((string)null!));

    [Fact]
    public void ToString_PadsWithZeros()
    {
        var code = KatottgCode.FromValue(1);
        Assert.Equal("UA00000000000000001", code.ToString());
        Assert.Equal("UA00000000000000000", default(KatottgCode).ToString());
    }

    [Fact]
    public void FromValue_RejectsMoreThan17Digits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => KatottgCode.FromValue(100_000_000_000_000_000UL));
        Assert.Equal(99_999_999_999_999_999UL, KatottgCode.FromValue(99_999_999_999_999_999UL).Value);
    }

    [Fact]
    public void TryFormat_Chars()
    {
        var code = KatottgCode.Parse("UA80000000001078669");
        Span<char> small = stackalloc char[18];
        Assert.False(code.TryFormat(small, out var written, default, null));
        Assert.Equal(0, written);

        Span<char> buffer = stackalloc char[32];
        Assert.True(code.TryFormat(buffer, out written, default, null));
        Assert.Equal("UA80000000001078669", buffer[..written].ToString());
        Assert.Equal("UA80000000001078669", $"{code}");
        Assert.Equal("UA80000000001078669", code.ToString("G", null));
    }

    [Fact]
    public void TryFormat_Utf8()
    {
        var code = KatottgCode.Parse("UA80000000001078669");
        Span<byte> buffer = stackalloc byte[19];
        Assert.True(code.TryFormat(buffer, out var written, default, null));
        Assert.Equal("UA80000000001078669", Encoding.UTF8.GetString(buffer[..written]));
        Assert.False(code.TryFormat(buffer[..18], out _, default, null));
    }

    [Fact]
    public void UnknownFormat_Throws()
    {
        var code = KatottgCode.Parse("UA80000000001078669");
        Assert.Throws<FormatException>(() => code.ToString("X", null));
    }

    [Theory]
    [InlineData("UA01020030010043419", "UA01020030000000000")] // село → громада
    [InlineData("UA01020010000048857", "UA01020000000000000")] // громада → район
    [InlineData("UA01020000000022387", "UA01000000000000000")] // район → область
    [InlineData("UA80000000001078669", "UA80000000000000000")] // район у Києві → Київ
    [InlineData("UA32000000010085013", "UA32000000000000000")] // Прип’ять → Київська обл.
    [InlineData("UA63120270010948820", "UA63120270010000000")] // район у Харкові → Харків
    public void ParentPrefix_ZeroesLowestNonZeroSegment(string child, string expected)
    {
        var parent = KatottgCode.Parse(child).ParentPrefix;
        Assert.NotNull(parent);
        Assert.Equal(expected, parent.Value.ToString());
        Assert.Equal(0u, parent.Value.Tail);
    }

    [Theory]
    [InlineData("UA01000000000013043")]
    [InlineData("UA80000000000093317")]
    [InlineData("UA00000000000012345")]
    public void ParentPrefix_IsNullForRoots(string code) => Assert.Null(KatottgCode.Parse(code).ParentPrefix);

    [Fact]
    public void Equality_And_Ordering()
    {
        var a = KatottgCode.Parse("UA01000000000013043");
        var b = KatottgCode.Parse("UA05000000000010236");

        Assert.True(a < b);
        Assert.True(b > a);
        var a2 = KatottgCode.Parse("UA01000000000013043");
        Assert.True(a <= a2 && a >= a2);
        Assert.True(a != b);
        Assert.True(a == KatottgCode.Parse("ua01000000000013043"));
        Assert.Equal(a.GetHashCode(), KatottgCode.Parse("UA01000000000013043").GetHashCode());
        Assert.True(a.CompareTo(b) < 0);
        Assert.Equal(0, a.CompareTo((object)a));
        Assert.True(a.CompareTo(null) > 0);
        Assert.Throws<ArgumentException>(() => a.CompareTo("x"));
        Assert.False(a.Equals((object)"UA01000000000013043"));
    }

    [Fact]
    public void SignificantPart_IgnoresTail()
    {
        var a = KatottgCode.Parse("UA01020030010043419");
        var b = KatottgCode.Parse("UA01020030010000000");
        Assert.NotEqual(a, b);
        Assert.Equal(a.SignificantPart, b.SignificantPart);
    }

    [Fact]
    public void GenericParsable_Works()
    {
        Assert.Equal("UA01020030010043419", ParseGeneric<KatottgCode>("UA01020030010043419").ToString());

        static T ParseGeneric<T>(string s) where T : ISpanParsable<T> => T.Parse(s.AsSpan(), null);
    }
}
