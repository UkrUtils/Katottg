import { defineConfig } from 'vitepress'
import { REPO, PAYPAL_BUTTON_ID } from './site.config.js'
import { donateUrl } from './lib/donate.js'

const GITHUB = `https://github.com/${REPO}`

if (donateUrl(PAYPAL_BUTTON_ID) === null) {
  console.warn('[site] PAYPAL_BUTTON_ID не задано — кнопку донату приховано')
}

// VitePress серіалізує функцію editLink.pattern у клієнтський бандл, тому вона має бути
// самодостатньою: жодних посилань на змінні модуля (вони там undefined).
// Сторінки із заглушками-@include редагують у справжньому джерелі, а не в заглушці.
const editPattern = ({ filePath }: { filePath: string }) => {
  const sources: Record<string, string> = {
    'overview.md': 'README.md',
    'api.md': 'Docs/Api.md',
    'deep-dive.md': 'Docs/DeepDive.md',
    'agents.md': 'Docs/AgentGuide.md',
    'roadmap.md': 'Docs/Roadmap.md',
    'contributing.md': 'Docs/Contributing.md',
    'uk/overview.md': 'Docs/uk/README.md',
    'uk/api.md': 'Docs/uk/Api.md',
    'uk/deep-dive.md': 'Docs/uk/DeepDive.md',
    'uk/agents.md': 'Docs/uk/AgentGuide.md',
    'uk/roadmap.md': 'Docs/uk/Roadmap.md',
    'uk/contributing.md': 'Docs/uk/Contributing.md',
  }
  return `https://github.com/UkrUtils/Katottg/edit/main/${sources[filePath] ?? `site/${filePath}`}`
}

type Labels = {
  docs: string
  overview: string
  api: string
  deepDive: string
  agents: string
  roadmap: string
  contributing: string
  releases: string
  donate: string
}

const nav = (prefix: string, t: Labels) => [
  { text: t.docs, link: `${prefix}/overview`, activeMatch: `^${prefix}/(overview|api|deep-dive|roadmap|contributing)` },
  { text: t.agents, link: `${prefix}/agents` },
  { text: t.releases, link: `${prefix}/releases` },
  { text: t.donate, link: `${prefix}/donate` },
]

const sidebar = (prefix: string, t: Labels) => [
  {
    text: t.docs,
    items: [
      { text: t.overview, link: `${prefix}/overview` },
      { text: t.api, link: `${prefix}/api` },
      { text: t.deepDive, link: `${prefix}/deep-dive` },
      { text: t.agents, link: `${prefix}/agents` },
      { text: t.roadmap, link: `${prefix}/roadmap` },
      { text: t.contributing, link: `${prefix}/contributing` },
    ],
  },
  { text: t.releases, link: `${prefix}/releases` },
  { text: t.donate, link: `${prefix}/donate` },
]

const en: Labels = {
  docs: 'Docs',
  overview: 'Overview',
  api: 'API',
  deepDive: 'Deep dive',
  agents: 'For AI agents',
  roadmap: 'Roadmap',
  contributing: 'Contributing',
  releases: 'Releases',
  donate: 'Donate',
}

const uk: Labels = {
  docs: 'Документація',
  overview: 'Огляд',
  api: 'API',
  deepDive: 'Поглиблено',
  agents: 'Для AI-агентів',
  roadmap: 'Дорожня карта',
  contributing: 'Як долучитися',
  releases: 'Релізи',
  donate: 'Підтримати',
}

export default defineConfig({
  base: '/Katottg/',
  cleanUrls: true,
  themeConfig: {
    socialLinks: [{ icon: 'github', link: GITHUB }],
    search: {
      provider: 'local',
      options: {
        locales: {
          uk: {
            translations: {
              button: { buttonText: 'Пошук', buttonAriaLabel: 'Пошук' },
              modal: {
                displayDetails: 'Показати докладніше',
                resetButtonTitle: 'Скинути пошук',
                backButtonTitle: 'Закрити пошук',
                noResultsText: 'Нічого не знайдено за запитом',
                footer: {
                  selectText: 'вибрати',
                  selectKeyAriaLabel: 'Enter',
                  navigateText: 'перейти',
                  navigateUpKeyAriaLabel: 'Стрілка вгору',
                  navigateDownKeyAriaLabel: 'Стрілка вниз',
                  closeText: 'закрити',
                  closeKeyAriaLabel: 'Esc',
                },
              },
            },
          },
        },
      },
    },
  },
  locales: {
    root: {
      label: 'English',
      lang: 'en-US',
      title: 'UkrUtils.Katottg',
      description: 'A .NET library for the KATOTTG codifier: search, hierarchy, embedded data.',
      themeConfig: {
        nav: nav('', en),
        sidebar: sidebar('', en),
        editLink: { pattern: editPattern, text: 'Edit this page on GitHub' },
        footer: { message: 'Code — MIT. Data — the official KATOTTG of the Ministry.' },
      },
    },
    uk: {
      label: 'Українська',
      lang: 'uk-UA',
      link: '/uk/',
      title: 'UkrUtils.Katottg',
      description: 'Бібліотека .NET для роботи з кодифікатором КАТОТТГ: пошук, ієрархія, вбудовані дані.',
      themeConfig: {
        nav: nav('/uk', uk),
        sidebar: sidebar('/uk', uk),
        editLink: { pattern: editPattern, text: 'Редагувати цю сторінку на GitHub' },
        footer: { message: 'Код — MIT. Дані — офіційний КАТОТТГ Мінрегіону.' },
        outline: { label: 'На цій сторінці' },
        docFooter: { prev: 'Попередня сторінка', next: 'Наступна сторінка' },
        darkModeSwitchLabel: 'Вигляд',
        returnToTopLabel: 'Нагору',
        sidebarMenuLabel: 'Меню',
        langMenuLabel: 'Змінити мову',
        skipToContentLabel: 'Перейти до вмісту',
        lightModeSwitchTitle: 'Перемкнути на світлу тему',
        darkModeSwitchTitle: 'Перемкнути на темну тему',
        notFound: {
          title: 'СТОРІНКУ НЕ ЗНАЙДЕНО',
          quote: 'Але якщо не змінювати напрямок і не шукати, де ви помилилися, можна опинитися там, куди прямуєте.',
          linkLabel: 'перейти на головну',
          linkText: 'Повернутися на головну',
        },
      },
    },
  },
})
