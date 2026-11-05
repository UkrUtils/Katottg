using BenchmarkDotNet.Attributes;

namespace UkrUtils.Katottg.Benchmarks;

/// <summary>Операції з п. 6.5 плану.</summary>
[MemoryDiagnoser]
public class DatasetBenchmarks
{
    private KatottgDataset _dataset = null!;
    private KatottgCode _code;
    private byte[] _snapshot = null!;

    [GlobalSetup]
    public void Setup()
    {
        _dataset = KatottgDataset.Embedded;
        _code = KatottgCode.Parse("UA63120270010948820");
        using var ms = new MemoryStream();
        _dataset.Save(ms);
        _snapshot = ms.ToArray();
        _dataset.Search("прогрів індексу");
    }

    [Benchmark]
    public KatottgUnit? FindByCode() => _dataset.Find(_code);

    [Benchmark]
    public KatottgCode ParseCode() => KatottgCode.Parse("UA63120270010948820");

    [Benchmark]
    public KatottgSearchResult SearchExact() => _dataset.Search("Кривий Ріг");

    [Benchmark]
    public KatottgSearchResult SearchPrefix() => _dataset.Search("Запор");

    [Benchmark]
    public KatottgSearchResult SearchShortPrefix() => _dataset.Search("К");

    [Benchmark]
    public KatottgSearchResult SearchAddress() => _dataset.Search("с. Андріївка, Бердянський р-н, Запорізька обл.");

    [Benchmark]
    public KatottgDataset LoadSnapshot() => KatottgDataset.Load(new MemoryStream(_snapshot));

    [Benchmark]
    public KatottgSearchResult LoadAndFirstSearch() => KatottgDataset.Load(new MemoryStream(_snapshot)).Search("Київ");
}
