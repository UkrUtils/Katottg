# UkrUtils.Katottg — посібник для AI-агентів

Стислий довідник для агентів, які пишуть код **з використанням** бібліотеки. Як змінювати саму
бібліотеку, описано в `AGENTS.md` у корені репозиторію. Людська документація — у `README.md`.

Де знайти цей файл: у корені кожного NuGet-пакета (`~/.nuget/packages/ukrutils.katottg/<версія>/AgentGuide.md`,
на Windows — `%USERPROFILE%\.nuget\packages\...`) — саме для встановленої версії; онлайн —
https://github.com/UkrUtils/Katottg/blob/main/Docs/AgentGuide.md (остання версія).

## Що це

КАТОТТГ — офіційний український кодифікатор адміністративно-територіальних одиниць. Кожен об’єкт
(область, район, громада, населений пункт, район у місті) має код `UA` + 17 цифр. Бібліотека містить
вбудовану офіційну редакцію (31 751 запис) і працює без мережі.

```csharp
using UkrUtils.Katottg;                 // усе основне
using UkrUtils.Katottg.Search;          // лише KatottgNormalizer, ApostropheHandling
using UkrUtils.Katottg.Import;          // пакет UkrUtils.Katottg.Import: розбір xlsx

var katottg = KatottgDataset.Embedded;  // синглтон; незмінний; потокобезпечний
```

## Правила (обов’язково)

1. **Ключ — код, ніколи не назва.** Назви повторюються («Іванівка» — 120 разів), однакові назви є навіть
   в одній громаді (місто й селище «Липовець»). Показуючи об’єкт людині, завжди виводьте категорію
   (`DisplayName`) або повну адресу (`FullName`).
2. **У БД зберігайте лише код найнижчого рівня.** Він унікальний. Батьків отримуйте з
   `Find(code)` або фільтруйте в SQL за `code.HierarchyPrefix` (`LIKE 'UA2302%'`). Окремі колонки з
   кодами області чи громади — лише як денормалізація для індексів.
3. **Не вигадуйте коди і не обчислюйте код батька з коду дитини.** `ParentPrefix` — лише префікс без
   хвоста; справжній батько — `unit.Parent` або `FindBySignificantPart(code.ParentPrefix.Value)`.
4. **Не визначайте рівень за номером сегмента чи колонки.** Київ і Севастополь (`K`) не мають району й
   громади; Прип’ять і Чорнобиль (`M`) лежать прямо під областю; райони в містах (`B`) є і під `M`.
   Використовуйте `Region` / `District` / `Community` / `PopulatedPlace` / `CityDistrict`: вони
   дорівнюють `null`, якщо рівня немає.
5. **Не парсіть адресу самостійно**, передайте рядок у `Search(...)`. Він сам розуміє маркери
   (`с.`, `м.`, `смт`, `р-н`, `обл.`, `громада`, `ТГ`), коми, зайві частини (вулиця, індекс),
   апострофи, дефіси, латинські двійники, регістр.
6. **Перевіряйте `Best` на `null`** і, якщо результатів кілька з близькою `Score`, не обирайте
   мовчки: покажіть варіанти користувачеві.
7. **`IsTemporarilyNotInForce`** = `true` для АР Крим і Севастополя: їхні коди набирають чинності після
   повернення територій. Не відкидайте ці об’єкти мовчки, але враховуйте прапорець.

## Карта API

| Задача | Виклик |
|---|---|
| розібрати / перевірити код | `KatottgCode.Parse(s)`, `KatottgCode.TryParse(s, out var c)` |
| об’єкт за кодом | `katottg.Find(code)` → `KatottgUnit?`; `katottg.TryFind("UA…", out var u)` |
| код із втраченим або неправильним хвостом | `katottg.FindBySignificantPart(code)` |
| пошук за назвою / адресою / кодом | `katottg.Search("…", new KatottgSearchOptions { Limit, Categories, Within })` → `.Hits`, `.Best` |
| структурований пошук | `katottg.Search(new KatottgQuery { Name, Region, District, Community, Categories, Limit })` |
| усі рівні адреси з кодами (DTO) | `unit.ToAddress()` → `KatottgAddress { Region, District?, Community?, PopulatedPlace?, CityDistrict? }` |
| шлях від області до об’єкта | `unit.Path`; предки — `unit.Ancestors()` |
| діти / піддерево | `unit.Children`, `unit.Descendants()` |
| корені дерева | `katottg.Regions` (24 області, АР Крим, Київ, Севастополь) |
| дерево результатів для UI | `katottg.Search("…").ToTree()` або `KatottgTreeNode.Build(units, includeDescendants)` |
| чи об’єкт усередині іншого | `ancestor.Contains(unit)` (за об’єктами) або `code.IsDescendantOf(ancestorCode)` (лише за цифрами) |
| SQL-префікс піддерева | `unit.Code.HierarchyPrefix` → `"UA0102"` |
| усі об’єкти категорії | `katottg.GetByCategory(KatottgCategory.City)` |
| версія даних | `katottg.Edition.Date`, `.Source`, `.Sha256` |
| імпорт нового офіційного xlsx | `KatottgSheetParser.Parse(path)` → `.Report`, `.CreateDataset()` |
| зберегти / завантажити знімок | `dataset.Save(stream)`, `KatottgDataset.Load(stream)` |

### Категорії (`KatottgCategory`)

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
| — | `Unknown` — невідома літера (майбутні редакції) | | |

`category.IsPopulatedPlace()` дає `true` для M, T, X, C, K.

## Рецепти

**Нормалізувати адресу з довільного тексту до коду:**

```csharp
var result = katottg.Search(userText);
if (result.Best is not { } best) return null;                       // нічого не знайдено
var ambiguous = result.Hits.Count > 1 && result.Hits[1].Score >= best.Score - 0.01;
return ambiguous ? AskUser(result.Hits) : best.Unit.Code;
```

**Відповідь API з повною ієрархією:**

```csharp
KatottgAddress address = katottg.Find(code)!.ToAddress();
// { Code, Category, Name, FullName, Region{Code,Name,...}, District?, Community?, PopulatedPlace?, CityDistrict? }
```

**Каскадні списки «область → район → громада → населений пункт»:**

```csharp
var regions     = katottg.Regions;                                   // 1-й список
var districts   = region.Children;                                   // P (для Києва — одразу B)
var communities = district.Children;                                 // H
var places      = community.Children;                                // M/T/X/C
```

Не вважайте, що в кожної області є райони і т. д.: перевіряйте `Category` дітей.

**Усі населені пункти громади, відсортовані за назвою:**

```csharp
var places = community.Children.Where(u => u.Category.IsPopulatedPlace()).OrderBy(u => u.Name, StringComparer.Create(new("uk-UA"), false));
```

**Перевірка коду з форми:**

```csharp
if (!KatottgCode.TryParse(input, out var code)) return "Невірний формат: потрібно UA і 17 цифр";
var unit = katottg.Find(code) ?? katottg.FindBySignificantPart(code);
if (unit is null) return "Такого коду немає в чинній редакції";
```

**Фільтр «усе в районі» в EF Core / SQL без бібліотеки на сервері:**

```csharp
var prefix = district.Code.HierarchyPrefix;                          // "UA2302"
query.Where(a => a.KatottgCode.StartsWith(prefix));                  // колонка char(19)
```

## Що бібліотека поки НЕ вміє (не обіцяйте користувачеві)

- нечіткий пошук з друкарськими помилками («Кривий Рыг» ≠ «Кривий Ріг»), пошук латиницею (`Kyiv`);
- порівняння редакцій і міграцію старих кодів (`KatottgDiff`);
- КОАТУУ ↔ КАТОТТГ; історію кодів на дату; перелік окупованих територій;
- автоматичне оновлення з сайту міністерства (дані оновлюються з новою версією пакета);
- JSON-конвертер і EF Core `ValueConverter` для `KatottgCode`: поки використовуйте `ToString()` / `Parse`.

## CLI для швидких перевірок

```bash
katottg search "Шевченківський р-н, м. Харків" --tree
```

```bash
katottg search UA01020010010075540
```
