using System.Buffers.Binary;
using System.IO.Compression;
using UkrUtils.Katottg.Tests.Fixtures;

namespace UkrUtils.Katottg.Tests;

public class SnapshotTests
{
    private static byte[] Save(KatottgDataset dataset)
    {
        using var ms = new MemoryStream();
        dataset.Save(ms);
        return ms.ToArray();
    }

    private static byte[] Decompress(byte[] gz)
    {
        using var input = new GZipStream(new MemoryStream(gz), CompressionMode.Decompress);
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }

    [Fact]
    public void SaveLoad_RoundTrip()
    {
        var original = FixtureDataset.Value;
        var loaded = KatottgDataset.Load(new MemoryStream(Save(original)));

        Assert.Equal(original.Edition, loaded.Edition);
        Assert.Equal(original.ToRecords(), loaded.ToRecords());
    }

    [Fact]
    public void Save_IsDeterministic()
    {
        var first = Save(FixtureDataset.Value);
        var second = Save(KatottgDataset.Load(new MemoryStream(first)));
        Assert.Equal(first, second);
    }

    [Fact]
    public void Load_AcceptsUncompressedSnapshot()
    {
        var raw = Decompress(Save(FixtureDataset.Value));
        Assert.Equal("KATG"u8.ToArray(), raw[..4]);

        var loaded = KatottgDataset.Load(new MemoryStream(raw));
        Assert.Equal(FixtureDataset.Value.ToRecords(), loaded.ToRecords());
    }

    [Fact]
    public void Load_NonSeekableStream()
    {
        var loaded = KatottgDataset.Load(new NonSeekableStream(Save(FixtureDataset.Value)));
        Assert.Equal(FixtureData.RowCount, loaded.Count);
    }

    [Fact]
    public void Load_RoundTripsUnknownEdition()
    {
        var dataset = KatottgDataset.Create(new KatottgEdition(default, null, null),
            [new KatottgRecord(KatottgCode.Parse("UA05000000000010236"), null, KatottgCategory.Unknown, "Тест")]);
        var loaded = KatottgDataset.Load(new MemoryStream(Save(dataset)));

        Assert.Equal(dataset.Edition, loaded.Edition);
        Assert.Equal(KatottgCategory.Unknown, loaded.All[0].Category);
    }

    [Fact]
    public void Load_RejectsBadMagic()
    {
        var raw = Decompress(Save(FixtureDataset.Value));
        raw[0] = (byte)'X';
        Assert.Throws<InvalidDataException>(() => KatottgDataset.Load(new MemoryStream(raw)));
    }

    [Fact]
    public void Load_RejectsUnsupportedVersion()
    {
        var raw = Decompress(Save(FixtureDataset.Value));
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(4), 999);
        Assert.Throws<InvalidDataException>(() => KatottgDataset.Load(new MemoryStream(raw)));
    }

    [Fact]
    public void Load_RejectsTruncated()
    {
        var raw = Decompress(Save(FixtureDataset.Value));
        Assert.Throws<InvalidDataException>(() => KatottgDataset.Load(new MemoryStream(raw[..(raw.Length - 5)])));
        Assert.Throws<InvalidDataException>(() => KatottgDataset.Load(new MemoryStream([])));
    }

    [Fact]
    public void Embedded_IsCurrentWithOfficialData()
    {
        var embedded = KatottgDataset.Embedded;
        var official = FixtureDataset.Official;

        Assert.Same(embedded, KatottgDataset.Embedded);
        Assert.Equal(official.Edition, embedded.Edition);
        Assert.Equal(official.ToRecords(), embedded.ToRecords());
    }

    [Fact]
    public void Embedded_FileMatchesFreshBuild()
    {
        // Вміст знімка в репозиторії має бути побайтово таким, як його будує build-snapshot.
        // Порівнюємо розпаковані дані: байти gzip можуть змінитися з новою версією zlib.
        Assert.Equal(Decompress(Save(FixtureDataset.Official)), Decompress(File.ReadAllBytes(TestPaths.EmbeddedSnapshot)));
    }

    [Fact]
    public void Load_InvalidUtf8_ThrowsInvalidData()
    {
        var raw = Decompress(Save(FixtureDataset.Value));
        // magic(4) + version(2) + date(4) + sha(32) + source(1 + 21) + count(4) + stringCount(4) + довжина першого рядка(1)
        Assert.Equal(21, raw[42]);
        raw[42 + 22 + 8 + 1] = 0xFF;
        Assert.Throws<InvalidDataException>(() => KatottgDataset.Load(new MemoryStream(raw)));
    }

    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();
        public override long Position { get => base.Position; set => throw new NotSupportedException(); }
    }
}
