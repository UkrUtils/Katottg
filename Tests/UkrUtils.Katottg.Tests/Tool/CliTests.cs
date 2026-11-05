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
    public void BuildSnapshot_MissingFile_Fails()
    {
        var (exit, _, err) = Run("build-snapshot", Path.Combine(_dir, "nope.xlsx"));
        Assert.Equal(1, exit);
        Assert.Contains("nope.xlsx", err);
    }
}
