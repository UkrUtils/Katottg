# UkrUtils.Katottg

> Українською: [Docs/uk/README.md](https://github.com/UkrUtils/Katottg/blob/main/Docs/uk/README.md)

A .NET library for the **Codifier of Administrative-Territorial Units and Territories of Territorial
Communities (KATOTTG)**, Ukraine's official register of regions, districts, communities and
settlements. It searches by name and address, walks the hierarchy "region → district → community →
settlement → city district", validates codes and imports the official xlsx.

Site and documentation: https://ukrutils.github.io/Katottg/

- Works out of the box: the official data ships inside the package (~360 KB), no network needed.
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

## Five rules worth knowing up front

1. **The key is the code (`UA` + 17 digits), never the name.** "Іванівка" occurs 120 times, and
   identical names exist even within one community.
2. **Store only the lowest-level code.** Parents come from `Find(code)`; in SQL, filter by the
   prefix (`LIKE 'UA2302%'`).
3. **Don't parse addresses yourself.** Pass the string to `Search(...)`: it handles category markers,
   commas, streets, postal codes, apostrophes and case.
4. **Hierarchy levels can be missing.** Kyiv and Sevastopol have no district or community; Prypiat
   and Chornobyl sit directly under a region. `District`, `Community` and the rest may be `null`.
5. **Show the category to people.** Use `DisplayName` ("с. Андріївка") or `FullName`, not `Name`.

## Documentation

| Page | What is in it |
|---|---|
| [API](https://ukrutils.github.io/Katottg/api) | codes, hierarchy, search, storing codes in a database, import, CLI, performance |
| [Deep dive](https://ukrutils.github.io/Katottg/deep-dive) | the official file and its quirks, the design, the snapshot format, how search works |
| [Agent guide](https://ukrutils.github.io/Katottg/agents) | a compact reference for AI agents that write code with the library |
| [Roadmap](https://ukrutils.github.io/Katottg/roadmap) | what is done, what is planned, open questions |
| [Contributing](https://ukrutils.github.io/Katottg/contributing) | building, testing, updating the data, releasing |

## AI agents in your project

Agents (Claude Code, Copilot, Cursor, Codex…) read the `AGENTS.md` / `CLAUDE.md` of **your**
repository, not the documentation inside NuGet packages. To make an agent use KATOTTG correctly, add
this to your `AGENTS.md`:

```markdown
## KATOTTG (UkrUtils.Katottg)

For codes of Ukraine's administrative-territorial units, use the NuGet package UkrUtils.Katottg.
Before writing code with it, read the agent guide:
- locally: ~/.nuget/packages/ukrutils.katottg/<version>/AgentGuide.md (the version is in the .csproj);
- online: https://github.com/UkrUtils/Katottg/blob/main/Docs/AgentGuide.md

Key points: the key is the code (UA + 17 digits), not the name; store only the lowest-level code in
the DB; pass addresses from text to KatottgDataset.Embedded.Search(...); hierarchy levels can be null.
```

The guide sits in the root of every package (`AgentGuide.md`), so an agent can read it offline, for
exactly the installed version. The key rules are also in the XML documentation of the types
(`KatottgDataset`, `KatottgCode`), which agents see through IntelliSense or decompilation.

## Data and license

The data comes from the official Codifier approved by Order No. 290 of the Ministry of Regional
Development of 26.11.2020 (as amended by Order No. 3 of 12.01.2021). The source is the website of the
Ministry for Development of Communities and Territories of Ukraine. The original xlsx files are kept in
[`OfficialData/`](https://github.com/UkrUtils/Katottg/tree/main/OfficialData). The library code is
distributed under the MIT license ([LICENSE](https://github.com/UkrUtils/Katottg/blob/main/LICENSE)).
