# AGENTS.md — робота з репозиторієм UkrUtils.Katottg

Інструкції для AI-агентів (і людей), які **змінюють** бібліотеку. Як нею користуватися, описано в
[Docs/AgentGuide.md](Docs/AgentGuide.md) і [README.md](README.md). План і контекст предметної
області — у [Docs/ImplementationPlan.md](Docs/ImplementationPlan.md); це головне джерело вимог.

## Команди

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

Сайт (VitePress, Node 24 з `site/.nvmrc`): тести й збірка, як у CI.

```bash
cd site && npm ci && npm test && npm run build
```

Локальний перегляд сайту з перезавантаженням:

```bash
cd site && npm run dev
```

Сторінка релізів будується з GitHub Releases API під час збірки. У CI (змінна `CI`) помилка API
валить збірку, локально — лише попередження й порожній список; токен береться з `GITHUB_TOKEN`.

SDK зафіксовано в `global.json` (.NET 10, `rollForward: latestFeature`). Повний прогін тестів триває
кілька секунд і використовує повний офіційний файл з `OfficialData/`.

## Структура

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
Docs/                               план, посібник для агентів
Docs/en/                            англійські переклади README та посібника для агентів
site/                               сайт GitHub Pages (VitePress, UA + EN); вставляє README.md,
                                    Docs/AgentGuide.md і Docs/en/*, сторінка релізів — з GitHub API
```

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
4. Підвищте версію в `Source/Directory.Build.props` разом із `VersionDate`, оновіть дату редакції в README.

Знімок не редагуйте вручну. Формат описано в `Storage/SnapshotFormat.cs`. Якщо формат змінюється,
підвищуйте `SnapshotFormat.Version`.

## Реліз

Реліз запускається однією кнопкою: **Actions → Release → Run workflow** (гілка `main`).
Workflow лежить у [.github/workflows/release.yml](.github/workflows/release.yml).

Workflow також оновлює сайт: job `pages` викликає `pages.yml` після `publish` (релізи, створені
через `GITHUB_TOKEN`, не запускають інших workflow, тож сторінка релізів оновлюється саме так).

1. У PR підвищте `PackageVersion` (а також `FileVersion`, `AssemblyVersion`,
   `InformationalVersion`, `VersionDate`) у `Source/Directory.Build.props` і злийте його в `main`.
   Версія з дефісом (`1.1.0-beta.1`) стає prerelease.
2. Запустіть **Release**. Спершу можна прогнати з `dry-run`: тоді workflow лише збере, протестує й
   викладе пакети як артефакт, з будь-якої гілки.
3. Workflow відмовить, якщо тег `v<версія>` уже існує. Інакше він опублікує три пакети (`.nupkg` +
   `.snupkg`) на nuget.org і створить тег та GitHub Release з пакетами й автоматичними нотатками.
   Повторний запуск після часткового збою безпечний (`--skip-duplicate`).

Одноразове налаштування:

- **nuget.org → Trusted Publishing** (меню акаунта): додайте політику з такими полями:
  Repository Owner `UkrUtils`, Repository `Katottg`, Workflow File `release.yml`, Environment `nuget`.
  Для першої публікації ID пакетів мають бути вільні або вже належати цьому акаунту.
- **GitHub → Settings → Environments**: створіть середовище `nuget`. За бажання обмежте його гілкою
  `main` і додайте обов’язкових рецензентів (тоді «одна кнопка» плюс схвалення).
- **GitHub → Settings → Secrets and variables → Actions → Variables**: змінна `NUGET_USER` — ім’я
  профілю на nuget.org, а не email.

- **GitHub → Settings → Pages → Source: GitHub Actions** (для сайту).
- **PayPal Donate button:** `PAYPAL_BUTTON_ID` у `site/.vitepress/site.config.js`. Якщо порожній,
  кнопку приховано, а збірка попереджає.

API-ключі не зберігаються: `NuGet/login` обмінює OIDC-токен GitHub на короткоживучий ключ.

## Правила коду

- **Ядро — без залежностей і без рефлексії** (`IsAotCompatible=true`), без `System.Text.Json`.
  Залежності допустимі лише в окремих пакетах (`Import`, майбутній DI-пакет).
- **Дані незмінні.** `KatottgDataset` і `KatottgUnit` після побудови не змінюються. Колекції
  віддаються як `ReadOnlyCollection` (`AsReadOnly()`), а не як масиви чи `List<T>`.
- **Документація й коментарі — українською**, ідентифікатори — англійською. Кожен публічний член має
  XML-doc; `GenerateDocumentationFile` увімкнено, тож пропущена документація видно як попередження CS1591.
- **TDD:** спершу тест, переконайтеся, що він падає, потім реалізація. Перевіряйте на реальних даних:
  `FixtureDataset.Official` (повний файл) або `FixtureDataset.Value` (44 рядки).
- Змінюючи `README.md` або `Docs/AgentGuide.md`, оновлюйте `Docs/en/*` у тому самому PR; сайт
  вставляє ці файли, копій немає.
- Приклади з README і `Docs/AgentGuide.md` дублюються в `DocsExamplesTests.cs`. Змінюючи API,
  оновлюйте всі три місця.
- Нетипові місця даних (Київ/Севастополь `K` без району й громади, Прип’ять/Чорнобиль `M` під
  областю, `B` під `M`) — постійне джерело помилок. Будь-яку логіку ієрархії перевіряйте тестом на
  повному наборі.
- Детермінізм: пошук і знімок мають давати однаковий результат при кожному запуску. Порядок завжди
  доводьте до однозначного (останній критерій — код).

## Підводні камені

- `Data/katottg.bin.gz` вбудовано з `WithCulture="false"`. Без цього MSBuild сприймає `.bin` як
  культуру (мова біні) і кладе ресурс у сателітну збірку.
- Байти gzip залежать від версії zlib у рантаймі. Тест свіжості знімка тому порівнює розпаковані дані.
- Літери категорій в офіційному файлі латинські, але візуально збігаються з кирилицею. `FromLetter`
  приймає обидва варіанти.
- Не вирішуйте за маркером категорії в пошуку остаточно: у реальних адресах категорію часто вказують
  неточно (див. fallback у `KatottgDataset.Search.cs`).
- У markdown, який вставляє сайт, сирі `<…>` і `{{` поза кодом ламають збірку VitePress — беріть їх
  у бектики.
- `editLink.pattern` у `site/.vitepress/config.mts` має лишатися самодостатньою функцією: VitePress
  серіалізує її в клієнтський бандл, тож змінні рівня модуля там не визначені.

## Git

Робоча гілка для MVP — `dev/mvp`, основна — `main`. Комітьте логічними кроками, в описі пояснюйте
«чому». Не комітьте `bin/`, `obj/`, результати BenchmarkDotNet.
