using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace UkrUtils.Katottg;

/// <summary>
/// KATOTTG code: <c>UA</c> + 17 digits, for example <c>UA01020030010043419</c>.
/// </summary>
/// <remarks>
/// Digit structure: <c>RR DD CCC SSS BB TTTTT</c> — region, district, community, populated place,
/// city district, and a 5-digit service tail. The first 12 digits (<see cref="SignificantPart"/>)
/// are unique for every object; the tail is not derived from the hierarchy.
/// The <c>default</c> value is <c>UA00000000000000000</c>; such a code does not exist in the codifier.
/// <para>
/// The code is unique within an edition, so storing it in a database needs just one field with the code of the
/// lowest level (<c>char(19)</c> via <see cref="ToString()"/> or <c>bigint</c> via
/// <see cref="Value"/>). Do not compute a parent's full code from a child's code: the parent's tail is not there;
/// use <see cref="KatottgUnit.Parent"/> or <see cref="HierarchyPrefix"/>.
/// </para>
/// </remarks>
public readonly struct KatottgCode
    : IEquatable<KatottgCode>, IComparable<KatottgCode>, IComparable,
      ISpanParsable<KatottgCode>, ISpanFormattable, IUtf8SpanFormattable
{
    /// <summary>Length of the text form: <c>UA</c> + 17 digits.</summary>
    public const int Length = 19;

    /// <summary>Number of digits in the code.</summary>
    public const int DigitCount = 17;

    /// <summary>Largest allowed numeric value (seventeen nines).</summary>
    public const ulong MaxValue = 99_999_999_999_999_999UL;

    private const ulong TailModulus = 100_000UL;

    private readonly ulong _value;

    private KatottgCode(ulong value) => _value = value;

    /// <summary>Creates a code from its 17 digits given as a number.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value has more than 17 digits.</exception>
    public static KatottgCode FromValue(ulong value)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxValue);
        return new KatottgCode(value);
    }

    /// <summary>The 17 digits of the code as a number.</summary>
    public ulong Value => _value;

    /// <summary>Region or city with special status (digits 1–2).</summary>
    public byte Region => (byte)(_value / 1_000_000_000_000_000UL % 100);

    /// <summary>District within a region (digits 3–4).</summary>
    public byte District => (byte)(_value / 10_000_000_000_000UL % 100);

    /// <summary>Community within a district (digits 5–7).</summary>
    public ushort Community => (ushort)(_value / 10_000_000_000UL % 1000);

    /// <summary>Populated place within a community (digits 8–10).</summary>
    public ushort Settlement => (ushort)(_value / 10_000_000UL % 1000);

    /// <summary>City district (digits 11–12).</summary>
    public byte CityDistrict => (byte)(_value / TailModulus % 100);

    /// <summary>5-digit service tail (digits 13–17).</summary>
    public uint Tail => (uint)(_value % TailModulus);

    /// <summary>12 significant digits without the tail. Unique for every codifier object.</summary>
    public ulong SignificantPart => _value / TailModulus;

    /// <summary>
    /// Prefix of the parent object: the lowest non-zero segment is zeroed and the tail is zero.
    /// This is <b>only a prefix</b> — the parent's full code (with the tail) cannot be computed from a child's code;
    /// look the parent up via <c>FindBySignificantPart</c>. <c>null</c> for root codes.
    /// </summary>
    public KatottgCode? ParentPrefix
    {
        get
        {
            var significant = SignificantPart;
            // Сегменти знизу вгору: район у місті (2), н. п. (3), громада (3), район (2). Область — корінь.
            ReadOnlySpan<ulong> multipliers = [1UL, 100UL, 100_000UL, 100_000_000UL];
            ReadOnlySpan<ulong> moduli = [100UL, 1000UL, 1000UL, 100UL];
            for (var i = 0; i < multipliers.Length; i++)
            {
                var segment = significant / multipliers[i] % moduli[i];
                if (segment != 0)
                {
                    var parentSignificant = significant - segment * multipliers[i];
                    return new KatottgCode(parentSignificant * TailModulus);
                }
            }
            return null;
        }
    }

    /// <summary>Number of significant digits up to the end of the lowest non-zero segment (0, 2, 4, 7, 10, or 12).</summary>
    private int HierarchyDigits
    {
        get
        {
            var significant = SignificantPart;
            if (significant % 100 != 0) return 12;                    // район у місті
            if (significant / 100 % 1000 != 0) return 10;             // населений пункт
            if (significant / 100_000 % 1000 != 0) return 7;          // громада
            if (significant / 100_000_000 % 100 != 0) return 4;       // район
            return significant != 0 ? 2 : 0;                          // область
        }
    }

    /// <summary>
    /// Text prefix that the codes of this object and all its descendants start with:
    /// <c>UA0102</c> for a district, <c>UA0102001</c> for a community. Handy for SQL:
    /// <c>WHERE code LIKE 'UA0102%'</c> — the district itself and everything in it.
    /// </summary>
    /// <remarks>
    /// The invariant is verified on all records of the official file: the prefix selects exactly the object's
    /// subtree, including the atypical cases (Kyiv, Sevastopol, Prypiat).
    /// </remarks>
    public string HierarchyPrefix
    {
        get
        {
            var digits = HierarchyDigits;
            Span<char> buffer = stackalloc char[Length];
            WriteChars(buffer, _value);
            return buffer[..(2 + digits)].ToString();
        }
    }

    /// <summary>
    /// Whether this object lies inside <paramref name="ancestor"/> (strictly: the object itself does not count).
    /// Checks only the code digits, without a dataset: the code starts with the ancestor's <see cref="HierarchyPrefix"/>.
    /// </summary>
    public bool IsDescendantOf(KatottgCode ancestor)
    {
        var digits = ancestor.HierarchyDigits;
        if (digits == 0 || SignificantPart == ancestor.SignificantPart)
            return false;
        var divisor = Pow10(12 - digits);
        return SignificantPart / divisor == ancestor.SignificantPart / divisor;

        static ulong Pow10(int n)
        {
            ulong result = 1;
            for (var i = 0; i < n; i++)
                result *= 10;
            return result;
        }
    }

    #region Parsing

    /// <summary>
    /// Parses a code. Accepts <c>UA</c> in any case (including the Cyrillic <c>УА</c>)
    /// and surrounding whitespace.
    /// </summary>
    /// <exception cref="FormatException">The string is not a KATOTTG code.</exception>
    public static KatottgCode Parse(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <inheritdoc cref="Parse(string)"/>
    public static KatottgCode Parse(ReadOnlySpan<char> s)
        => TryParse(s, out var code) ? code : throw new FormatException($"'{s}' is not a valid KATOTTG code (expected 'UA' followed by 17 digits).");

    /// <inheritdoc cref="Parse(string)"/>
    public static KatottgCode Parse(string s, IFormatProvider? provider) => Parse(s);

    /// <inheritdoc cref="Parse(string)"/>
    public static KatottgCode Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);

    /// <summary>Tries to parse a code; see <see cref="Parse(string)"/>.</summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out KatottgCode result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>Tries to parse a code; see <see cref="Parse(string)"/>.</summary>
    public static bool TryParse(ReadOnlySpan<char> s, out KatottgCode result)
    {
        result = default;
        s = s.Trim();
        if (s.Length != Length || !IsU(s[0]) || !IsA(s[1]))
            return false;

        ulong value = 0;
        foreach (var c in s[2..])
        {
            var digit = (uint)(c - '0');
            if (digit > 9)
                return false;
            value = value * 10 + digit;
        }

        result = new KatottgCode(value);
        return true;

        static bool IsU(char c) => c is 'U' or 'u' or 'У' or 'у';
        static bool IsA(char c) => c is 'A' or 'a' or 'А' or 'а';
    }

    /// <inheritdoc cref="TryParse(string, out KatottgCode)"/>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out KatottgCode result)
        => TryParse(s, out result);

    /// <inheritdoc cref="TryParse(ReadOnlySpan{char}, out KatottgCode)"/>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out KatottgCode result)
        => TryParse(s, out result);

    #endregion

    #region Formatting

    /// <summary>Canonical form: <c>UA</c> + 17 digits.</summary>
    public override string ToString() => string.Create(Length, _value, static (span, value) => WriteChars(span, value));

    /// <summary>Only the default format is supported (<c>null</c>, <c>""</c> or <c>"G"</c>).</summary>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        ValidateFormat(format);
        return ToString();
    }

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        ValidateFormat(format);
        if (destination.Length < Length)
        {
            charsWritten = 0;
            return false;
        }
        WriteChars(destination, _value);
        charsWritten = Length;
        return true;
    }

    /// <inheritdoc/>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        ValidateFormat(format);
        if (utf8Destination.Length < Length)
        {
            bytesWritten = 0;
            return false;
        }
        utf8Destination[0] = (byte)'U';
        utf8Destination[1] = (byte)'A';
        var value = _value;
        for (var i = Length - 1; i >= 2; i--)
        {
            utf8Destination[i] = (byte)('0' + value % 10);
            value /= 10;
        }
        bytesWritten = Length;
        return true;
    }

    private static void WriteChars(Span<char> destination, ulong value)
    {
        destination[0] = 'U';
        destination[1] = 'A';
        for (var i = Length - 1; i >= 2; i--)
        {
            destination[i] = (char)('0' + value % 10);
            value /= 10;
        }
    }

    private static void ValidateFormat(ReadOnlySpan<char> format)
    {
        if (format.IsEmpty || format is "G" or "g")
            return;
        throw new FormatException($"Format '{format}' is not supported by KatottgCode. Use 'G'.");
    }

    #endregion

    #region Equality and ordering

    /// <inheritdoc/>
    public bool Equals(KatottgCode other) => _value == other._value;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is KatottgCode other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => _value.GetHashCode();

    /// <inheritdoc/>
    public int CompareTo(KatottgCode other) => _value.CompareTo(other._value);

    /// <inheritdoc/>
    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        KatottgCode other => CompareTo(other),
        _ => throw new ArgumentException($"Object must be of type {nameof(KatottgCode)}.", nameof(obj)),
    };

#pragma warning disable CS1591 // оператори очевидні
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(KatottgCode left, KatottgCode right) => left._value == right._value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(KatottgCode left, KatottgCode right) => left._value != right._value;

    public static bool operator <(KatottgCode left, KatottgCode right) => left._value < right._value;
    public static bool operator >(KatottgCode left, KatottgCode right) => left._value > right._value;
    public static bool operator <=(KatottgCode left, KatottgCode right) => left._value <= right._value;
    public static bool operator >=(KatottgCode left, KatottgCode right) => left._value >= right._value;
#pragma warning restore CS1591

    #endregion
}
