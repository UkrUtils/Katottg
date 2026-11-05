using UkrUtils.Katottg.Storage;

namespace UkrUtils.Katottg;

public sealed partial class KatottgDataset
{
    internal const string EmbeddedResourceName = "UkrUtils.Katottg.Data.katottg.bin.gz";

    private static readonly Lazy<KatottgDataset> s_embedded = new(LoadEmbedded, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Dataset from the snapshot embedded in the package. Loaded lazily on first access,
    /// after which the same instance is returned. No network is required.
    /// </summary>
    public static KatottgDataset Embedded => s_embedded.Value;

    /// <summary>Loads a binary snapshot (gzip-compressed or not; detected automatically).</summary>
    /// <exception cref="InvalidDataException">The stream is not a valid snapshot.</exception>
    public static KatottgDataset Load(Stream snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return SnapshotFormat.Read(snapshot);
    }

    /// <summary>
    /// Writes the dataset to a binary snapshot compressed with gzip. The output is deterministic: the same dataset
    /// yields the same bytes.
    /// </summary>
    public void Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        SnapshotFormat.Write(this, destination, compress: true);
    }

    private static KatottgDataset LoadEmbedded()
    {
        using var stream = typeof(KatottgDataset).Assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{EmbeddedResourceName}' is missing from {typeof(KatottgDataset).Assembly.GetName().Name}.");
        return Load(stream);
    }
}
