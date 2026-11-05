using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace UkrUtils.Katottg.Storage;

/// <summary>
/// Бінарний знімок набору даних (little-endian, зазвичай у gzip):
/// <code>
/// magic "KATG" | version u16 | edition-date i32 (yyyyMMdd) | sha256[32] (нулі = невідомо)
/// source: рядок (порожній = невідомо)
/// count u32
/// string-table: u32 count, далі рядки
/// records: count × { code u64, parentIndex i32 (-1 = корінь), category u8, nameIndex u32 }
/// </code>
/// Рядок — це довжина в байтах (7-бітне кодування, як у <see cref="BinaryWriter"/>) і UTF-8.
/// Таблиця рядків — унікальні назви в порядку першої появи, тож вивід детермінований.
/// </summary>
internal static class SnapshotFormat
{
    public const ushort Version = 1;
    private static ReadOnlySpan<byte> Magic => "KATG"u8;
    private const int Sha256Length = 32;
    private const int MaxCount = 10_000_000;

    public static void Write(KatottgDataset dataset, Stream destination, bool compress)
    {
        if (compress)
        {
            using var gzip = new GZipStream(destination, CompressionLevel.SmallestSize, leaveOpen: true);
            Write(dataset, gzip, compress: false);
            return;
        }

        using var writer = new BinaryWriter(destination, new UTF8Encoding(false), leaveOpen: true);
        var edition = dataset.Edition;

        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(edition.Date.Year * 10_000 + edition.Date.Month * 100 + edition.Date.Day);
        Span<byte> sha = stackalloc byte[Sha256Length];
        sha.Clear();
        if (edition.Sha256 is { } hex)
        {
            if (Convert.FromHexString(hex, sha, out _, out var written) != System.Buffers.OperationStatus.Done || written != Sha256Length)
                throw new InvalidOperationException($"Edition SHA-256 '{hex}' is not 64 hex digits.");
        }
        writer.Write(sha);
        writer.Write(edition.Source ?? "");

        var units = dataset.All;
        var strings = new List<string>();
        var stringIndex = new Dictionary<string, uint>(StringComparer.Ordinal);
        var nameIndexes = new uint[units.Count];
        for (var i = 0; i < units.Count; i++)
        {
            var name = units[i].Name;
            if (!stringIndex.TryGetValue(name, out var index))
            {
                index = (uint)strings.Count;
                strings.Add(name);
                stringIndex.Add(name, index);
            }
            nameIndexes[i] = index;
        }

        writer.Write((uint)units.Count);
        writer.Write((uint)strings.Count);
        foreach (var s in strings)
            writer.Write(s);

        for (var i = 0; i < units.Count; i++)
        {
            var unit = units[i];
            writer.Write(unit.Code.Value);
            writer.Write(unit.Parent?.Index ?? -1);
            writer.Write((byte)unit.Category);
            writer.Write(nameIndexes[i]);
        }
    }

    public static KatottgDataset Read(Stream source)
    {
        // Визначаємо, чи це gzip (1F 8B), не покладаючись на Seek.
        Span<byte> head = stackalloc byte[2];
        var read = source.ReadAtLeast(head, 2, throwOnEndOfStream: false);
        if (read < 2)
            throw new InvalidDataException("Snapshot is empty or truncated.");
        var prefixed = new PrefixedStream(head.ToArray(), source);

        try
        {
            if (head[0] == 0x1F && head[1] == 0x8B)
            {
                using var gzip = new GZipStream(prefixed, CompressionMode.Decompress, leaveOpen: true);
                return ReadRaw(new BufferedStream(gzip, 64 * 1024));
            }
            return ReadRaw(prefixed);
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException("Snapshot is truncated.", ex);
        }
        catch (Exception ex) when (ex is DecoderFallbackException or FormatException)
        {
            // Невалідний UTF-8 у рядку або зіпсований 7-бітний префікс довжини.
            throw new InvalidDataException("Snapshot contains a malformed string.", ex);
        }
    }

    private static KatottgDataset ReadRaw(Stream stream)
    {
        using var reader = new BinaryReader(stream, new UTF8Encoding(false, throwOnInvalidBytes: true), leaveOpen: true);

        Span<byte> magic = stackalloc byte[4];
        reader.BaseStream.ReadExactly(magic);
        if (!magic.SequenceEqual(Magic))
            throw new InvalidDataException("Not a KATOTTG snapshot (bad magic).");
        var version = reader.ReadUInt16();
        if (version != Version)
            throw new InvalidDataException($"Unsupported snapshot version {version}; this library reads version {Version}.");

        var dateValue = reader.ReadInt32();
        var date = DecodeDate(dateValue);
        Span<byte> sha = stackalloc byte[Sha256Length];
        reader.BaseStream.ReadExactly(sha);
        var sha256 = sha.ContainsAnyExcept((byte)0) ? Convert.ToHexStringLower(sha) : null;
        var sourceName = reader.ReadString();

        var count = CheckCount(reader.ReadUInt32(), "record");
        var stringCount = CheckCount(reader.ReadUInt32(), "string");
        var strings = new string[stringCount];
        for (var i = 0; i < strings.Length; i++)
            strings[i] = reader.ReadString();

        var records = new KatottgRecord[count];
        Span<byte> buffer = stackalloc byte[17];
        for (var i = 0; i < records.Length; i++)
        {
            reader.BaseStream.ReadExactly(buffer);
            var code = BinaryPrimitives.ReadUInt64LittleEndian(buffer);
            var parentIndex = BinaryPrimitives.ReadInt32LittleEndian(buffer[8..]);
            var category = buffer[12];
            var nameIndex = BinaryPrimitives.ReadUInt32LittleEndian(buffer[13..]);

            if (code > KatottgCode.MaxValue)
                throw new InvalidDataException($"Record {i}: code value {code} has more than 17 digits.");
            if (parentIndex < -1 || parentIndex >= i)
                throw new InvalidDataException($"Record {i}: parent index {parentIndex} must refer to an earlier record.");
            if (nameIndex >= (uint)strings.Length)
                throw new InvalidDataException($"Record {i}: name index {nameIndex} is out of range.");

            records[i] = new KatottgRecord(
                KatottgCode.FromValue(code),
                parentIndex < 0 ? null : records[parentIndex].Code,
                Enum.IsDefined((KatottgCategory)category) ? (KatottgCategory)category : KatottgCategory.Unknown,
                strings[nameIndex]);
        }

        try
        {
            return KatottgDataset.Create(new KatottgEdition(date, sourceName.Length == 0 ? null : sourceName, sha256), records);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException("Snapshot content is inconsistent: " + ex.Message, ex);
        }
    }

    private static int CheckCount(uint value, string what)
        => value <= MaxCount ? (int)value : throw new InvalidDataException($"Snapshot {what} count {value} is implausible.");

    private static DateOnly DecodeDate(int value)
    {
        int year = value / 10_000, month = value / 100 % 100, day = value % 100;
        if (year is < 1 or > 9999 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
            throw new InvalidDataException($"Snapshot edition date {value} is invalid.");
        return new DateOnly(year, month, day);
    }

    /// <summary>Повертає вже прочитаний префікс, потім решту потоку.</summary>
    private sealed class PrefixedStream(byte[] prefix, Stream inner) : Stream
    {
        private int _position;

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (_position < prefix.Length)
            {
                var n = Math.Min(buffer.Length, prefix.Length - _position);
                prefix.AsSpan(_position, n).CopyTo(buffer);
                _position += n;
                return n;
            }
            return inner.Read(buffer);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
