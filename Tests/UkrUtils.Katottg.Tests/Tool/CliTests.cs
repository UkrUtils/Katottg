using UkrUtils.Katottg.Tests.Fixtures;
using UkrUtils.Katottg.Tool;

namespace UkrUtils.Katottg.Tests.Tool;

public class CliTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("katottg-cli-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static (int Exit, string Out, string Err) Run(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = KatottgCli.Run(args, stdout, stderr);
        return (exit, stdout.ToString(), stderr.ToString());
    }

    private string WriteFixture(string name = "katottg-26-11-03.xlsx")
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, FixtureData.CreateOfficialLike().Build());
        return path;
    }

    [Fact]
    public void NoArgs_PrintsUsage()
    {
        var (exit, _, err) = Run();
        Assert.Equal(2, exit);
        Assert.Contains("build-snapshot", err);
    }

    [Fact]
    public void UnknownCommand_Fails()
    {
        var (exit, _, err) = Run("frobnicate");
        Assert.Equal(2, exit);
        Assert.Contains("frobnicate", err);
    }

    [Fact]
    public void Import_PrintsSummary()
    {
        var (exit, output, _) = Run("import", WriteFixture());
        Assert.Equal(0, exit);
        Assert.Contains("2026-11-03", output);
        Assert.Contains("44", output);
        Assert.Contains("LatinCharactersInName", output);
    }

    [Fact]
    public void BuildSnapshot_WritesLoadableFile()
    {
        var output = Path.Combine(_dir, "out", "snap.bin.gz");
        var (exit, _, err) = Run("build-snapshot", WriteFixture(), "--output", output);

        Assert.True(exit == 0, err);
        using var stream = File.OpenRead(output);
        var dataset = KatottgDataset.Load(stream);
        Assert.Equal(FixtureData.RowCount, dataset.Count);
    }

    [Fact]
    public void BuildSnapshot_FromDirectory_PicksLatestEdition()
    {
        WriteFixture("katottg-25-01-01.xlsx");
        WriteFixture("katottg-26-11-03.xlsx");
        var output = Path.Combine(_dir, "snap.bin.gz");

        var (exit, _, err) = Run("build-snapshot", _dir, "-o", output);

        Assert.True(exit == 0, err);
        using var stream = File.OpenRead(output);
        Assert.Equal(new DateOnly(2026, 11, 3), KatottgDataset.Load(stream).Edition.Date);
    }

    [Fact]
    public void Search_Embedded()
    {
        var (exit, output, _) = Run("search", "Кривий", "Ріг", "--limit", "1");
        Assert.Equal(0, exit);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.StartsWith("UA12060170010065850", lines[0]);
        Assert.Contains("м. Кривий Ріг", lines[0]);
    }

    [Fact]
    public void Search_ByCode_FromSnapshotFile()
    {
        var snapshot = Path.Combine(_dir, "snap.bin.gz");
        Assert.Equal(0, Run("build-snapshot", WriteFixture(), "-o", snapshot).Exit);

        var (exit, output, _) = Run("search", "UA01020010010000000", "--snapshot", snapshot);
        Assert.Equal(0, exit);
        Assert.Contains("с. Андріївка, Андріївська громада, Бахчисарайський р-н, АР Крим", output);
    }

    [Fact]
    public void Search_Tree()
    {
        var (exit, output, _) = Run("search", "Шевченківський р-н, м. Харків", "--tree");
        Assert.Equal(0, exit);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();
        Assert.Equal(5, lines.Length);
        Assert.StartsWith("Харківська обл.  [UA63", lines[0]);
        Assert.StartsWith("      м. Харків", lines[3]);
        Assert.StartsWith("        * Шевченківський р-н  [UA63120270010948820]", lines[4]);
    }

    [Fact]
    public void Search_NothingFound()
    {
        var (exit, _, err) = Run("search", "Ыыыыы");
        Assert.Equal(1, exit);
        Assert.Contains("Nothing found", err);
    }

    [Fact]
    public void Search_BadLimit() => Assert.Equal(2, Run("search", "Київ", "--limit", "0").Exit);

    [Fact]
    public void Search_CorruptSnapshot_ExitsWithError()
    {
        // Нестиснутий знімок фікстури з невалідним UTF-8 у першій назві
        var gz = Path.Combine(_dir, "snap.bin.gz");
        Assert.Equal(0, Run("build-snapshot", WriteFixture(), "-o", gz).Exit);
        using var ms = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(File.OpenRead(gz), System.IO.Compression.CompressionMode.Decompress))
            gzip.CopyTo(ms);
        var raw = ms.ToArray();
        raw[42 + 22 + 8 + 1] = 0xFF;
        var path = Path.Combine(_dir, "corrupt.bin");
        File.WriteAllBytes(path, raw);
        var (exit, _, err) = Run("search", "Київ", "--snapshot", path);
        Assert.Equal(1, exit);
        Assert.Contains("error", err);
    }

    [Fact]
    public void BuildSnapshot_MissingFile_Fails()
    {
        var (exit, _, err) = Run("build-snapshot", Path.Combine(_dir, "nope.xlsx"));
        Assert.Equal(1, exit);
        Assert.Contains("nope.xlsx", err);
    }
}
