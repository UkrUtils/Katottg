using UkrUtils.Katottg.Storage;

namespace UkrUtils.Katottg;

public sealed partial class KatottgDataset
{
    internal const string EmbeddedResourceName = "UkrUtils.Katottg.Data.katottg.bin.gz";

    private static readonly Lazy<KatottgDataset> s_embedded = new(LoadEmbedded, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Набір даних із вбудованого в пакет знімка. Завантажується ліниво при першому зверненні,
    /// далі повертається той самий екземпляр. Мережа не потрібна.
    /// </summary>
    public static KatottgDataset Embedded => s_embedded.Value;

    /// <summary>Завантажує бінарний знімок (стиснутий gzip або ні — визначається автоматично).</summary>
    /// <exception cref="InvalidDataException">Потік не є коректним знімком.</exception>
    public static KatottgDataset Load(Stream snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return SnapshotFormat.Read(snapshot);
    }

    /// <summary>
    /// Записує набір у бінарний знімок, стиснутий gzip. Вивід детермінований: однаковий набір
    /// дає однакові байти.
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
