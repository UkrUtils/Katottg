namespace UkrUtils.Katottg.Tests.Fixtures;

internal static class TestPaths
{
    private static readonly Lazy<string> s_repoRoot = new(() =>
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "OfficialData")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (with OfficialData/) not found.");
    });

    public static string RepoRoot => s_repoRoot.Value;

    /// <summary>Найновіший офіційний xlsx в <c>OfficialData/</c> (за іменем файлу).</summary>
    public static string LatestOfficialXlsx =>
        Directory.GetFiles(Path.Combine(RepoRoot, "OfficialData"), "katottg-*.xlsx")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Last();

    public static string EmbeddedSnapshot => Path.Combine(RepoRoot, "Source", "UkrUtils.Katottg", "Data", "katottg.bin.gz");
}
