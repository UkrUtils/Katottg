using UkrUtils.Katottg.Import;

namespace UkrUtils.Katottg.Tests.Fixtures;

internal static class FixtureDataset
{
    private static readonly Lazy<KatottgDataset> s_value = new(() =>
        KatottgSheetParser.Parse(new MemoryStream(FixtureData.CreateOfficialLike().Build()), "katottg-26-11-03.xlsx").CreateDataset());

    /// <summary>Набір із 44 записів фікстури.</summary>
    public static KatottgDataset Value => s_value.Value;

    private static readonly Lazy<KatottgDataset> s_official = new(() =>
    {
        var result = KatottgSheetParser.Parse(TestPaths.LatestOfficialXlsx);
        if (result.Report.HasErrors)
            throw new InvalidOperationException("Official file has import errors.");
        return result.CreateDataset();
    });

    /// <summary>Повний набір з офіційного файлу в <c>OfficialData/</c>.</summary>
    public static KatottgDataset Official => s_official.Value;

    public static KatottgUnit Unit(this KatottgDataset dataset, string code)
        => dataset.Find(KatottgCode.Parse(code)) ?? throw new InvalidOperationException($"{code} not found.");
}
