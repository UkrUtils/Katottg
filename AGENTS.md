# AGENTS.md — working on the UkrUtils.Katottg repository

Instructions for AI agents (and people) who **change** the library. The full guide is
[Docs/Contributing.md](Docs/Contributing.md): read it before your first change. How to *use* the
library is in [README.md](README.md), [Docs/Api.md](Docs/Api.md) and
[Docs/AgentGuide.md](Docs/AgentGuide.md). The domain background (the official file, its quirks, how
search works) is in [Docs/DeepDive.md](Docs/DeepDive.md); planned work is in
[Docs/Roadmap.md](Docs/Roadmap.md).

## Commands

```bash
dotnet build Source/UkrUtils.Katottg.slnx
```

```bash
dotnet test Tests/UkrUtils.Katottg.Tests
```

```bash
cd site && npm ci && npm test && npm run build
```

Packing, benchmarks, the data update and the release are in [Docs/Contributing.md](Docs/Contributing.md).

## Rules

- **The core has no dependencies and no reflection** (`IsAotCompatible=true`), no `System.Text.Json`.
  Dependencies only in separate packages.
- **Data is immutable.** `KatottgDataset` and `KatottgUnit` never change after they are built;
  collections are `ReadOnlyCollection` (`AsReadOnly()`).
- **TDD:** test first, see it fail, then implement. Test on real data: `FixtureDataset.Official` (the
  full file) or `FixtureDataset.Value` (44 rows). Check any hierarchy logic on the full dataset: Kyiv /
  Sevastopol (`K` without a district and community), Prypiat / Chornobyl (`M` under a region) and `B`
  under `M` break naive code.
- **Determinism:** search and the snapshot give the same result on every run; every sort ends with an
  unambiguous criterion (the code).
- **Language:** public XML-doc and identifiers in English, every public member documented (CS1591).
  Regular code comments in Ukrainian.
- **Docs:** English is the source; every page in `Docs/` and `README.md` has a Ukrainian mirror in
  `Docs/uk/` — update both in the same PR. Doc examples are repeated in `DocsExamplesTests.cs`; when
  the API changes, update all of them.
- **Never edit `Data/katottg.bin.gz` by hand**: rebuild it with `katottg build-snapshot` (see
  Contributing). If the snapshot format changes, bump `SnapshotFormat.Version`.
- **Versions:** a version bump in `Source/Directory.Build.props` always moves `VersionDate` too.

## Pitfalls

- `Data/katottg.bin.gz` needs `WithCulture="false"`, or MSBuild puts it into a satellite assembly.
- Gzip bytes depend on the runtime's zlib: compare decompressed snapshots, not files.
- Category letters in the official file are Latin look-alikes of Cyrillic; `FromLetter` accepts both.
- A category marker in a search query is not final: keep the fallback in `KatottgDataset.Search.cs`.
- In markdown the site includes, raw `<…>` outside code breaks VitePress: use backticks. Double curly
  braces break it even in inline code: only in fenced code blocks.
- `editLink.pattern` in `site/.vitepress/config.mts` must stay self-contained (it is serialized into
  the client bundle).

## Git

Main branch `main`; work in feature branches. Commit in logical steps and explain "why". Don't commit
`bin/`, `obj/`, BenchmarkDotNet results or `Docs/superpowers/` (local agent notes, git-ignored).
