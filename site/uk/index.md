---
layout: home
hero:
  name: UkrUtils.Katottg
  text: Кодифікатор КАТОТТГ для .NET
  tagline: Бібліотека .NET для роботи з Кодифікатором адміністративно-територіальних одиниць та територій територіальних громад (КАТОТТГ).
  actions:
    - theme: brand
      text: Документація
      link: /uk/overview
    - theme: alt
      text: GitHub
      link: https://github.com/UkrUtils/Katottg
    - theme: alt
      text: Підтримати
      link: /uk/donate
features:
  - title: Вбудовані дані
    details: Офіційні дані вже в пакеті (~360 КБ, 31 751 запис), мережа не потрібна.
  - title: Без залежностей
    details: Ядро не має залежностей, а набір даних незмінний і безпечний для багатьох потоків.
  - title: AOT і trimming
    details: Без рефлексії, дружня до Native AOT і trimming.
  - title: Пошук за назвою й адресою
    details: Вільний рядок, код або адреса через коми; маркери категорій, префікси, нормалізація.
  - title: Ієрархія
    details: Область → район → громада → населений пункт → район у місті; предки, діти, повна адреса.
  - title: CLI katottg
    details: Інструмент командного рядка — import, build-snapshot і search.
---

```bash
dotnet add package UkrUtils.Katottg
```

```csharp
using UkrUtils.Katottg;

var katottg = KatottgDataset.Embedded;          // ліниво завантажується один раз (~12 мс)

// За кодом
var unit = katottg.Find(KatottgCode.Parse("UA01020010010075540"));
Console.WriteLine(unit!.FullName);
// с. Андріївка, Андріївська громада, Бахчисарайський р-н, АР Крим

// За назвою або адресою
var hit = katottg.Search("смт Андріївка, Бердянський р-н, Запорізька обл.").Best!;
Console.WriteLine($"{hit.Unit.Code} {hit.Unit.FullName}");
// UA23020030010042079 смт Андріївка, Андріївська громада, Бердянський р-н, Запорізька обл.
```
