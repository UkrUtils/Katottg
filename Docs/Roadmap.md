# Roadmap

> Українською: [Docs/uk/Roadmap.md](https://github.com/UkrUtils/Katottg/blob/main/Docs/uk/Roadmap.md)

What is already built, what comes next and which questions are still open. The designs below are
plans, not promises: details may change while they are implemented. How the existing parts work is
described in the [deep dive](https://ukrutils.github.io/Katottg/deep-dive).

## Status

| # | Stage | Status |
|---|---|---|
| 1 | Skeleton: projects, tests, CI (`dotnet build/test/pack`) | done |
| 2 | `KatottgCode`, `KatottgCategory` | done |
| 3 | `MinimalXlsxReader`, `KatottgSheetParser`, `ImportReport` | done |
| 4 | `KatottgUnit`, `KatottgDataset`, the tree and its invariants | done |
| 5 | Binary snapshot, `build-snapshot` in the CLI, the embedded resource | done |
| 6 | Normalizer, exact and prefix search | done |
| 7 | Ranking; fuzzy search with typos | ranking done; fuzzy search planned |
| 8 | Free-form address; transliteration | address done; transliteration planned |
| 9 | Edition comparison (`KatottgDiff`) and `katottg diff` | planned |
| 10 | Update sources, the DI package, background updates | planned |
| 11 | JSON / `TypeConverter` / EF Core converters; docs and examples | docs done; converters planned |
| 12 | A GitHub Action that checks the ministry's site and opens a PR with new data | planned |

Built beyond the original plan: `KatottgAddress` (`ToAddress()`), `KatottgTreeNode`, the one-button
release workflow with NuGet Trusted Publishing, and the documentation site.

Stages 1–6 were the MVP. Stages 7–8 bring the most value to users; stages 9–12 are about maintenance
and keeping the data fresh.

## Packages

| Package | Depends on | Status |
|---|---|---|
| `UkrUtils.Katottg` | — | published |
| `UkrUtils.Katottg.Import` | core | published (xlsx); update sources and diff planned |
| `UkrUtils.Katottg.Tool` (`katottg`) | Import | published (`import`, `build-snapshot`, `search`); `diff` and export planned |
| `UkrUtils.Katottg.Extensions.DependencyInjection` | core, Import, `Microsoft.Extensions.*` | planned |
| `UkrUtils.Katottg.EntityFrameworkCore` | core, EF Core | planned |

## Planned features

### Fuzzy search

A trigram index (`Dictionary<int, int[]>`: a trigram → sorted unit indexes) gives candidates, including
a part of a word in the middle of a name. The candidates are then checked with a bounded
Damerau–Levenshtein distance: at most 1 edit for short queries, at most 2 for longer ones. Each edit
lowers the score, so fuzzy matches rank below the existing match kinds. Target: < 300 µs for 20 results.

### Transliteration

Search in Latin script (`Kyiv`, `Zaporizhzhia`, `Kamianske`) through a separate key built with the
official transliteration (Resolution of the Cabinet of Ministers No. 55 of 27.01.2010). Reverse
transliteration is ambiguous, so the names in the index are transliterated, and the query is only
normalized. The key gets its own sorted array, like the existing name index.

### Edition comparison (`KatottgDiff`)

```csharp
public sealed record KatottgChange(KatottgChangeKind Kind, KatottgUnit? Old, KatottgUnit? New);
public enum KatottgChangeKind { Added, Removed, Renamed, CategoryChanged, Moved }

KatottgDiff diff = KatottgDiff.Compare(oldDataset, newDataset);
```

Units are matched by code. When a unit moves to another community, the ministry usually gives it a new
code, so pairs "removed + added" with the same name and category in the same region are marked as
probably moved. Users need this to migrate codes stored in their databases:
`KatottgDiff.SuggestReplacement(code)`. The CLI gets `katottg diff old.xlsx new.xlsx --format md|json|csv`,
and its output becomes the release notes of a data update.

### Update sources

The library will keep working offline; updates are an opt-in plug-in, not a hard-coded URL:

```csharp
public interface IKatottgSource
{
    ValueTask<KatottgEditionInfo?> GetLatestEditionAsync(CancellationToken ct);
    ValueTask<Stream> OpenAsync(KatottgEditionInfo edition, CancellationToken ct);
}
```

- `GitHubReleaseSource` — ready snapshots (`.bin.gz`) from this repository's releases. The default:
  CI has already imported and validated the official xlsx, and clients download ~360 KB without
  depending on the ministry's HTML.
- `OfficialSiteSource` — parses the ministry's page and looks for links to `.xlsx` with a date; retries,
  `ETag` / `If-Modified-Since`.
- `UrlSource` — a direct link configured by the user.
- `FileSource` — a local file (tests, offline environments).

### Dependency injection and background updates

```csharp
services.AddKatottg(o =>
{
    o.UpdateSource = KatottgSources.GitHubReleases();   // or OfficialSite(), Url(...), File(...)
    o.UpdateInterval = TimeSpan.FromDays(1);
    o.CacheDirectory = "/var/cache/katottg";             // the last downloaded snapshot
});

public interface IKatottgProvider
{
    KatottgDataset Current { get; }                      // a volatile read of a field
    event EventHandler<KatottgUpdatedEventArgs> Updated; // with a KatottgDiff
}
```

Startup order:

1. Load the disk cache if it is newer than the embedded snapshot; otherwise use the embedded one.
2. Query the source in the background.
3. Validate what came back: the import validator, at least 90% of the current record count, no
   critical errors.
4. Write it to disk: a temporary file, then a rename.
5. Swap the current dataset with `Interlocked.Exchange`.

If the network or the source is unavailable, log a warning through `ILogger` and keep the old data.

### Converters

- `JsonConverter<KatottgCode>`, compatible with System.Text.Json source generation, in a package other
  than the core (the core stays free of `System.Text.Json`).
- `TypeConverter` for configuration binding and ASP.NET Core model binding.
- EF Core `ValueConverter<KatottgCode, string>` in `UkrUtils.Katottg.EntityFrameworkCore`.

### Automatic data updates

A weekly GitHub Action checks the ministry's site. When a new edition appears, it opens a PR with the
new xlsx in `OfficialData/`, the rebuilt snapshot and the diff. A person reviews and merges it, then
releases a new package version.

The CLI may also get an export to JSON / CSV for those who need the data outside .NET.

## Open questions

1. **KOATUU ↔ KATOTTG.** Many older systems still store KOATUU codes. The codifier has no mapping, and
   there are no official mapping tables, only unofficial ones (for example, from the Treasury). If it is
   needed, it would be a separate package, `UkrUtils.Katottg.Koatuu`.
2. **Edition history.** Should the library answer "what was this village's code on date X"? That would
   mean keeping every snapshot from `OfficialData/` and adding a `KatottgHistory`.
3. **Occupied territories.** Is a "temporarily occupied / active hostilities" flag needed? That list is
   published separately by the Ministry of Reintegration and is not part of the codifier. For AR Crimea
   and Sevastopol the note in the file already gives `IsTemporarilyNotInForce`.
4. **Older .NET.** Is `netstandard2.0` needed, for example for .NET Framework? Then `ISpanParsable` and
   some optimizations would have to go, or the project would need multi-targeting.
5. **Data license.** The State Statistics Service publishes its materials under CC BY 4.0. The
   attribution should be added to the README and the package metadata.
