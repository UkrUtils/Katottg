# UkrUtils.Katottg — guide for AI agents

> Українська версія: [AgentGuide.md](https://github.com/UkrUtils/Katottg/blob/main/Docs/AgentGuide.md)

A concise reference for agents that write code **using** the library. How to change the library itself
is described in `AGENTS.md` at the repository root. Human-oriented documentation is in
[README](https://github.com/UkrUtils/Katottg/blob/main/Docs/en/README.md).

Where to find this file: in the root of every NuGet package
(`~/.nuget/packages/ukrutils.katottg/<version>/AgentGuide.md`, on Windows `%USERPROFILE%\.nuget\packages\...`),
exactly for the installed version; the packaged copy is the Ukrainian one. English version online:
https://github.com/UkrUtils/Katottg/blob/main/Docs/en/AgentGuide.md (latest version).

## What it is

KATOTTG is the official Ukrainian codifier of administrative-territorial units. Every unit (region,
district, community, settlement, city district) has a code `UA` + 17 digits. The library contains the
embedded official edition (31,751 records) and works without a network.

```csharp
using UkrUtils.Katottg;                 // everything main
using UkrUtils.Katottg.Search;          // only KatottgNormalizer, ApostropheHandling
using UkrUtils.Katottg.Import;          // the UkrUtils.Katottg.Import package: xlsx parsing

var katottg = KatottgDataset.Embedded;  // singleton; immutable; thread-safe
```

## Rules (mandatory)

1. **The key is the code, never the name.** Names repeat ("Іванівка" — 120 times), and identical names
   exist even within one community (the city and the settlement "Липовець"). When showing a unit to a
   person, always output the category (`DisplayName`) or the full address (`FullName`).
2. **In the DB store only the lowest-level code.** It is unique. Get the parents from `Find(code)` or
   filter in SQL by `code.HierarchyPrefix` (`LIKE 'UA2302%'`). Separate columns with region or community
   codes are only denormalization for indexes.
3. **Don't invent codes and don't compute a parent's code from a child's code.** `ParentPrefix` is only
   a prefix without the tail; the real parent is `unit.Parent` or
   `FindBySignificantPart(code.ParentPrefix.Value)`.
4. **Don't determine the level by segment or column number.** Kyiv and Sevastopol (`K`) have no district
   or community; Prypiat and Chornobyl (`M`) sit directly under a region; city districts (`B`) exist
   under `M` too. Use `Region` / `District` / `Community` / `PopulatedPlace` / `CityDistrict`: they are
   `null` if the level does not exist.
5. **Don't parse an address yourself**, pass the string to `Search(...)`. It understands the markers
   (`с.`, `м.`, `смт`, `р-н`, `обл.`, `громада`, `ТГ`), commas, extra parts (street, postal code),
   apostrophes, hyphens, Latin look-alikes, and case on its own.
6. **Check `Best` for `null`**, and if there are several results with close `Score`, don't pick silently:
   show the options to the user.
7. **`IsTemporarilyNotInForce`** = `true` for AR Crimea and Sevastopol: their codes take effect after the
   territories return. Don't silently discard these units, but take the flag into account.

## API map

| Task | Call |
|---|---|
| parse / validate a code | `KatottgCode.Parse(s)`, `KatottgCode.TryParse(s, out var c)` |
| unit by code | `katottg.Find(code)` → `KatottgUnit?`; `katottg.TryFind("UA…", out var u)` |
| code with a lost or wrong tail | `katottg.FindBySignificantPart(code)` |
| search by name / address / code | `katottg.Search("…", new KatottgSearchOptions { Limit, Categories, Within })` → `.Hits`, `.Best` |
| structured search | `katottg.Search(new KatottgQuery { Name, Region, District, Community, Categories, Limit })` |
| all address levels with codes (DTO) | `unit.ToAddress()` → `KatottgAddress { Region, District?, Community?, PopulatedPlace?, CityDistrict? }` |
| path from the region to the unit | `unit.Path`; ancestors — `unit.Ancestors()` |
| children / subtree | `unit.Children`, `unit.Descendants()` |
| tree roots | `katottg.Regions` (24 regions, AR Crimea, Kyiv, Sevastopol) |
| result tree for a UI | `katottg.Search("…").ToTree()` or `KatottgTreeNode.Build(units, includeDescendants)` |
| whether a unit is inside another | `ancestor.Contains(unit)` (by objects) or `code.IsDescendantOf(ancestorCode)` (by digits only) |
| SQL prefix of a subtree | `unit.Code.HierarchyPrefix` → `"UA0102"` |
| all units of a category | `katottg.GetByCategory(KatottgCategory.City)` |
| data version | `katottg.Edition.Date`, `.Source`, `.Sha256` |
| import a new official xlsx | `KatottgSheetParser.Parse(path)` → `.Report`, `.CreateDataset()` |
| save / load a snapshot | `dataset.Save(stream)`, `KatottgDataset.Load(stream)` |

### Categories (`KatottgCategory`)

| Letter | Meaning | Typical parent | `DisplayName` |
|---|---|---|---|
| `O` | `Region` — region, AR Crimea | — | "Харківська обл.", "АР Крим" |
| `K` | `SpecialStatusCity` — Kyiv, Sevastopol | — | "м. Київ" |
| `P` | `District` — district | `O` | "Харківський р-н" |
| `H` | `Community` — territorial community | `P` | "Харківська громада" |
| `M` | `City` — city | `H` (Prypiat, Chornobyl — `O`) | "м. Харків" |
| `T` | `UrbanTypeSettlement` — urban-type settlement | `H` | "смт Андріївка" |
| `X` | `Settlement` — settlement | `H` | "с-ще Липовець" |
| `C` | `Village` — village | `H` | "с. Андріївка" |
| `B` | `CityDistrict` — city district | `M` or `K` | "Шевченківський р-н" |
| — | `Unknown` — unknown letter (future editions) | | |

`category.IsPopulatedPlace()` returns `true` for M, T, X, C, K.

## Recipes

**Normalize an address from arbitrary text to a code:**

```csharp
var result = katottg.Search(userText);
if (result.Best is not { } best) return null;                       // nothing found
var ambiguous = result.Hits.Count > 1 && result.Hits[1].Score >= best.Score - 0.01;
return ambiguous ? AskUser(result.Hits) : best.Unit.Code;
```

**An API response with the full hierarchy:**

```csharp
KatottgAddress address = katottg.Find(code)!.ToAddress();
// { Code, Category, Name, FullName, Region{Code,Name,...}, District?, Community?, PopulatedPlace?, CityDistrict? }
```

**Cascading lists "region → district → community → settlement":**

```csharp
var regions     = katottg.Regions;                                   // 1st list
var districts   = region.Children;                                   // P (for Kyiv — B directly)
var communities = district.Children;                                 // H
var places      = community.Children;                                // M/T/X/C
```

Don't assume every region has districts, and so on: check the `Category` of the children.

**All settlements of a community, sorted by name:**

```csharp
var places = community.Children.Where(u => u.Category.IsPopulatedPlace()).OrderBy(u => u.Name, StringComparer.Create(new("uk-UA"), false));
```

**Validating a code from a form:**

```csharp
if (!KatottgCode.TryParse(input, out var code)) return "Невірний формат: потрібно UA і 17 цифр";   // "Invalid format: UA and 17 digits required"
var unit = katottg.Find(code) ?? katottg.FindBySignificantPart(code);
if (unit is null) return "Такого коду немає в чинній редакції";                                    // "No such code in the current edition"
```

**An "everything in the district" filter in EF Core / SQL without the library on the server:**

```csharp
var prefix = district.Code.HierarchyPrefix;                          // "UA2302"
query.Where(a => a.KatottgCode.StartsWith(prefix));                  // a char(19) column
```

## What the library can NOT do yet (don't promise this to the user)

- fuzzy search with typos ("Кривий Рыг" ≠ "Кривий Ріг"), search in Latin script (`Kyiv`);
- edition comparison and migration of old codes (`KatottgDiff`);
- KOATUU ↔ KATOTTG; code history for a given date; a list of occupied territories;
- automatic updates from the ministry's website (the data is updated with a new package version);
- a JSON converter and an EF Core `ValueConverter` for `KatottgCode`: for now use `ToString()` / `Parse`.

## CLI for quick checks

```bash
katottg search "Шевченківський р-н, м. Харків" --tree
```

```bash
katottg search UA01020010010075540
```
