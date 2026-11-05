# API

> English: [Docs/Api.md](https://github.com/UkrUtils/Katottg/blob/main/Docs/Api.md)

Як користуватися бібліотекою, тип за типом. Швидкий старт — в
[огляді](https://ukrutils.github.io/Katottg/uk/overview); чому все влаштовано саме так — у
[поглибленому описі](https://ukrutils.github.io/Katottg/uk/deep-dive).

```csharp
using UkrUtils.Katottg;                 // усе основне
using UkrUtils.Katottg.Search;          // лише KatottgNormalizer, ApostropheHandling
using UkrUtils.Katottg.Import;          // пакет UkrUtils.Katottg.Import: розбір xlsx

var katottg = KatottgDataset.Embedded;  // синглтон; незмінний; потокобезпечний
```

## Набір даних (`KatottgDataset`)

`KatottgDataset.Embedded` завантажує вбудований у пакет знімок при першому зверненні (~12 мс) і далі
повертає той самий екземпляр. Після побудови набір даних не змінюється, тож його можна спільно
використовувати з багатьох потоків без блокувань.

| Член | Що робить |
|---|---|
| `Edition` | `Date`, `Source` (ім’я файлу) і `Sha256` офіційного файлу, з якого взято дані |
| `Count`, `All` | усі об’єкти в порядку офіційного файлу |
| `Regions` | 27 коренів: 24 області, АР Крим, Київ і Севастополь |
| `Find(code)` | об’єкт за повним кодом або `null` |
| `TryFind("UA…", out var unit)` | те саме, одразу з рядка |
| `FindBySignificantPart(code)` | об’єкт за першими 12 цифрами, без огляду на втрачений чи неправильний хвіст |
| `GetByCategory(category)` | усі об’єкти однієї категорії |
| `Search(...)` | див. [Пошук](#пошук) |
| `Create(edition, records)`, `ToRecords()` | побудувати набір даних із власних записів і навпаки |
| `Save(stream)`, `Load(stream)` | див. [Власні дані та імпорт](#власні-дані-та-імпорт) |

## Код (`KatottgCode`)

`UA` + 17 цифр. `readonly struct` поверх `ulong`: без алокацій, швидке порівняння, `ISpanParsable`,
`ISpanFormattable`, `IUtf8SpanFormattable`.

```
UA 01 02 003 001 00 43419
   │  │  │   │   │  └─ Tail: службовий хвіст (не виводиться з ієрархії)
   │  │  │   │   └──── CityDistrict: район у місті
   │  │  │   └──────── Settlement: населений пункт у громаді
   │  │  └──────────── Community: громада в районі
   │  └─────────────── District: район в області
   └────────────────── Region: область
```

```csharp
var code = KatottgCode.Parse(" ua01020030010043419 ");   // регістр, пробіли, кириличне «УА» — ок
bool ok  = KatottgCode.TryParse(input, out var parsed);

code.Region;            // 1
code.SignificantPart;   // 010200300100 — 12 цифр без хвоста, теж унікальні
code.HierarchyPrefix;   // "UA0102003001" — з нього починаються коди об’єкта та всіх нащадків
code.IsDescendantOf(KatottgCode.Parse("UA01020000000022387"));  // true — лише за цифрами коду
code.ParentPrefix;      // UA01020030000000000 — лише префікс батька, без хвоста
```

Якщо хвіст коду втрачено або він неправильний, шукайте об’єкт через `FindBySignificantPart(code)`.

## Категорії (`KatottgCategory`)

| Літера | Значення | Типовий батько | `DisplayName` |
|---|---|---|---|
| `O` | `Region` — область, АР Крим | — | «Харківська обл.», «АР Крим» |
| `K` | `SpecialStatusCity` — Київ, Севастополь | — | «м. Київ» |
| `P` | `District` — район | `O` | «Харківський р-н» |
| `H` | `Community` — територіальна громада | `P` | «Харківська громада» |
| `M` | `City` — місто | `H` (Прип’ять, Чорнобиль — `O`) | «м. Харків» |
| `T` | `UrbanTypeSettlement` — СМТ | `H` | «смт Андріївка» |
| `X` | `Settlement` — селище | `H` | «с-ще Липовець» |
| `C` | `Village` — село | `H` | «с. Андріївка» |
| `B` | `CityDistrict` — район у місті | `M` або `K` | «Шевченківський р-н» |
| — | `Unknown` — літера, якої бібліотека ще не знає (майбутні редакції) | | |

Методи розширення: `IsPopulatedPlace()` (`true` для M, T, X, C, K), `ToUkrainianName()` («село»),
`ToShortPrefix()` («с.», «м.», «смт», «с-ще»), `ToShortSuffix()` («обл.», «р-н», «громада»),
`ToLetter()`, а також `KatottgCategoryExtensions.FromLetter(c)`, який приймає і латинську літеру, і
схожу на неї кириличну.

## Ієрархія (`KatottgUnit`)

Кожен об’єкт знає своїх предків і дітей. Посилання будуються один раз під час завантаження.

```csharp
var district = katottg.Find(KatottgCode.Parse("UA63120270010948820"))!;   // Шевченківський р-н м. Харків

district.Category;        // CityDistrict
district.DisplayName;     // "Шевченківський р-н"
district.Parent;          // м. Харків
district.Region;          // Харківська обл.   (O або K; для кореня — він сам)
district.District;        // Харківський р-н   (P; null для Києва, Прип’яті)
district.Community;       // Харківська громада (H)
district.PopulatedPlace;  // м. Харків          (для району в місті — місто)
district.CityDistrict;    // він сам
district.Path;            // [Харківська, Харківський, Харківська, Харків, Шевченківський]
district.Ancestors();     // від батька до кореня
district.Children;        // діти в порядку офіційного файлу
district.Descendants();   // усі нащадки, у глибину
```

Також: `Name` (як в офіційному файлі, з обрізаними пробілами), `FullName` (уся адреса, від об’єкта до
області), `Depth` (0 для кореня), `Contains(unit)` і `IsTemporarilyNotInForce` (див.
[Нетипові місця](#нетипові-місця-в-дереві)).

### Повне дерево для знайденого об’єкта — `ToAddress()`

Пласка адреса з кодами всіх рівнів. Це значення (record), воно не посилається на набір даних, тож його
зручно серіалізувати в JSON або повертати з API.

```csharp
KatottgAddress a = hit.Unit.ToAddress();

a.Code; a.Category; a.Name; a.FullName;
a.Region.Code;          a.Region.Name;          // завжди є
a.District?.Code;       a.Community?.Code;      // null, якщо рівня немає
a.PopulatedPlace?.Code; a.CityDistrict?.Code;
a.IsTemporarilyNotInForce;                      // АР Крим і Севастополь
```

### Дерево для відображення — `KatottgTreeNode`

- **Повне дерево** вже є в об’єктах: `katottg.Regions` → `Children` → …; у UI його зручно
  підвантажувати ліниво.
- **Частина дерева**, наприклад результати пошуку разом з областями, районами й громадами:

```csharp
IReadOnlyList<KatottgTreeNode> roots = katottg.Search("Андріївка").ToTree();
// або KatottgTreeNode.Build(units, includeDescendants: false)

void Print(KatottgTreeNode node, int level)
{
    Console.WriteLine($"{new string(' ', level * 2)}{(node.IsMatch ? "* " : "")}{node.Unit.DisplayName} [{node.Unit.Code}]");
    foreach (var child in node.Children) Print(child, level + 1);
}
foreach (var root in roots) Print(root, 0);
```

`IsMatch == true` — знайдений об’єкт; `false` — предок, доданий для контексту. Порядок вузлів — як
в офіційному файлі, кожен об’єкт трапляється один раз.

## Пошук

```csharp
// Вільний рядок: назва, код або адреса через коми / крапку з комою
katottg.Search("Кривий Ріг");                         // місто вище за однойменне село
katottg.Search("кам'янське");                         // апострофи, регістр, дефіси — байдуже
katottg.Search("Запор");                              // автодоповнення за префіксом
katottg.Search("Шевченківський р-н, м. Харків");      // контекст: результат усередині Харкова
katottg.Search("01001, м. Київ, вул. Хрещатик, 1");   // вулиці, індекси, номери ігноруються
katottg.Search("UA01020010010000000");                // код, навіть із втраченим хвостом

// Параметри
katottg.Search("Андріївка", new KatottgSearchOptions
{
    Limit = 50,                                       // за замовчуванням 20
    Categories = [KatottgCategory.Village],
    Within = katottg.Regions.Single(r => r.Name == "Запорізька"),
});

// Структурований запит
IReadOnlyList<KatottgSearchHit> hits = katottg.Search(new KatottgQuery
{
    Name = "Андріївка",
    Region = "Запорізька",
    District = "Бердянський",
});
```

Пошук за вільним рядком повертає `KatottgSearchResult` з `Hits` (найкращі першими) і `Best` (`null`,
якщо нічого не знайдено). Кожен результат — `KatottgSearchHit(Unit, Score, Kind)`, де `Kind`: `Code`,
`Exact`, `Prefix` або `WordPrefix` («Подільський» → «Могилів-Подільський»). `Score` порівнюйте лише
в межах одного пошуку.

**Маркери категорій** стають фільтром: `с.`, `село`, `м.`, `місто`, `смт`, `с-ще`, `селище`, `р-н`,
`район`, `обл.`, `область`, `громада`, `ТГ` / `ОТГ`, «… міська/сільська/селищна територіальна
громада». Якщо з фільтром нічого не знайдено, пошук повторюється без нього: категорію в адресах часто
вказують неточно. `с-ще` охоплює й СМТ, бо категорію СМТ скасовують.

**Нормалізація** (`UkrUtils.Katottg.Search.KatottgNormalizer`): нижній регістр; латинські двійники в
кириличних словах («Вiйтовецька» з латинською i); апострофи ``' ’ ʼ ` ‘``; дефіси й крапки; `ґ→г`,
`ё/ы/э/ъ` з російської розкладки.

Нечіткого пошуку з друкарськими помилками та пошуку латинською транслітерацією (`Kyiv`) поки немає;
див. [дорожню карту](https://ukrutils.github.io/Katottg/uk/roadmap).

## Зберігання в базі даних

**Код об’єкта унікальний.** Це перевірено на всіх 31 751 записах офіційного файлу: унікальні і повні
коди, і 12 значущих цифр. Імпорт і `KatottgDataset.Create` відкидають дублікати.

Тому **зберігайте лише код найнижчого рівня** (село, місто, район у місті). Батьківські коди
зберігати не треба:

- у застосунку їх дає `katottg.Find(code)` → `Region`, `District`, `Community`… або
  `ToAddress()`;
- у SQL без бібліотеки — за префіксом, бо коди вкладені:

  ```sql
  -- усі адреси в Бердянському районі (сам район + усе, що в ньому)
  SELECT * FROM addresses WHERE katottg LIKE 'UA2302%';   -- KatottgCode.HierarchyPrefix району
  ```

  Інваріант «префікс вибирає рівно піддерево» перевірено тестом на кожному записі, разом із Києвом,
  Севастополем і Прип’яттю.

| Тип колонки | Як |
|---|---|
| `char(19)` / `varchar(19)` | `code.ToString()` ↔ `KatottgCode.Parse` — читається людиною, працює `LIKE` |
| `bigint` | `(long)code.Value` ↔ `KatottgCode.FromValue((ulong)value)` — компактніше |

Денормалізувати коди області чи громади в окремі колонки має сенс лише для індексів і звітів. Це
похідні дані, їх можна перерахувати з коду.

**Між редакціями** код може зникнути: коли об’єкт переносять в іншу громаду, міністерство зазвичай
дає новий код. Зберігайте поруч дату редакції (`katottg.Edition.Date`) і після оновлення пакета
перевіряйте, що `Find(code)` не повертає `null`. Порівняння редакцій і підказки заміни кодів
(`KatottgDiff`) є в [дорожній карті](https://ukrutils.github.io/Katottg/uk/roadmap).

**Не робіть так:**

- не використовуйте назву як ключ: «Іванівка» трапляється 120 разів, а однакові назви є навіть
  в одній громаді;
- не обчислюйте повний код батька з коду дитини: хвоста батька там немає;
- не визначайте рівень за номером колонки xlsx: «третій рівень» не завжди означає громаду.

## Нетипові місця в дереві

- **Київ і Севастополь** (`K`) — корені; під ними одразу райони в місті (`B`), без району й громади.
- **Прип’ять і Чорнобиль** (`M`) — прямо під Київською областю, без району й громади.
- **Райони в містах** (`B`) є не лише в Києві та Севастополі, а й у 22 містах під `M` (Харків, Дніпро,
  Кривий Ріг, Львів…).
- **АР Крим і Севастополь**: коди набирають чинності з повернення територій під юрисдикцію України
  (примітка до кодифікатора) → `IsTemporarilyNotInForce`.

## Власні дані та імпорт

```csharp
using UkrUtils.Katottg.Import;

var result = KatottgSheetParser.Parse("katottg-26-11-03.xlsx");   // дата редакції — з імені файлу
if (result.Report.HasErrors)
    foreach (var e in result.Report.Errors) Console.WriteLine(e);
var dataset = result.CreateDataset();

using (var file = File.Create("katottg.bin.gz")) dataset.Save(file);   // детермінований знімок
using (var file = File.OpenRead("katottg.bin.gz")) dataset = KatottgDataset.Load(file);
```

`ImportReport` містить помилки (рядок пропущено), попередження (рядок імпортовано, але він підозрілий)
та інформаційні повідомлення (обрізані пробіли, пропущені службові рядки); рядки «Примітка» потрапляють у `Notes`. Якщо в імені файлу немає дати,
передайте її в `KatottgImportOptions.EditionDate`; без цього парсер візьме дату з `docProps/core.xml`.

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

`search` також приймає `--limit <n>` і `--snapshot <path>`. `import` і `build-snapshot` приймають файл
або каталог; з каталогу береться найновіший за іменем `katottg-YY-MM-DD.xlsx`.

## Продуктивність

Набір займає ~6.5 МБ керованої пам’яті, індекс назв — ще ~1.4 МБ; індекс будується при першому пошуку
(~26 мс). Типові часи: `Find` ~4 нс; точний пошук ~1 мкс; префікс ~4 мкс; адреса з трьох частин
~16 мкс. Бенчмарки лежать у `Tests/UkrUtils.Katottg.Benchmarks`.
