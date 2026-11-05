---
layout: home
hero:
  name: UkrUtils.Katottg
  text: The KATOTTG codifier for .NET
  tagline: A .NET library for the Codifier of administrative-territorial units and territories of territorial communities (KATOTTG).
  actions:
    - theme: brand
      text: Docs
      link: /en/guide
    - theme: alt
      text: GitHub
      link: https://github.com/UkrUtils/Katottg
    - theme: alt
      text: Donate
      link: /en/donate
features:
  - title: Embedded data
    details: The official data ships in the package (~360 KB, 31,751 records); no network needed.
  - title: No dependencies
    details: The core has no dependencies; the dataset is immutable and thread-safe.
  - title: AOT and trimming
    details: No reflection; friendly to Native AOT and trimming.
  - title: Search by name and address
    details: Free-form string, code or comma-separated address; category markers, prefixes, normalization.
  - title: Hierarchy
    details: Region → district → community → settlement → city district; ancestors, children, full address.
  - title: The katottg CLI
    details: A command-line tool — import, build-snapshot and search.
---

```bash
dotnet add package UkrUtils.Katottg
```

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
