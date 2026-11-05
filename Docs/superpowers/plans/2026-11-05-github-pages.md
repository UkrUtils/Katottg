# GitHub Pages для UkrUtils.Katottg — план реалізації

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Мета:** розгорнути сайт `https://ukrutils.github.io/Katottg/` (UA+EN). На ньому: опис, документація,
вставлена з README та AgentGuide, історія релізів і кнопка donate через PayPal. Окремо XML-doc
публічного API перекладається англійською.

**Архітектура:** VitePress у `site/` (`srcDir`), на бібліотеку він не впливає. Чиста логіка (релізи,
donate) лежить у `site/.vitepress/lib/*.js` і має тести на `node:test`. Data loader бере релізи
з GitHub API під час збірки. `pages.yml` збирає й розгортає сайт; `release.yml` викликає його після
публікації.

**Стек:** Node 24 (LTS), VitePress 1.6.x (`latest`; 2.x — alpha, не беремо), `node:test`,
GitHub Actions (`actions/configure-pages`, `upload-pages-artifact`, `deploy-pages`).

**Spec:** [Docs/superpowers/specs/2026-11-05-github-pages-design.md](../specs/2026-11-05-github-pages-design.md)

## Глобальні обмеження

- `base: '/Katottg/'`. Locales: `root` = `uk-UA`, `en` = `en-US`.
- Node лише в `site/`. `site/package-lock.json` комітиться, `site/.nvmrc` = `24`, `"type": "module"`.
- Логіка в `lib/` написана на JS з JSDoc, а не на TS. Так `node --test` запускає її без
  завантажувачів. Це свідоме відхилення від `releases.ts` у специфікації.
- Тексти документації не копіюються: `@include` з `README.md`, `Docs/AgentGuide.md`, `Docs/en/*`.
- Сторонніх скриптів немає, PayPal SDK не підключається. URL кнопки:
  `https://www.paypal.com/donate/?hosted_button_id=<ID>`.
- API релізів: `GET https://api.github.com/repos/UkrUtils/Katottg/releases?per_page=100`;
  заголовки `Accept: application/vnd.github+json`, `User-Agent: UkrUtils.Katottg-site`,
  `Authorization: Bearer $GITHUB_TOKEN`, якщо змінна задана.
- Пакети: `UkrUtils.Katottg`, `UkrUtils.Katottg.Import`, `UkrUtils.Katottg.Tool`. URL пакета:
  `https://www.nuget.org/packages/<id>/<версія>`.
- Детермінізм: релізи сортуються за `published_at` спаданням, а при рівності за `tag_name`
  ординально спаданням. Дати форматуються в `timeZone: 'UTC'`.
- Комітимо в гілку `mvp/pages` логічними кроками, а в описі пояснюємо «чому».
- Документація й коментарі сайту українською, крім англійських сторінок. XML-doc у `Source/` після
  частини B англійською.

## Фокус рев’ю

1. Сирий `<слово>` або `{{` поза кодом у README/AgentGuide/`Docs/en/*` — це помилка компіляції Vue.
   Збірка мусить пройти, а виправлення в джерелі — лише обгортанням у бектики (Task 2, 3).
2. Внутрішні посилання мають отримати префікс `/Katottg/`. Сирий `<a href="/...">` у markdown чи Vue
   префікс втрачає (Task 1, перевірка `grep` у dist).
3. Реліз з `body: null` або `name: null` не повинен ламати збірку: тіло порожнє, назва дорівнює тегу
   (Task 4, тести).
4. Локально без токена API може повернути 403 (rate limit). Тоді має бути попередження і порожній
   список, а не падіння. На CI той самий збій валить збірку (Task 4, тести).
5. Реліз о 23:30 UTC не повинен «переїхати» на наступну дату через часовий пояс машини (Task 4,
   тест).

---

### Task 1: Каркас VitePress, конфіг і головні сторінки

**Files:**
- Create:
  - `site/package.json`, `site/package-lock.json`, `site/.nvmrc`;
  - `site/.vitepress/config.mts`;
  - `site/.vitepress/site.config.js`;
  - `site/.vitepress/theme/index.js`;
  - `site/index.md`, `site/en/index.md`.
- Modify: `.gitignore` (кінець файлу).

**Interfaces:**
- Produces (`site.config.js`):
  - `export const REPO = 'UkrUtils/Katottg'`;
  - `export const PACKAGE_IDS = ['UkrUtils.Katottg', 'UkrUtils.Katottg.Import', 'UkrUtils.Katottg.Tool']`;
  - `export const PAYPAL_BUTTON_ID = ''`.
- Produces (`theme/index.js`): `export default { extends: DefaultTheme, enhanceApp({ app }) {} }`.
  Наступні задачі реєструють тут компоненти `Releases` і `DonateButton`.
- Produces: npm-скрипти `dev`, `build`, `preview`, `test` (`node --test "tests/*.test.js"`).

- [ ] **Step 1:** `cd site && npm init -y`. Задайте `"private": true` і `"type": "module"`,
  виконайте `npm install -D vitepress@^1.6.4` і додайте скрипти з Interfaces. `.nvmrc` = `24`.
- [ ] **Step 2:** Допишіть у `.gitignore` секцію `# VitePress site` з рядками
  `site/.vitepress/dist/` і `site/.vitepress/cache/`. `node_modules/` уже ігнорується.
- [ ] **Step 3:** `config.mts`:
  - `srcDir` за замовчуванням (тека `site/`), `base: '/Katottg/'`, `cleanUrls: true`;
  - `lastUpdated` не вмикаємо: для сторінок з `@include` git-дата файлу-заглушки вводила б в
    оману;
  - `head`: favicon не потрібен;
  - `themeConfig.socialLinks`: GitHub `https://github.com/UkrUtils/Katottg`;
  - `search: { provider: 'local', options: { locales: { root: { translations: … } } } }`
    з українськими підписами кнопки й модалки;
  - `locales.root` (`label: 'Українська'`, `lang: 'uk-UA'`) і `locales.en` (`label: 'English'`,
    `lang: 'en-US'`, `link: '/en/'`). Кожна локаль має свої `title: 'UkrUtils.Katottg'`,
    `description`, `themeConfig.nav` (Документація/Docs → `/guide`, Для AI-агентів/For AI agents →
    `/agents`, Релізи/Releases → `/releases`, Підтримати/Donate → `/donate`; для EN усі з префіксом
    `/en`), однаковий для всіх сторінок `sidebar` з тими самими чотирма пунктами, `editLink`,
    `footer` (`Код — MIT. Дані —
    офіційний КАТОТТГ Мінрегіону.` / `Code — MIT. Data — the official KATOTTG of the Ministry.`);
  - `editLink.pattern` — функція `({ filePath }) => …`. Мапа
    `{ 'guide.md': 'README.md', 'agents.md': 'Docs/AgentGuide.md', 'en/guide.md': 'Docs/en/README.md', 'en/agents.md': 'Docs/en/AgentGuide.md' }`
    дає `https://github.com/UkrUtils/Katottg/edit/main/<файл>`, а всі інші сторінки —
    `…/edit/main/site/<filePath>`. Так «Редагувати» веде до справжнього джерела, а не до заглушки
    з `@include`;
  - для UA також підписи `outline`, `docFooter`, `editLink.text`, `darkModeSwitchLabel`,
    `returnToTopLabel`, `sidebarMenuLabel`, `langMenuLabel`.

  Сторінок `guide`/`agents`/`releases`/`donate` ще немає, тому, щоб збірка пройшла, тимчасово
  задайте `ignoreDeadLinks: [/^\/(en\/)?(guide|agents|releases|donate)$/]`. Task 5 приберe це.
- [ ] **Step 4:** `index.md` / `en/index.md` з `layout: home`:
  - `hero`: `name: UkrUtils.Katottg`; `text` — «Кодифікатор КАТОТТГ для .NET» /
    "The KATOTTG codifier for .NET"; `tagline` — перше речення README або його переклад;
  - `actions`: Документація (brand) → `/guide`, GitHub (alt) → repo, Підтримати (alt) → `/donate`;
  - `features`: 6 карток (вбудовані дані ~360 КБ і 31 751 запис; без залежностей; AOT/trimming;
    пошук за назвою й адресою; ієрархія область → район у місті; CLI `katottg`);
  - під frontmatter: блок `dotnet add package UkrUtils.Katottg` і C#-приклад зі «Швидкого старту»
    README. Скопіюйте дослівно, з коментарями мовою сторінки.

  Усі посилання задаються лише полями frontmatter або markdown-посиланнями, без сирих `<a href>`.
- [ ] **Step 5:** Запустіть `cd site && npm run build`. Очікування: `build complete`, є
  `site/.vitepress/dist/index.html` і `dist/en/index.html`.
- [ ] **Step 6:** `grep -o 'href="/[^"]*"' site/.vitepress/dist/index.html | grep -v '^href="/Katottg/'`.
  Очікуваний вивід порожній.
- [ ] **Step 7:** Закомітьте `site/` без `node_modules`/`dist`/`cache` разом із `.gitignore`:
  `Add VitePress site skeleton with uk/en home pages`.

### Task 2: Сторінки документації (UA)

**Files:**
- Create: `site/guide.md`, `site/agents.md`.
- Modify, лише якщо збірка впаде на синтаксисі Vue: `README.md`, `Docs/AgentGuide.md`.

- [ ] **Step 1:** `guide.md` = `<!--@include: ../README.md-->`, `agents.md` =
  `<!--@include: ../Docs/AgentGuide.md-->`. Frontmatter: `outline: [2, 3]`.
- [ ] **Step 2:** Запустіть `npm run build`. Якщо впаде на `<…>` чи `{{` поза кодом, обгорніть
  фрагмент у бектики в джерелі й перезапустіть `dotnet test Tests/UkrUtils.Katottg.Tests`
  (`DocsExamplesTests` мусить лишитися зеленим). Інших правок README не робіть.
- [ ] **Step 3:** У `dist/guide.html` є `Швидкий старт` і `KatottgTreeNode`, у `dist/agents.html` —
  `Карта API` (перевірте через `grep -c`).
- [ ] **Step 4:** Закомітьте: `Add UA docs pages included from README and AgentGuide`.

### Task 3: Англійський переклад документації

**Files:**
- Create: `Docs/en/README.md`, `Docs/en/AgentGuide.md`, `site/en/guide.md`, `site/en/agents.md`.

- [ ] **Step 1:** `Docs/en/README.md` — повний переклад `README.md`:
  - та сама структура розділів;
  - код і `// вивід` — без змін, бо в прикладах і виводі українські назви: це дані, а не текст;
  - коментарі в коді перекладіть;
  - усі посилання абсолютні (`https://github.com/UkrUtils/Katottg/blob/main/...`), а посилання на
    AgentGuide веде на `Docs/en/AgentGuide.md`;
  - на початку, під H1, рядок: `> Українська версія: [README.md](https://github.com/UkrUtils/Katottg/blob/main/README.md)`.
- [ ] **Step 2:** `Docs/en/AgentGuide.md` — повний переклад за тими самими правилами. Блок
  «додайте у свій AGENTS.md» перекладіть англійською, а посилання в ньому ведуть на EN-посібник.
- [ ] **Step 3:** `site/en/guide.md` = `<!--@include: ../../Docs/en/README.md-->`, `site/en/agents.md`
  = `<!--@include: ../../Docs/en/AgentGuide.md-->`, `outline: [2, 3]`.
- [ ] **Step 4:** Запустіть `npm run build`. Перевірка: є `dist/en/guide.html` з `Quick start` і
  `dist/en/agents.html` з `API map`.
- [ ] **Step 5:** Переконайтеся, що `dotnet pack` не бере `Docs/en/*`: у
  `Source/Directory.Build.props` пакується лише `Docs\AgentGuide.md`, тож нічого міняти не треба.
- [ ] **Step 6:** Закомітьте: `Add English translation of README and AgentGuide for the site`.

### Task 4: Історія релізів

**Files:**
- Create:
  - `site/.vitepress/lib/releases.js`;
  - `site/tests/releases.test.js`;
  - `site/.vitepress/theme/releases.data.js`;
  - `site/.vitepress/theme/Releases.vue`;
  - `site/releases.md`, `site/en/releases.md`.
- Modify: `site/.vitepress/theme/index.js` (реєстрація `Releases`).

**Interfaces:**
- Consumes: `REPO`, `PACKAGE_IDS` з `site.config.js`.
- Produces (`lib/releases.js`):
  - `/** @typedef {{ tag: string, version: string, name: string, publishedAt: string, prerelease: boolean, url: string, bodyMarkdown: string, packages: { id: string, url: string }[] }} Release */`;
  - `normalizeReleases(raw: object[], packageIds: string[]): Release[]`;
  - `formatReleaseDate(iso: string, lang: 'uk' | 'en'): string`;
  - `loadReleases({ fetchImpl, env, warn, repo, packageIds }): Promise<Release[]>`. Тут `env` —
    об’єкт на кшталт `process.env`, `warn(msg: string)`, `fetchImpl` — сумісний з `fetch`.
- Produces (data loader): `data: (Release & { bodyHtml: string })[]`.
- Produces (компонент): `<Releases lang="uk" />` / `<Releases lang="en" />`.

- [ ] **Step 1: падаючі тести** у `site/tests/releases.test.js` (`node:test`, `node:assert/strict`):
  - `drops drafts` — з `[{draft:true,…},{draft:false,…}]` лишається 1;
  - `sorts by published_at desc then tag desc`: `v1.0.0@2026-01-01`, `v1.1.0@2026-02-01` і
    `v1.0.1@2026-02-01` дають `['v1.1.0','v1.0.1','v1.0.0']`;
  - `version strips leading v`: `v1.0.1` → `1.0.1`, `1.2.0` → `1.2.0`;
  - `name falls back to tag`, `null body becomes empty string`;
  - `prerelease flag passes through`;
  - `builds nuget links`: `packages[0]` дорівнює
    `{ id: 'UkrUtils.Katottg', url: 'https://www.nuget.org/packages/UkrUtils.Katottg/1.0.1' }`,
    довжина — 3;
  - `url is html_url`;
  - `empty input gives empty list`;
  - `formats date in UTC`: `formatReleaseDate('2026-11-05T23:30:00Z','uk') === '05.11.2026'`,
    `('…','en') === 'Nov 5, 2026'`;
  - `loadReleases sends token and headers`: фейковий `fetchImpl` фіксує URL
    `https://api.github.com/repos/UkrUtils/Katottg/releases?per_page=100`, `Authorization:
    'Bearer t'` за `env.GITHUB_TOKEN='t'` і відсутність `Authorization` без токена;
  - `loadReleases throws on CI failure`: статус 500 з `env.CI='true'` дає `rejects`;
  - `loadReleases warns locally on failure`: статус 403 без `CI` повертає `[]`, `warn` викликано
    один раз; мережевий виняток поводиться так само;
  - `loadReleases warns at 100 releases`: 100 записів дають `warn` зі словом `pagination`.
- [ ] **Step 2:** Запустіть `npm test`. Очікування: FAIL (модуля немає).
- [ ] **Step 3:** Реалізуйте `lib/releases.js` за Interfaces і Глобальними обмеженнями.
  - Формат дати: `uk-UA` `{ day:'2-digit', month:'2-digit', year:'numeric', timeZone:'UTC' }`,
    `en-US` `{ month:'short', day:'numeric', year:'numeric', timeZone:'UTC' }`.
  - Помилка — це статус ≠ 200 або виняток `fetchImpl`. Текст помилки містить статус.
- [ ] **Step 4:** Запустіть `npm test`. Очікування: усі PASS.
- [ ] **Step 5:** `theme/releases.data.js`: `export default { async load() { … } }`.
  - Викликає `loadReleases` з `fetch`, `process.env`, `console.warn`, константами.
  - Для кожного релізу додає `bodyHtml = md.render(bodyMarkdown)`. Тут
    `md = await createMarkdownRenderer(config.srcDir, config.markdown, config.site.base, config.logger)`,
    де `config = globalThis.VITEPRESS_CONFIG`.
- [ ] **Step 6:** `Releases.vue` (`props: { lang: String }`):
  - `import { data } from './releases.data.js'`;
  - кожен реліз виводиться так: `<h2>` з назвою та id = тег; дата з `formatReleaseDate`; бейдж
    `<Badge type="warning">` із текстом prerelease; `<div class="vp-doc" v-html="bodyHtml">`;
    рядок посилань: GitHub Release і три пакети NuGet;
  - порожній стан: «Релізів ще немає.» / "No releases yet.";
  - тексти — у словнику `{ uk: {…}, en: {…} }` всередині компонента.

  Зареєструйте компонент у `theme/index.js`.
- [ ] **Step 7:** `releases.md`: `# Релізи`, один абзац (джерело — GitHub Releases; кожен реліз
  публікує три пакети на NuGet) і `<Releases lang="uk" />`. `en/releases.md`: те саме англійською й
  примітка `Release notes are generated by GitHub and may be in Ukrainian.`
- [ ] **Step 8:** Запустіть `npm run build`. Локально без мережі чи токена допустиме попередження.
  У `dist/releases.html` є «Релізів ще немає» (поки релізів немає) або тег, якщо реліз уже з’явився.
  Потім `CI=true npm run build` з мережею: збірка проходить, бо API відповідає 200 з `[]`.
- [ ] **Step 9:** Закомітьте: `Add build-time GitHub Releases history page`.

### Task 5: Сторінка «Підтримати»

**Files:**
- Create:
  - `site/.vitepress/lib/donate.js`;
  - `site/tests/donate.test.js`;
  - `site/.vitepress/theme/DonateButton.vue`;
  - `site/donate.md`, `site/en/donate.md`.
- Modify: `site/.vitepress/theme/index.js`, `site/.vitepress/config.mts` (попередження й прибрати
  тимчасовий `ignoreDeadLinks`).

**Interfaces:**
- Consumes: `PAYPAL_BUTTON_ID` з `site.config.js`.
- Produces: `donateUrl(buttonId: string): string | null`.

- [ ] **Step 1: падаючі тести** `donate.test.js`:
  - `donateUrl('')` і `donateUrl('  ')` повертають `null`;
  - `donateUrl('ABC123')` повертає `'https://www.paypal.com/donate/?hosted_button_id=ABC123'`;
  - `donateUrl('a b&c')` кодує значення через `encodeURIComponent`.
- [ ] **Step 2:** Запустіть `npm test`. Очікування: FAIL.
- [ ] **Step 3:** Реалізуйте `donateUrl` (trim, null для порожнього рядка).
- [ ] **Step 4:** Запустіть `npm test`. Очікування: PASS.
- [ ] **Step 5:** `DonateButton.vue` (`props: { lang }`):
  - якщо URL є, виводить `<a class="VPButton medium brand" :href target="_blank" rel="noopener">`
    з текстом «Підтримати через PayPal» / "Donate via PayPal";
  - якщо немає, виводить «Кнопка донату скоро з’явиться.» / "The donate button is coming soon.".

  Зареєструйте компонент у `theme/index.js`.
- [ ] **Step 6:** `donate.md` / `en/donate.md`: `# Підтримати проєкт` / `# Support the project`, 2–3
  речення (кошти йдуть на супровід: оновлення даних з кожною редакцією кодифікатора, нові функції)
  і `<DonateButton lang="…" />`.
- [ ] **Step 7:** `config.mts`:
  - якщо `donateUrl(PAYPAL_BUTTON_ID)` повертає `null`, виведіть
    `console.warn('[site] PAYPAL_BUTTON_ID не задано — кнопку донату приховано')`;
  - видаліть тимчасовий `ignoreDeadLinks`.
- [ ] **Step 8:** Запустіть `npm run build`. Очікування: попередження про PayPal, жодного dead link,
  є `dist/donate.html` і `dist/en/donate.html` з текстом «скоро».
- [ ] **Step 9:** Закомітьте: `Add donate page with PayPal Donate button link`.

### Task 6: Розгортання — `pages.yml` і виклик з `release.yml`

**Files:**
- Create: `.github/workflows/pages.yml`.
- Modify: `.github/workflows/release.yml` (новий job у кінці).

- [ ] **Step 1:** `pages.yml`:
  - `name: Pages`;
  - `on`:
    - `push` (`branches: [main]`, `paths: ['site/**', 'README.md', 'Docs/**', '.github/workflows/pages.yml']`);
    - `pull_request` (ті самі `paths`);
    - `workflow_dispatch`;
    - `workflow_call`;
  - `permissions: contents: read`;
  - `concurrency: { group: pages, cancel-in-progress: false }`;
  - job `build`: `runs-on: ubuntu-latest`, `defaults.run.working-directory: site`, кроки:
    - `actions/checkout@v4`;
    - `actions/setup-node@v4` (`node-version-file: site/.nvmrc`, `cache: npm`,
      `cache-dependency-path: site/package-lock.json`);
    - `npm ci`;
    - `npm test`;
    - `npm run build` з `env: GITHUB_TOKEN: ${{ github.token }}`;
    - `actions/configure-pages@v5`, але `if: github.event_name != 'pull_request'`;
    - `actions/upload-pages-artifact@v3` (`path: site/.vitepress/dist`);
  - job `deploy`:
    - `needs: build`, `if: github.event_name != 'pull_request'`;
    - `environment: { name: github-pages, url: ${{ steps.deployment.outputs.page_url }} }`;
    - `permissions: { pages: write, id-token: write }`;
    - крок `actions/deploy-pages@v4` з `id: deployment`.

  Угорі файлу додайте коментар українською. Він пояснює, навіщо `workflow_call`: релізи від
  `GITHUB_TOKEN` не запускають інші workflow.
- [ ] **Step 2:** У `release.yml` додайте job:
  ```yaml
  pages:
    needs: publish
    uses: ./.github/workflows/pages.yml
    permissions:
      contents: read
      pages: write
      id-token: write
  ```
  Над ним коментар: сайт оновлюється після кожного релізу; при `dry-run` job пропускається разом з
  `publish`.
- [ ] **Step 3:** Перевірте YAML: `npx --yes @action-validator/cli .github/workflows/pages.yml` і
  те саме для `release.yml`. Очікування: без помилок. Якщо валідатор недоступний, підійде
  `python -c "import yaml,sys;[yaml.safe_load(open(f)) for f in sys.argv[1:]]" <файли>`.
- [ ] **Step 4:** Закомітьте: `Deploy site via Pages workflow, also after each release`.

### Task 7: Документація репозиторію

**Files:**
- Modify: `AGENTS.md`, `README.md`, `Docs/en/README.md`, `llms.txt`.

- [ ] **Step 1:** Зміни в `AGENTS.md`:
  - «Команди»: блок `cd site && npm ci && npm test && npm run build` і окремо `npm run dev`;
  - «Структура»: рядки `site/` (VitePress, UA+EN) і `Docs/en/` (англійські переклади);
  - «Правила коду»: «Змінюючи `README.md` або `Docs/AgentGuide.md`, оновлюйте `Docs/en/*` у тому
    самому PR; сайт вставляє ці файли, копій немає»;
  - «Реліз»: workflow також оновлює сайт (job `pages`);
  - «Одноразове налаштування»: «GitHub → Settings → Pages → Source: GitHub Actions» і
    «PayPal Donate button: `PAYPAL_BUTTON_ID` у `site/.vitepress/site.config.js`»;
  - «Підводні камені»: «у markdown, який вставляє сайт, сирі `<…>` і `{{` поза кодом ламають
    збірку VitePress — беріть їх у бектики».
- [ ] **Step 2:** У `README.md` під першим абзацом додайте рядок `Сайт і документація:
  https://ukrutils.github.io/Katottg/`. У `Docs/en/README.md` той самий рядок англійською.
- [ ] **Step 3:** У `llms.txt` замініть `Docs are in Ukrainian` на `Docs are in Ukrainian with
  English translations` і додайте до `## Docs` пункти `Site` і `English README` / `English agent
  guide` з абсолютними URL.
- [ ] **Step 4:** Запустіть `cd site && npm run build` (include і далі цілі) та
  `dotnet test Tests/UkrUtils.Katottg.Tests`. Очікування: обидва зелені.
- [ ] **Step 5:** Закомітьте: `Document the site, English docs sync rule and Pages setup`.

### Task 8: XML-doc англійською — ядро

**Files:**
- Modify: усі `///` у `Source/UkrUtils.Katottg/**/*.cs`, тобто 15 файлів:
  - `KatottgAddress`, `KatottgCategory`, `KatottgCode`;
  - `KatottgDataset` (+ `.Search`, `.Storage`);
  - `KatottgEdition`, `KatottgRecord`, `KatottgTreeNode`, `KatottgUnit`;
  - `Search/{KatottgNormalizer,NameIndex,QueryPart,SearchTypes}`;
  - `Storage/SnapshotFormat`.

- [ ] **Step 1:** Запустіть `dotnet build Source/UkrUtils.Katottg.slnx -c Release 2>&1 | grep -c "warning"`
  і запишіть базову кількість попереджень.
- [ ] **Step 2:** Перекладіть `///` англійською.
  - Не змінюються: теги, `cref`, `paramref`, приклади коду всередині `<code>`/`<example>` (лише
    коментарі в них), назви категорій і приклади даних українською (`с. Андріївка`, `обл.`).
  - Звичайні `//`, рядки й повідомлення винятків не чіпайте.
  - Ключові правила для агентів, продубльовані в `KatottgDataset`/`KatottgCode` (див. README,
    «AI-агенти у вашому проєкті»), мають зберегти зміст.
- [ ] **Step 3:** Знову запустіть `dotnet build`. Кількість попереджень не більша за базову.
  Перевірте також `grep -rn '///.*[А-Яа-яІіЇїЄєҐґ]' Source/UkrUtils.Katottg`: лишаються тільки
  рядки з українськими прикладами даних.
- [ ] **Step 4:** `dotnet test Tests/UkrUtils.Katottg.Tests` — зелено.
- [ ] **Step 5:** Закомітьте: `Translate core public XML-doc to English for IntelliSense`.

### Task 9: XML-doc англійською — Import і Tool, правило в AGENTS.md

**Files:**
- Modify:
  - `///` у `Source/UkrUtils.Katottg.Import/{ImportReport,KatottgImportResult}.cs`;
  - `Source/UkrUtils.Katottg.Import/Xlsx/{KatottgSheetParser,MinimalXlsxReader}.cs`;
  - `Source/UkrUtils.Katottg.Tool/KatottgCli.cs`;
  - `AGENTS.md` («Правила коду», пункт про мову).

- [ ] **Step 1:** Перекладіть `///` за правилами Task 8, Step 2. Вивід CLI українською не чіпайте.
- [ ] **Step 2:** У `AGENTS.md` замініть пункт «Документація й коментарі — українською…» на такий:
  «**Мова.** XML-doc публічного API — англійською (IntelliSense). Звичайні коментарі, README і
  `Docs/` — українською, з англійським перекладом README та AgentGuide у `Docs/en/`.
  Ідентифікатори — англійською. Кожен публічний член має XML-doc; `GenerateDocumentationFile`
  увімкнено, тож пропущена документація видно як попередження CS1591.»
- [ ] **Step 3:** Запустіть `dotnet build` (попереджень не більше за базову кількість) і
  `dotnet test Tests/UkrUtils.Katottg.Tests` (зелено).
- [ ] **Step 4:** Закомітьте: `Translate Import/Tool XML-doc to English; update language rule`.

### Task 10: Фінальна перевірка

- [ ] **Step 1:** `cd site && npm ci && npm test && CI=true npm run build`. Очікування: усе зелено,
  а з ненульовим токеном чи мережею й API без помилок.
- [ ] **Step 2:** `dotnet build Source/UkrUtils.Katottg.slnx` і `dotnet test Tests/UkrUtils.Katottg.Tests`.
  Очікування: зелено.
- [ ] **Step 3:** Запустіть `npm run preview` і в браузері перевірте `/Katottg/`:
  - обидві мови й перемикач мов;
  - пошук (UA-запит «Андріївка» знаходить сторінку документації);
  - сторінки релізів (порожній стан) і «Підтримати» (текст «скоро»);
  - темну тему;
  - «Редагувати сторінку» на `/guide` веде на `edit/main/README.md`, а на `/en/agents` — на
    `edit/main/Docs/en/AgentGuide.md`.
- [ ] **Step 4:** Повідомте власникові, що лишилося зробити вручну:
  - Settings → Pages → Source: GitHub Actions;
  - `PAYPAL_BUTTON_ID`;
  - поле Website у репозиторії.

  Сам PR і merge — лише за його згодою.
