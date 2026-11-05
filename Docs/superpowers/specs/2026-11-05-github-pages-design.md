# GitHub Pages для UkrUtils.Katottg — дизайн

Дата: 2026-11-05. Гілка: `mvp/pages`.

## Мета

Публічний сайт `https://ukrutils.github.io/Katottg/` українською та англійською з такими розділами:

- короткий опис бібліотеки;
- документація;
- donate через PayPal;
- історія релізів з GitHub Releases.

Сайт оновлюється без ручних дій після кожного релізу й кожної зміни документації в `main`. Окремо
XML-doc публічного API перекладається англійською, щоб IntelliSense був зрозумілий і англомовним
користувачам.

Критерії успіху:

1. Сайт збирається в CI і розгортається з `main`.
2. Після запуску workflow **Release** історія релізів на сайті вже містить новий реліз.
3. Тексти документації ніде не дублюються: сайт вставляє файли з репо, а не тримає копії.
4. Ядро бібліотеки й пакети від сайту не залежать (Node лише в `site/`).
5. Збірка `dotnet build` не дає нових попереджень після перекладу XML-doc.

Поза межами: API-довідник (DocFX), власний домен, аналітика, коментарі, блог.

## Частина A. Сайт

### Інструмент

VitePress у теці `site/`, тобто `srcDir` = `site/`. Версія фіксується в `site/package.json`, і
`site/package-lock.json` комітиться. Версію Node зафіксовано в `site/.nvmrc` (LTS), CI бере її
звідти. Тема стандартна, з мінімальними власними компонентами.

### Структура

```
site/
  package.json, package-lock.json, .nvmrc
  .vitepress/
    config.mts            base '/Katottg/', locales (root = uk, en), nav, sidebar,
                          search: local (з перекладом інтерфейсу пошуку), editLink, socialLinks
    site.config.ts        константи: REPO, PACKAGES (3 ID), PAYPAL_BUTTON_ID
    releases.data.ts      data loader: завантаження релізів під час збірки
    releases.ts           чиста функція normalizeReleases(raw) → Release[]
    theme/
      index.ts            розширення стандартної теми, реєстрація компонентів
      Releases.vue        список релізів (props: lang)
      DonateButton.vue    кнопка-посилання PayPal (props: lang)
  index.md                головна (layout: home)
  guide.md                <!--@include: ../README.md-->
  agents.md               <!--@include: ../Docs/AgentGuide.md-->
  releases.md             <Releases lang="uk" />
  donate.md               текст + <DonateButton lang="uk" />
  en/index.md, en/guide.md (→ ../../Docs/en/README.md), en/agents.md (→ ../../Docs/en/AgentGuide.md),
  en/releases.md, en/donate.md
  tests/releases.test.ts  node:test для normalizeReleases
Docs/en/README.md         англійський переклад README.md
Docs/en/AgentGuide.md     англійський переклад Docs/AgentGuide.md
.github/workflows/pages.yml
```

### Сторінки

- **Головна** (`layout: home`):
  - hero: назва, однорядковий опис;
  - кнопки «Документація», «GitHub», «Підтримати»;
  - 6 карток переваг: вбудовані дані, без залежностей, AOT/trimming, пошук за адресою, ієрархія,
    CLI;
  - блок `dotnet add package UkrUtils.Katottg` і приклад із «Швидкого старту» README. Приклад
    короткий і копіюється в `index.md` свідомо: це вітрина, а не документація. Він збігається з
    прикладом у README, а той перевіряє `DocsExamplesTests`.
- **Документація** і **Для AI-агентів** лише вставляють файли через `@include`. Перший H1 файлу
  стає заголовком сторінки. Sidebar будується з H2.
- **Релізи:**
  - кожен реліз показує тег (заголовок), дату публікації (`Intl.DateTimeFormat` у локалі сторінки,
    `timeZone: 'UTC'`, без часу, тож результат однаковий при кожній збірці) і бейдж «prerelease»;
  - нотатки релізу перетворюються з markdown на HTML під час збірки рендерером VitePress
    (`createMarkdownRenderer`);
  - для кожного релізу є посилання на GitHub Release і на три пакети NuGet конкретної версії
    (`https://www.nuget.org/packages/<id>/<версія>`; версія = тег без префікса `v`);
  - якщо релізів немає, сторінка показує «Релізів ще немає» / "No releases yet";
  - на EN-сторінці є примітка, що нотатки генерує GitHub і вони можуть бути українською.
- **Підтримати:** короткий текст про те, на що йдуть кошти (підтримка даних в актуальному стані),
  і кнопка-посилання `https://www.paypal.com/donate/?hosted_button_id=<PAYPAL_BUTTON_ID>`
  (`target="_blank" rel="noopener"`). PayPal SDK не підключається: на сайті нуль сторонніх
  скриптів. «Підтримати» також є в nav.

### Релізи під час збірки

`releases.data.ts` під час `vitepress build` виконує:

1. `GET https://api.github.com/repos/UkrUtils/Katottg/releases?per_page=100` з заголовками
   `Accept: application/vnd.github+json`, `User-Agent: UkrUtils.Katottg-site` і
   `Authorization: Bearer $GITHUB_TOKEN`, якщо змінна задана.
2. `normalizeReleases(raw)`:
   - відкидає `draft`;
   - сортує за `published_at` спаданням, а при рівності за `tag_name`, щоб порядок був однозначним;
   - повертає `{ tag, version, name, publishedAt, prerelease, url, bodyMarkdown, packages[] }`.
3. Рендерить `bodyMarkdown` в HTML.

Помилки:

- у CI (`process.env.CI`) будь-яка помилка API (мережа, статус ≠ 200) **валить збірку**. Так ми не
  опублікуємо сайт з хибно порожньою історією;
- локально виводиться попередження, і список релізів порожній, щоб сайт можна було правити без
  мережі;
- більше 100 релізів поки не очікується (YAGNI). Якщо API повертає 100 записів, loader виводить
  попередження про потребу в пагінації.

### Мови

- `root` — українська (`lang: uk-UA`), `/en/` — англійська (`lang: en-US`).
- Перекладено: nav, sidebar, підписи теми (outline, prev/next, lastUpdated, darkMode), інтерфейс
  локального пошуку, тексти компонентів.
- `Docs/en/*` — повні переклади. **Правило для AGENTS.md:** змінюючи `README.md` або
  `Docs/AgentGuide.md`, оновлюйте `Docs/en/*` у тому самому PR. Приклади коду в обох мовах
  однакові.
- `Docs/en/*` не пакуються в NuGet.

### CI/CD — `.github/workflows/pages.yml`

Тригери:

- `push` у `main` за шляхами `site/**`, `README.md`, `Docs/**`, `.github/workflows/pages.yml`;
- `pull_request` за тими самими шляхами (лише build);
- `workflow_dispatch`;
- `workflow_call`.

Jobs:

- **build:**
  - `actions/checkout`, `actions/setup-node` (`node-version-file: site/.nvmrc`, кеш npm);
  - `npm ci`, `npm test`, `npm run build` у `site/` з `GITHUB_TOKEN`;
  - `actions/upload-pages-artifact` з `site/.vitepress/dist`.

  VitePress за замовчуванням валить збірку на битих посиланнях, і це наша перевірка цілісності
  include.
- **deploy:** лише не для `pull_request`. Має `environment: github-pages`, `permissions: pages:
  write, id-token: write` і використовує `actions/deploy-pages`.

Також `concurrency: group: pages, cancel-in-progress: false`.

**Чому окремий виклик з `release.yml`.** `release.yml` створює реліз через `GITHUB_TOKEN`, а події
від цього токена інші workflow не запускають. Тому `release.yml` отримує останній job:

```yaml
pages:
  needs: publish
  uses: ./.github/workflows/pages.yml
  permissions:
    contents: read
    pages: write
    id-token: write
```

Так сайт оновлюється одразу після створення релізу. Повторний запуск Release після часткового збою
так само безпечно перезбирає сайт.

### Тестування

- `npm test` (`node:test` + `--experimental-strip-types` або tsx) для `normalizeReleases`:
  - чернетки відкидаються;
  - сортування, зокрема однакові дати;
  - prerelease;
  - `version` з тегу;
  - посилання на NuGet;
  - порожній вхід.
- `npm run build` — VitePress-збірка з перевіркою битих посилань.
- Ручна перевірка: `npm run preview`, обидві мови, пошук, сторінка релізів (поки порожня), кнопка
  PayPal веде на правильний URL.

### Що робить власник репозиторію (одноразово)

1. **Settings → Pages → Build and deployment → Source: GitHub Actions.**
2. Створити PayPal Donate button і передати `hosted_button_id`. Поки `PAYPAL_BUTTON_ID` порожній,
   кнопка не рендериться: сторінка «Підтримати» показує «скоро буде», а збірка виводить
   попередження. Сайт можна запустити, не чекаючи PayPal, і битої кнопки не буде.
3. За бажання вказати `https://ukrutils.github.io/Katottg/` у полі Website репозиторію.

## Частина B. XML-doc англійською

- Усі `///` коментарі в `Source/**.cs` (близько 420 рядків у 20 файлах) перекладаються англійською.
  Зміст і теги (`<see cref>`, `<paramref>`, `<example>`) зберігаються.
- **Не змінюються:** звичайні коментарі `//`, повідомлення винятків, вивід CLI, тести. Звичайні
  коментарі адресовані тим, хто супроводжує код, і лишаються українською. Повідомлення винятків і
  вивід CLI — окреме рішення про поведінку, тут їх не чіпаємо.
- **AGENTS.md, «Правила коду»:** «XML-doc публічного API — англійською; звичайні коментарі й
  документація (README, Docs) — українською, з англійським перекладом у `Docs/en/`».
- Перевірка: `dotnet build` без нових попереджень (CS1591, некоректні `cref`), `dotnet test`
  зелені.

Частина B незалежна від частини A: інші файли, і результати одна одної вони не потребують.

## Оновлення документації репо

- **AGENTS.md:**
  - розділ «Команди»: `npm ci`, `npm test`, `npm run dev`, `npm run build` у `site/`;
  - структура: `site/`, `Docs/en/`;
  - правило синхронізації `Docs/en/*`;
  - нове правило мови XML-doc;
  - розділ «Реліз»: сайт оновлюється сам; одноразове налаштування Pages.
- **README.md і Docs/en/README.md:** посилання на сайт угорі.
- **.gitignore:** `site/node_modules/`, `site/.vitepress/dist/`, `site/.vitepress/cache/`.
