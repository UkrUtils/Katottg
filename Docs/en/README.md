# UkrUtils.Katottg

> Українська версія: [README.md](https://github.com/UkrUtils/Katottg/blob/main/README.md)

A .NET library for working with the **Codifier of Administrative-Territorial Units and Territories of
Territorial Communities (KATOTTG)**: search by name and address, the hierarchy "region → district →
community → settlement → city district", code validation, and import of the official xlsx file.

Site and documentation: https://ukrutils.github.io/Katottg/

- Works out of the box: the official data is embedded in the package (~360 KB), no network required.
- No dependencies, AOT- and trimming-friendly. The dataset is immutable and safe to share across threads.
- Embedded edition: **03.11.2026** (`KatottgDataset.Embedded.Edition`), 31,751 records.

| Package | Purpose |
|---|---|
| `UkrUtils.Katottg` | models, search, hierarchy, embedded data |
| `UkrUtils.Katottg.Import` | parsing the official xlsx (`KatottgSheetParser`) |
| `UkrUtils.Katottg.Tool` | the `katottg` CLI: `import`, `build-snapshot`, `search` |

```bash
dotnet add package UkrUtils.Katottg
```

There is a separate concise guide for AI agents: [Docs/en/AgentGuide.md](https://github.com/UkrUtils/Katottg/blob/main/Docs/en/AgentGuide.md).

---

## Quick start

```csharp
using UkrUtils.Katottg;

var katottg = KatottgDataset.Embedded;          // lazily loaded once (~12 ms)

// By code
var unit = katottg.Find(KatottgCode.Parse("UA01020010010075540"));
Console.WriteLine(unit!.FullName);
// с. Андріївка, Андріївська громада, Бахчисарайський р-н, АР Крим

// By name or address
var hit = katottg.Search("смт Андріївка, Бердянський р-н, Запорізька обл.").Best!;
Console.WriteLine($"{hit.Unit.Code} {hit.Unit.FullName}");
// UA23020030010042079 смт Андріївка, Андріївська громада, Бердянський р-н, Запорізька обл.
```

## Code (`KatottgCode`)

`UA` + 17 digits. A `readonly struct` over `ulong`: no allocations, fast comparison, `ISpanParsable`,
`ISpanFormattable`, `IUtf8SpanFormattable`.

```
UA 01 02 003 001 00 43419
   │  │  │   │   │  └─ Tail: service suffix (not derived from the hierarchy)
   │  │  │   │   └──── CityDistrict: city district
   │  │  │   └──────── Settlement: settlement within a community
   │  │  └──────────── Community: community within a district
   │  └─────────────── District: district within a region
   └────────────────── Region: region
```

```csharp
var code = KatottgCode.Parse(" ua01020030010043419 ");   // case, whitespace, Cyrillic "УА" are all fine
bool ok  = KatottgCode.TryParse(input, out var parsed);

code.Region;            // 1
code.SignificantPart;   // 010200300100 — 12 digits without the tail, also unique
code.HierarchyPrefix;   // "UA0102003001" — the codes of the unit and all its descendants start with it
code.IsDescendantOf(KatottgCode.Parse("UA01020000000022387"));  // true — by code digits only
code.ParentPrefix;      // UA01020030000000000 — only the parent's prefix, without the tail
```

If the tail of a code is lost or wrong, search with `FindBySignificantPart(code)`.

## Hierarchy (`KatottgUnit`)

Every unit knows its ancestors and children. The links are built once, at load time.

```csharp
var district = katottg.Find(KatottgCode.Parse("UA63120270010948820"))!;   // Shevchenkivskyi district of Kharkiv

district.Category;        // CityDistrict
district.DisplayName;     // "Шевченківський р-н"
district.Parent;          // м. Харків
district.Region;          // Харківська обл.   (O or K; for a root — itself)
district.District;        // Харківський р-н   (P; null for Kyiv, Prypiat)
district.Community;       // Харківська громада (H)
district.PopulatedPlace;  // м. Харків          (for a city district — the city)
district.CityDistrict;    // itself
district.Path;            // [Харківська, Харківський, Харківська, Харків, Шевченківський]
district.Ancestors();     // from the parent up to the root
district.Children;        // children in the order of the official file
district.Descendants();   // all descendants, depth-first
```

### The full tree for a found unit — `ToAddress()`

A flat address with the codes of all levels. It is a value (a record) that does not reference the
dataset, so it is convenient to serialize to JSON or return from an API.

```csharp
KatottgAddress a = hit.Unit.ToAddress();

a.Code; a.Category; a.Name; a.FullName;
a.Region.Code;          a.Region.Name;          // always present
a.District?.Code;       a.Community?.Code;      // null if the level does not exist
a.PopulatedPlace?.Code; a.CityDistrict?.Code;
a.IsTemporarilyNotInForce;                      // AR Crimea and Sevastopol
```

### A tree for display — `KatottgTreeNode`

- **The full tree** is already there in the objects: `katottg.Regions` → `Children` → …; in a UI it is
  convenient to load it lazily.
- **Part of the tree**, for example search results together with their regions, districts and communities:

```csharp
IReadOnlyList<KatottgTreeNode> roots = katottg.Search("Андріївка").ToTree();
// or KatottgTreeNode.Build(units, includeDescendants: false)

void Print(KatottgTreeNode node, int level)
{
    Console.WriteLine($"{new string(' ', level * 2)}{(node.IsMatch ? "* " : "")}{node.Unit.DisplayName} [{node.Unit.Code}]");
    foreach (var child in node.Children) Print(child, level + 1);
}
foreach (var root in roots) Print(root, 0);
```

`IsMatch == true` — a unit that was found; `false` — an ancestor added for context. Nodes follow the
order of the official file, and each unit appears once.

## Search

```csharp
// Free-form string: a name, a code, or an address separated by commas / semicolons
katottg.Search("Кривий Ріг");                         // the city ranks above a village with the same name
katottg.Search("кам'янське");                         // apostrophes, case, hyphens — all ignored
katottg.Search("Запор");                              // prefix autocomplete
katottg.Search("Шевченківський р-н, м. Харків");      // context: the result is inside Kharkiv
katottg.Search("01001, м. Київ, вул. Хрещатик, 1");   // streets, postal codes, numbers are ignored
katottg.Search("UA01020010010000000");                // a code, even with a lost tail

// Options
katottg.Search("Андріївка", new KatottgSearchOptions
{
    Limit = 50,                                       // 20 by default
    Categories = [KatottgCategory.Village],
    Within = katottg.Regions.Single(r => r.Name == "Запорізька"),
});

// Structured query
IReadOnlyList<KatottgSearchHit> hits = katottg.Search(new KatottgQuery
{
    Name = "Андріївка",
    Region = "Запорізька",
    District = "Бердянський",
});
```

Each result is a `KatottgSearchHit(Unit, Score, Kind)`, where `Kind` is `Code`, `Exact`, `Prefix`, or
`WordPrefix` ("Подільський" → "Могилів-Подільський").

**Category markers** become a filter: `с.` (village), `село` (village), `м.` (city), `місто` (city),
`смт` (urban-type settlement), `с-ще` (settlement), `селище` (settlement), `р-н` (district), `район`
(district), `обл.` (region), `область` (region), `громада` (community), `ТГ` / `ОТГ` (territorial
community), and "… міська/сільська/селищна територіальна громада" (city/rural/settlement territorial
community). If nothing is found with the filter, the search is repeated without it: addresses often
state the category inaccurately. `с-ще` also covers urban-type settlements (the urban-type settlement
category is being abolished).

**Normalization** (`UkrUtils.Katottg.Search.KatottgNormalizer`): lower case; Latin look-alikes inside
Cyrillic words ("Вiйтовецька" with a Latin i); apostrophes `' ’ ʼ ` ‘`; hyphens and periods; `ґ→г`,
and `ё/ы/э/ъ` from the Russian layout.

Fuzzy search tolerant of typos and Latin transliteration (`Kyiv`) are not available yet: they are
stages 7–8 of the plan.

## Storing in a database

**A unit's code is unique.** This has been verified on all 31,751 records of the official file: both
the full codes and the 12 significant digits are unique. Import and `KatottgDataset.Create` reject
duplicates.

So **store only the code of the lowest level** (village, city, city district). There is no need to
store the parent codes:

- in the application they come from `katottg.Find(code)` → `Region`, `District`, `Community`… or
  `ToAddress()`;
- in SQL without the library, use the prefix, because the codes are nested:

  ```sql
  -- all addresses in Berdiansk district (the district itself + everything inside it)
  SELECT * FROM addresses WHERE katottg LIKE 'UA2302%';   -- the district's KatottgCode.HierarchyPrefix
  ```

  The invariant "a prefix selects exactly the subtree" is verified by a test on every record,
  including Kyiv, Sevastopol and Prypiat.

| Column type | How |
|---|---|
| `char(19)` / `varchar(19)` | `code.ToString()` ↔ `KatottgCode.Parse` — human-readable, `LIKE` works |
| `bigint` | `(long)code.Value` ↔ `KatottgCode.FromValue((ulong)value)` — more compact |

Denormalizing region or community codes into separate columns only makes sense for indexes and
reports. It is derived data and can be recomputed from the code.

**Between editions** a code may disappear: when a unit is moved to another community, the ministry
usually assigns a new code. Store the edition date alongside (`katottg.Edition.Date`) and, after
updating the package, check that `Find(code)` does not return `null`. Edition comparison and
code-replacement hints (`KatottgDiff`) are planned for stage 9.

**Don't do this:**

- don't use a name as a key: "Іванівка" occurs 120 times, and identical names exist even within one
  community;
- don't compute a parent's full code from a child's code: the parent's tail is not in it;
- don't determine the level by the xlsx column number: the "third level" is not always a community.

## Atypical places in the hierarchy

- **Kyiv and Sevastopol** (`K`) are roots; directly under them are city districts (`B`), with no
  district or community.
- **Prypiat and Chornobyl** (`M`) sit directly under Kyiv region, with no district or community.
- **City districts** (`B`) exist not only in Kyiv and Sevastopol but also in 22 cities under `M`
  (Kharkiv, Dnipro, Kryvyi Rih, Lviv…).
- **AR Crimea and Sevastopol**: the codes take effect once the territories return under Ukraine's
  jurisdiction (a note in the codifier) → `IsTemporarilyNotInForce`.

## Custom data and import

```csharp
using UkrUtils.Katottg.Import;

var result = KatottgSheetParser.Parse("katottg-26-11-03.xlsx");   // the edition date comes from the file name
if (result.Report.HasErrors)
    foreach (var e in result.Report.Errors) Console.WriteLine(e);
var dataset = result.CreateDataset();

using (var file = File.Create("katottg.bin.gz")) dataset.Save(file);   // deterministic snapshot
using (var file = File.OpenRead("katottg.bin.gz")) dataset = KatottgDataset.Load(file);
```

`ImportReport` contains errors (the row is skipped), warnings (the row was imported but looks
suspicious), and informational messages (trimmed whitespace, service rows).

## CLI

```bash
dotnet tool install -g UkrUtils.Katottg.Tool
```

```bash
katottg search "Шевченківський р-н, м. Харків" --tree
```

```bash
katottg import OfficialData/katottg-26-11-03.xlsx --verbose
```

```bash
katottg build-snapshot OfficialData -o Source/UkrUtils.Katottg/Data/katottg.bin.gz
```

## Performance

The dataset takes ~6.5 MB of managed memory, and the name index another ~1.4 MB; the index is built on
the first search (~26 ms). Typical timings: `Find` ~4 ns; exact search ~1 µs; prefix ~4 µs; a
three-part address ~16 µs. Benchmarks live in `Tests/UkrUtils.Katottg.Benchmarks`.

## AI agents in your project

Agents (Claude Code, Copilot, Cursor, Codex…) read the `AGENTS.md` / `CLAUDE.md` of **your**
repository, but not the documentation inside NuGet packages. So that an agent works with KATOTTG
correctly, add this to your `AGENTS.md`:

```markdown
## KATOTTG (UkrUtils.Katottg)

For codes of Ukraine's administrative-territorial units, use the NuGet package UkrUtils.Katottg.
Before writing code with it, read the agent guide:
- locally: ~/.nuget/packages/ukrutils.katottg/<version>/AgentGuide.md (the version is in the .csproj;
  the packaged guide is in Ukrainian);
- online (English): https://github.com/UkrUtils/Katottg/blob/main/Docs/en/AgentGuide.md

Key points: the key is the code (UA + 17 digits), not the name; store only the lowest-level code in
the DB; pass addresses from text to KatottgDataset.Embedded.Search(...); hierarchy levels can be null.
```

The guide sits in the root of every package (`AgentGuide.md`, in Ukrainian), so an agent can read it
offline, for exactly the installed version. The key rules are duplicated in the XML documentation of
the types (`KatottgDataset`, `KatottgCode`), which agents see through IntelliSense or decompilation.

## Data

The data comes from the official Codifier approved by Order No. 290 of the Ministry of Regional
Development of 26.11.2020 (as amended by Order No. 3 of 12.01.2021). The source is the website of the
Ministry for Development of Communities and Territories of Ukraine. The original xlsx files are kept in
[`OfficialData/`](https://github.com/UkrUtils/Katottg/tree/main/OfficialData). The library code is
distributed under the MIT license ([LICENSE](https://github.com/UkrUtils/Katottg/blob/main/LICENSE)).

## Development

How to build, test and update the data is described in [AGENTS.md](https://github.com/UkrUtils/Katottg/blob/main/AGENTS.md) (in Ukrainian). The roadmap is in
[Docs/ImplementationPlan.md](https://github.com/UkrUtils/Katottg/blob/main/Docs/ImplementationPlan.md) (in Ukrainian).
