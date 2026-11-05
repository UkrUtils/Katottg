# Як долучитися

> English: [Docs/Contributing.md](https://github.com/UkrUtils/Katottg/blob/main/Docs/Contributing.md)

Як збирати, тестувати й змінювати бібліотеку, оновлювати дані та випускати реліз. Вимоги й контекст
предметної області описано на сторінці [Поглиблено](https://ukrutils.github.io/Katottg/uk/deep-dive)
і в [дорожній карті](https://ukrutils.github.io/Katottg/uk/roadmap). AI-агенти також читають
`AGENTS.md` у корені репозиторію: там стисло викладено правила, наведені нижче.

## Команди

SDK зафіксовано в `global.json` (.NET 10, `rollForward: latestFeature`).

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

Повний прогін тестів триває кілька секунд і використовує повний офіційний файл з `OfficialData/`.

Сайт (VitePress, Node 24 з `site/.nvmrc`): тести й збірка, як у CI.

```bash
cd site && npm ci && npm test && npm run build
```

Локальний перегляд із перезавантаженням:

```bash
cd site && npm run dev
```

Сторінка релізів будується з GitHub Releases API під час збірки. У CI (змінна `CI`) помилка API
валить збірку, локально — лише попередження й порожній список. Токен береться з `GITHUB_TOKEN`.

## Структура репозиторію

```
Source/UkrUtils.Katottg/            ядро без залежностей: KatottgCode, KatottgCategory, KatottgUnit,
                                    KatottgDataset (+ .Search.cs, .Storage.cs), KatottgAddress,
                                    KatottgTreeNode, Search/ (нормалізатор, індекс, розбір запиту),
                                    Storage/ (бінарний знімок), Data/katottg.bin.gz (вбудований ресурс)
Source/UkrUtils.Katottg.Import/     Xlsx/MinimalXlsxReader (zip + XmlReader), KatottgSheetParser, ImportReport
Source/UkrUtils.Katottg.Tool/       CLI `katottg` (KatottgCli.cs; ручний розбір аргументів)
Tests/UkrUtils.Katottg.Tests/       xUnit v3; Fixtures/ — генератор xlsx у пам’яті та 44 офіційні рядки
Tests/UkrUtils.Katottg.Benchmarks/  BenchmarkDotNet
OfficialData/                       архів офіційних xlsx (katottg-YY-MM-DD.xlsx) — джерело знімка
Docs/                               документація англійською (джерело); Docs/uk/ — українське дзеркало
site/                               сайт GitHub Pages (VitePress, EN у корені, UK у /uk/);
                                    вставляє README.md і Docs/*; сторінка релізів — з GitHub API
```

## Правила коду

- **Ядро — без залежностей і без рефлексії** (`IsAotCompatible=true`), без `System.Text.Json`.
  Залежності допустимі лише в окремих пакетах (`Import`, майбутній DI-пакет).
- **Дані незмінні.** `KatottgDataset` і `KatottgUnit` після побудови не змінюються. Колекції
  віддаються як `ReadOnlyCollection` (`AsReadOnly()`), а не як масиви чи `List<T>`.
- **Мова.** XML-doc публічного API — англійською (IntelliSense). Ідентифікатори — англійською. Кожен
  публічний член має XML-doc; `GenerateDocumentationFile` увімкнено, тож пропущену документацію видно
  як попередження CS1591. Звичайні коментарі в коді — українською.
- **TDD:** спершу тест, переконайтеся, що він падає, потім реалізація. Перевіряйте на реальних даних:
  `FixtureDataset.Official` (повний файл) або `FixtureDataset.Value` (44 рядки).
- **Нетипові дані** (Київ/Севастополь `K` без району й громади, Прип’ять/Чорнобиль `M` під областю,
  `B` під `M`) — постійне джерело помилок. Будь-яку логіку ієрархії перевіряйте тестом на повному
  наборі.
- **Детермінізм:** пошук і знімок мають давати однаковий результат при кожному запуску. Порядок завжди
  доводьте до однозначного (останній критерій — код).

## Тестування

- **Коди:** `Parse` / `TryParse` / `ToString` проходять туди й назад для всіх 31 751 кодів;
  некоректні входи; сегменти; `ParentPrefix`.
- **Імпорт:** фікстура з 44 офіційних рядків, яка покриває АР Крим, Київ із районами, Прип’ять, місто
  з районами (Харків), назви з пробілом у кінці, латинською `i` та апострофами. Повний офіційний файл
  використовується в окремих тестах (`OfficialFileTests`).
- **Ієрархія:** інваріанти на повному наборі (`HierarchyInvariantTests`): корені — лише `O` і `K`;
  кожен об’єкт є в дереві; не більше 5 рівнів; `B` лежить тільки під `M` / `K`; `H` — тільки під `P`;
  кількості категорій збігаються з офіційним файлом. `HierarchyApiTests` перевіряє, що префікс коду
  вибирає рівно піддерево.
- **Пошук:** запити з відомими відповідями й перевірки ранжування (місто вище за однойменне село), а
  також регресійні тести на реальних адресах.
- **Знімок:** `Save` → `Load` проходять туди й назад; вивід детермінований; вбудований знімок
  відповідає найновішому xlsx (`SnapshotTests.Embedded_*`).
- **Документація:** приклади з `README.md`, `Docs/Api.md` і `Docs/AgentGuide.md` дублюються в
  `DocsExamplesTests.cs`.

## Документація

- Джерело — англійська. Кожна сторінка в `Docs/` (і `README.md`) має українське дзеркало в `Docs/uk/`
  з тим самим іменем файлу (`README.md` → `Docs/uk/README.md`). Оновлюйте обидві в тому самому PR.
- Сайт вставляє ці файли; копій немає. Для нової сторінки потрібні заглушки в `site/` і `site/uk/`,
  запис у `nav()` і в `editPattern` у `site/.vitepress/config.mts`.
- Посилання між сторінками ведуть на сайт (`https://ukrutils.github.io/Katottg/<page>`, українські
  сторінки — під `/uk/`): вони працюють на GitHub, на nuget.org (`README.md` — це readme пакета) і на
  сайті.
- Змінюючи API, оновлюйте приклади в документації (обома мовами) і в `DocsExamplesTests.cs`.
- У markdown, який вставляє сайт, сирі `<…>` поза кодом ламають збірку VitePress — беріть їх у
  бектики. Подвійні фігурні дужки ламають її навіть в інлайн-коді (Vue їх інтерполює): тримайте їх
  лише в блоках коду.

## Оновлення даних (нова редакція кодифікатора)

1. Покладіть файл у `OfficialData/` з іменем `katottg-YY-MM-DD.xlsx`: з імені береться дата редакції.
2. Перебудуйте знімок:
   ```bash
   dotnet run --project Source/UkrUtils.Katottg.Tool -- build-snapshot OfficialData -o Source/UkrUtils.Katottg/Data/katottg.bin.gz
   ```
   Команда бере найновіший файл за іменем і відмовляється писати знімок, якщо в імпорті є помилки.
3. Запустіть тести. `SnapshotTests.Embedded_*` перевіряють, що знімок відповідає найновішому xlsx.
   Тести з точними кількостями (`OfficialFileTests`, `HierarchyInvariantTests`) і конкретними кодами
   (`SearchTests`) можуть упасти через реальні зміни даних. Перевірте, що зміни справжні, і оновіть
   очікування.
4. Підвищте версію (див. нижче) і оновіть дату редакції в `README.md` і `Docs/uk/README.md`.

Знімок не редагуйте вручну. Формат описано в `Storage/SnapshotFormat.cs`. Якщо формат змінюється,
підвищуйте `SnapshotFormat.Version`.

## Версіонування

Версія задається в `Source/Directory.Build.props`: `PackageVersion`, а також `FileVersion`,
`AssemblyVersion` і `InformationalVersion`. `VersionDate` — момент UTC, коли встановлено версію, у
форматі ISO 8601. Він змінюється з кожним підвищенням версії, будь-якого сегмента, зокрема ревізії, і
ніколи сам по собі. Його записано в кожну збірку як `[assembly: AssemblyMetadata("VersionDate", ...)]`.

Версія з дефісом (`1.1.0-beta.1`) стає prerelease. Нова вбудована редакція завжди означає нову версію
пакета.

## Реліз

Реліз запускається однією кнопкою: **Actions → Release → Run workflow** (гілка `main`). Workflow
лежить у [.github/workflows/release.yml](https://github.com/UkrUtils/Katottg/blob/main/.github/workflows/release.yml).

1. У PR підвищте версію в `Source/Directory.Build.props` і злийте його в `main`.
2. Запустіть **Release**. Спершу можна прогнати з `dry-run`: тоді workflow лише збере, протестує й
   викладе пакети як артефакт, з будь-якої гілки.
3. Workflow відмовить, якщо тег `v<версія>` уже існує. Інакше він опублікує три пакети (`.nupkg` +
   `.snupkg`) на nuget.org і створить тег та GitHub Release з пакетами й автоматичними нотатками.
   Повторний запуск після часткового збою безпечний (`--skip-duplicate`).

Workflow також оновлює сайт: job `pages` викликає `pages.yml` після `publish`. Релізи, створені через
`GITHUB_TOKEN`, не запускають інших workflow, тож сторінка релізів оновлюється саме так.

API-ключі не зберігаються: `NuGet/login` обмінює OIDC-токен GitHub на короткоживучий ключ.

### Одноразове налаштування

- **nuget.org → Trusted Publishing** (меню акаунта): додайте політику з такими полями: Repository
  Owner `UkrUtils`, Repository `Katottg`, Workflow File `release.yml`, Environment `nuget`. Для першої
  публікації ID пакетів мають бути вільні або вже належати цьому акаунту.
- **GitHub → Settings → Environments**: створіть середовище `nuget`. За бажання обмежте його гілкою
  `main` і додайте обов’язкових рецензентів (тоді «одна кнопка» плюс схвалення).
- **GitHub → Settings → Secrets and variables → Actions → Variables**: змінна `NUGET_USER` — ім’я
  профілю на nuget.org, а не email.
- **GitHub → Settings → Pages → Source: GitHub Actions** (для сайту).
- **PayPal Donate button:** `PAYPAL_BUTTON_ID` у `site/.vitepress/site.config.js`. Якщо порожній,
  кнопку приховано, а збірка попереджає.

## Підводні камені

- `Data/katottg.bin.gz` вбудовано з `WithCulture="false"`. Без цього MSBuild сприймає `.bin` як
  культуру (мова біні) і кладе ресурс у сателітну збірку.
- Байти gzip залежать від версії zlib у рантаймі. Тест свіжості знімка тому порівнює розпаковані дані.
- Літери категорій в офіційному файлі латинські, але візуально збігаються з кирилицею. `FromLetter`
  приймає обидва варіанти.
- Не вирішуйте за маркером категорії в пошуку остаточно: у реальних адресах категорію часто вказують
  неточно (див. fallback у `KatottgDataset.Search.cs`).
- `editLink.pattern` у `site/.vitepress/config.mts` має лишатися самодостатньою функцією: VitePress
  серіалізує її в клієнтський бандл, де змінні рівня модуля не визначені.

## Git

Основна гілка — `main`; робота ведеться у feature-гілках, які зливаються через PR. Комітьте логічними
кроками, в описі пояснюйте «чому». Не комітьте `bin/`, `obj/` і результати BenchmarkDotNet.
