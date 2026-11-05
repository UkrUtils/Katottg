using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace UkrUtils.Katottg;

/// <summary>
/// Код КАТОТТГ: <c>UA</c> + 17 цифр, наприклад <c>UA01020030010043419</c>.
/// </summary>
/// <remarks>
/// Структура цифр: <c>RR DD CCC SSS BB TTTTT</c> — область, район, громада, населений пункт,
/// район у місті та 5-значний службовий хвіст. Перші 12 цифр (<see cref="SignificantPart"/>)
/// унікальні для кожного об’єкта; хвіст з ієрархії не виводиться.
/// Значення <c>default</c> — це <c>UA00000000000000000</c>, такого коду в кодифікаторі немає.
/// <para>
/// Код унікальний у межах редакції, тож для зберігання в БД досить одного поля з кодом
/// найнижчого рівня (<c>char(19)</c> через <see cref="ToString()"/> або <c>bigint</c> через
/// <see cref="Value"/>). Не обчислюйте повний код батька з коду дитини: хвоста батька там немає —
/// використовуйте <see cref="KatottgUnit.Parent"/> або <see cref="HierarchyPrefix"/>.
/// </para>
/// </remarks>
public readonly struct KatottgCode
    : IEquatable<KatottgCode>, IComparable<KatottgCode>, IComparable,
      ISpanParsable<KatottgCode>, ISpanFormattable, IUtf8SpanFormattable
{
    /// <summary>Довжина текстового подання: <c>UA</c> + 17 цифр.</summary>
    public const int Length = 19;

    /// <summary>Кількість цифр у коді.</summary>
    public const int DigitCount = 17;

    /// <summary>Найбільше допустиме числове значення (17 дев’яток).</summary>
    public const ulong MaxValue = 99_999_999_999_999_999UL;

    private const ulong TailModulus = 100_000UL;

    private readonly ulong _value;

    private KatottgCode(ulong value) => _value = value;

    /// <summary>Створює код з його 17 цифр, поданих як число.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Значення має більше ніж 17 цифр.</exception>
    public static KatottgCode FromValue(ulong value)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxValue);
        return new KatottgCode(value);
    }

    /// <summary>17 цифр коду як число.</summary>
    public ulong Value => _value;

    /// <summary>Область або місто зі спеціальним статусом (цифри 1–2).</summary>
    public byte Region => (byte)(_value / 1_000_000_000_000_000UL % 100);

    /// <summary>Район в області (цифри 3–4).</summary>
    public byte District => (byte)(_value / 10_000_000_000_000UL % 100);

    /// <summary>Громада в районі (цифри 5–7).</summary>
    public ushort Community => (ushort)(_value / 10_000_000_000UL % 1000);

    /// <summary>Населений пункт у громаді (цифри 8–10).</summary>
    public ushort Settlement => (ushort)(_value / 10_000_000UL % 1000);

    /// <summary>Район у місті (цифри 11–12).</summary>
    public byte CityDistrict => (byte)(_value / TailModulus % 100);

    /// <summary>Службовий 5-значний хвіст (цифри 13–17).</summary>
    public uint Tail => (uint)(_value % TailModulus);

    /// <summary>12 значущих цифр без хвоста. Унікальні для кожного об’єкта кодифікатора.</summary>
    public ulong SignificantPart => _value / TailModulus;

    /// <summary>
    /// Префікс батьківського об’єкта: найнижчий ненульовий сегмент обнулено, хвіст дорівнює нулю.
    /// Це <b>лише префікс</b> — повний код батька (з хвостом) з коду дитини не обчислити;
    /// шукайте батька через <c>FindBySignificantPart</c>. <c>null</c> для кореневих кодів.
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

    /// <summary>Кількість значущих цифр до кінця найнижчого ненульового сегмента (0, 2, 4, 7, 10 або 12).</summary>
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
    /// Текстовий префікс, з якого починаються коди цього об’єкта і всіх його нащадків:
    /// <c>UA0102</c> для району, <c>UA0102001</c> для громади. Зручно для SQL:
    /// <c>WHERE code LIKE 'UA0102%'</c> — сам район і все, що в ньому.
    /// </summary>
    /// <remarks>
    /// Інваріант перевірено на всіх записах офіційного файлу: префікс вибирає рівно піддерево
    /// об’єкта, включно з нетиповими місцями (Київ, Севастополь, Прип’ять).
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
    /// Чи лежить цей об’єкт усередині <paramref name="ancestor"/> (строго: сам об’єкт — ні).
    /// Перевіряє лише цифри коду, без набору даних: код починається з <see cref="HierarchyPrefix"/> предка.
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
    /// Розбирає код. Приймає <c>UA</c> у будь-якому регістрі (також кириличне <c>УА</c>)
    /// і пробіли по краях.
    /// </summary>
    /// <exception cref="FormatException">Рядок не є кодом КАТОТТГ.</exception>
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

    /// <summary>Пробує розібрати код; див. <see cref="Parse(string)"/>.</summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out KatottgCode result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>Пробує розібрати код; див. <see cref="Parse(string)"/>.</summary>
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

    /// <summary>Канонічне подання: <c>UA</c> + 17 цифр.</summary>
    public override string ToString() => string.Create(Length, _value, static (span, value) => WriteChars(span, value));

    /// <summary>Підтримується лише формат за замовчуванням (<c>null</c>, <c>""</c> або <c>"G"</c>).</summary>
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
