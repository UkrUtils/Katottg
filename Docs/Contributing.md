# Contributing

> Українською: [Docs/uk/Contributing.md](https://github.com/UkrUtils/Katottg/blob/main/Docs/uk/Contributing.md)

How to build, test and change the library, update the data and cut a release. The requirements and
the domain background are in the [deep dive](https://ukrutils.github.io/Katottg/deep-dive) and the
[roadmap](https://ukrutils.github.io/Katottg/roadmap). AI agents also read `AGENTS.md` at the
repository root, which summarizes the rules below.

## Commands

The SDK is pinned in `global.json` (.NET 10, `rollForward: latestFeature`).

```bash
dotnet build Source/UkrUtils.Katottg.slnx
```

```bash
dotnet test Tests/UkrUtils.Katottg.Tests
```

```bash
dotnet pack Source/UkrUtils.Katottg.slnx -c Release -o artifacts/packages
```

```bash
dotnet run -c Release --project Tests/UkrUtils.Katottg.Benchmarks -- --job short --filter "*"
```

A full test run takes a few seconds and uses the full official file from `OfficialData/`.

The site (VitePress, Node 24 from `site/.nvmrc`): tests and build, as in CI.

```bash
cd site && npm ci && npm test && npm run build
```

Local preview with hot reload:

```bash
cd site && npm run dev
```

The releases page is built from the GitHub Releases API at build time. In CI (the `CI` variable) an
API error fails the build; locally it is only a warning and an empty list. The token comes from
`GITHUB_TOKEN`.

## Repository layout

```
Source/UkrUtils.Katottg/            the dependency-free core: KatottgCode, KatottgCategory, KatottgUnit,
                                    KatottgDataset (+ .Search.cs, .Storage.cs), KatottgAddress,
                                    KatottgTreeNode, Search/ (normalizer, index, query parsing),
                                    Storage/ (binary snapshot), Data/katottg.bin.gz (embedded resource)
Source/UkrUtils.Katottg.Import/     Xlsx/MinimalXlsxReader (zip + XmlReader), KatottgSheetParser, ImportReport
Source/UkrUtils.Katottg.Tool/       the `katottg` CLI (KatottgCli.cs; hand-written argument parsing)
Tests/UkrUtils.Katottg.Tests/       xUnit v3; Fixtures/ — an in-memory xlsx builder and 44 official rows
Tests/UkrUtils.Katottg.Benchmarks/  BenchmarkDotNet
OfficialData/                       the archive of official xlsx files (katottg-YY-MM-DD.xlsx), the snapshot's source
Docs/                               documentation in English (the source); Docs/uk/ — the Ukrainian mirror
site/                               the GitHub Pages site (VitePress, EN at the root, UK under /uk/);
                                    it includes README.md and Docs/*; the releases page comes from the GitHub API
```

## Code rules

- **The core has no dependencies and no reflection** (`IsAotCompatible=true`), and no
  `System.Text.Json`. Dependencies are allowed only in separate packages (`Import`, the future DI
  package).
- **Data is immutable.** `KatottgDataset` and `KatottgUnit` do not change after they are built.
  Collections are exposed as `ReadOnlyCollection` (`AsReadOnly()`), not as arrays or `List<T>`.
- **Language.** Public API XML-doc is in English (IntelliSense). Identifiers are in English. Every
  public member has XML-doc; `GenerateDocumentationFile` is on, so missing docs show up as CS1591
  warnings. Regular code comments are in Ukrainian.
- **TDD:** write the test first, make sure it fails, then implement. Test on real data:
  `FixtureDataset.Official` (the full file) or `FixtureDataset.Value` (44 rows).
- **Atypical data** (Kyiv/Sevastopol `K` without a district and community, Prypiat/Chornobyl `M`
  under a region, `B` under `M`) is a constant source of bugs. Check any hierarchy logic with a test on
  the full dataset.
- **Determinism:** search and the snapshot must give the same result on every run. Always make the
  order unambiguous (the last criterion is the code).

## Testing

- **Codes:** `Parse` / `TryParse` / `ToString` round-trip for all 31,751 codes; invalid input;
  segments; `ParentPrefix`.
- **Import:** a fixture of 44 official rows covering AR Crimea, Kyiv with its districts, Prypiat, a city
  with districts (Kharkiv), names with a trailing space, a Latin `i` and apostrophes. The full official
  file is used in separate tests (`OfficialFileTests`).
- **Hierarchy:** invariants on the full dataset (`HierarchyInvariantTests`): the roots are only `O` and
  `K`; every unit is in the tree; at most 5 levels; `B` lies only under `M` / `K`; `H` only under `P`;
  category counts match the official file. `HierarchyApiTests` checks that a code prefix selects
  exactly the subtree.
- **Search:** queries with known answers and ranking checks (a city above a village with the same
  name), plus regression tests for real addresses.
- **Snapshot:** `Save` → `Load` round-trips; the output is deterministic; the embedded snapshot matches
  the newest xlsx (`SnapshotTests.Embedded_*`).
- **Docs:** the examples from `README.md`, `Docs/Api.md` and `Docs/AgentGuide.md` are repeated in
  `DocsExamplesTests.cs`.

## Documentation

- English is the source. Every page in `Docs/` (and `README.md`) has a Ukrainian mirror in `Docs/uk/`
  with the same file name (`README.md` → `Docs/uk/README.md`). Update both in the same PR.
- The site includes these files; there are no copies. A new page needs a stub in `site/` and
  `site/uk/`, an entry in `nav()` and in `editPattern` in `site/.vitepress/config.mts`.
- Links between pages point to the site (`https://ukrutils.github.io/Katottg/<page>`, the Ukrainian
  pages under `/uk/`): they work on GitHub, on nuget.org (`README.md` is the package readme) and on the
  site.
- When you change the API, update the examples in the docs (both languages) and in
  `DocsExamplesTests.cs`.
- In markdown the site includes, a raw `<…>` outside code breaks the VitePress build: put it in
  backticks. Double curly braces break it even inside inline code (Vue interpolates them): keep them
  in fenced code blocks only.

## Updating the data (a new codifier edition)

1. Put the file in `OfficialData/` named `katottg-YY-MM-DD.xlsx`: the edition date comes from the name.
2. Rebuild the snapshot:
   ```bash
   dotnet run --project Source/UkrUtils.Katottg.Tool -- build-snapshot OfficialData -o Source/UkrUtils.Katottg/Data/katottg.bin.gz
   ```
   The command takes the newest file by name and refuses to write the snapshot if the import has errors.
3. Run the tests. `SnapshotTests.Embedded_*` check that the snapshot matches the newest xlsx. Tests with
   exact counts (`OfficialFileTests`, `HierarchyInvariantTests`) and specific codes (`SearchTests`) may
   fail because of real changes in the data. Check that the changes are real and update the
   expectations.
4. Bump the version (see below) and update the edition date in `README.md` and `Docs/uk/README.md`.

Never edit the snapshot by hand. The format is described in `Storage/SnapshotFormat.cs`; if it changes,
bump `SnapshotFormat.Version`.

## Versioning

The version lives in `Source/Directory.Build.props`: `PackageVersion` plus `FileVersion`,
`AssemblyVersion` and `InformationalVersion`. `VersionDate` is the UTC moment the version was set, in
ISO 8601. It moves with every version bump, any segment including the revision, and never on its own.
It is stamped into every assembly as `[assembly: AssemblyMetadata("VersionDate", ...)]`.

A version with a hyphen (`1.1.0-beta.1`) becomes a prerelease. A new embedded edition always means a
new package version.

## Releasing

A release is one button: **Actions → Release → Run workflow** (branch `main`). The workflow is in
[.github/workflows/release.yml](https://github.com/UkrUtils/Katottg/blob/main/.github/workflows/release.yml).

1. In a PR, bump the version in `Source/Directory.Build.props` and merge it into `main`.
2. Run **Release**. You can do a `dry-run` first: the workflow then only builds, tests and uploads the
   packages as an artifact, from any branch.
3. The workflow refuses if the tag `v<version>` already exists. Otherwise it publishes three packages
   (`.nupkg` + `.snupkg`) to nuget.org and creates the tag and a GitHub Release with the packages and
   generated notes. Re-running after a partial failure is safe (`--skip-duplicate`).

The workflow also updates the site: the `pages` job calls `pages.yml` after `publish`. Releases created
with `GITHUB_TOKEN` don't trigger other workflows, so this is how the releases page gets updated.

No API keys are stored: `NuGet/login` exchanges GitHub's OIDC token for a short-lived key.

### One-time setup

- **nuget.org → Trusted Publishing** (account menu): add a policy with Repository Owner `UkrUtils`,
  Repository `Katottg`, Workflow File `release.yml`, Environment `nuget`. For the first publish the
  package IDs must be free or already belong to this account.
- **GitHub → Settings → Environments**: create the `nuget` environment. Optionally restrict it to
  `main` and add required reviewers (then it is "one button" plus an approval).
- **GitHub → Settings → Secrets and variables → Actions → Variables**: the `NUGET_USER` variable is
  the nuget.org profile name, not an email.
- **GitHub → Settings → Pages → Source: GitHub Actions** (for the site).
- **PayPal Donate button:** `PAYPAL_BUTTON_ID` in `site/.vitepress/site.config.js`. If it is empty,
  the button is hidden and the build warns.

## Pitfalls

- `Data/katottg.bin.gz` is embedded with `WithCulture="false"`. Without it MSBuild takes `.bin` as a
  culture (the Bini language) and puts the resource into a satellite assembly.
- Gzip bytes depend on the zlib version of the runtime, so the snapshot freshness test compares the
  decompressed data.
- The category letters in the official file are Latin but look exactly like Cyrillic ones.
  `FromLetter` accepts both.
- Don't treat a category marker in search as final: real addresses often give the category
  inaccurately (see the fallback in `KatottgDataset.Search.cs`).
- `editLink.pattern` in `site/.vitepress/config.mts` must stay a self-contained function: VitePress
  serializes it into the client bundle, where module-level variables are undefined.

## Git

The main branch is `main`; work happens in feature branches merged through PRs. Commit in logical
steps and explain "why" in the message. Don't commit `bin/`, `obj/` or BenchmarkDotNet results.
