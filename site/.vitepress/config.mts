import { defineConfig } from 'vitepress'
import { REPO } from './site.config.js'

const GITHUB = `https://github.com/${REPO}`

// VitePress серіалізує функцію editLink.pattern у клієнтський бандл, тому вона має бути
// самодостатньою: жодних посилань на змінні модуля (вони там undefined).
// Сторінки із заглушками-@include редагують у справжньому джерелі, а не в заглушці.
const editPattern = ({ filePath }: { filePath: string }) => {
  const sources: Record<string, string> = {
    'guide.md': 'README.md',
    'agents.md': 'Docs/AgentGuide.md',
    'en/guide.md': 'Docs/en/README.md',
    'en/agents.md': 'Docs/en/AgentGuide.md',
  }
  return `https://github.com/UkrUtils/Katottg/edit/main/${sources[filePath] ?? `site/${filePath}`}`
}

const nav = (prefix: string, labels: string[]) => [
  { text: labels[0], link: `${prefix}/guide` },
  { text: labels[1], link: `${prefix}/agents` },
  { text: labels[2], link: `${prefix}/releases` },
  { text: labels[3], link: `${prefix}/donate` },
]

export default defineConfig({
  base: '/Katottg/',
  cleanUrls: true,
  // Тимчасово: сторінок guide/agents/releases/donate ще немає (Task 5 прибере це).
  ignoreDeadLinks: [/^\/(en\/)?(guide|agents|releases|donate)$/],
  themeConfig: {
    socialLinks: [{ icon: 'github', link: GITHUB }],
    search: {
      provider: 'local',
      options: {
        locales: {
          root: {
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
      label: 'Українська',
      lang: 'uk-UA',
      title: 'UkrUtils.Katottg',
      description: 'Бібліотека .NET для роботи з кодифікатором КАТОТТГ: пошук, ієрархія, вбудовані дані.',
      themeConfig: {
        nav: nav('', ['Документація', 'Для AI-агентів', 'Релізи', 'Підтримати']),
        sidebar: nav('', ['Документація', 'Для AI-агентів', 'Релізи', 'Підтримати']),
        editLink: { pattern: editPattern, text: 'Редагувати цю сторінку на GitHub' },
        footer: { message: 'Код — MIT. Дані — офіційний КАТОТТГ Мінрегіону.' },
        outline: { label: 'На цій сторінці' },
        docFooter: { prev: 'Попередня сторінка', next: 'Наступна сторінка' },
        darkModeSwitchLabel: 'Вигляд',
        returnToTopLabel: 'Нагору',
        sidebarMenuLabel: 'Меню',
        langMenuLabel: 'Змінити мову',
      },
    },
    en: {
      label: 'English',
      lang: 'en-US',
      link: '/en/',
      title: 'UkrUtils.Katottg',
      description: 'A .NET library for the KATOTTG codifier: search, hierarchy, embedded data.',
      themeConfig: {
        nav: nav('/en', ['Docs', 'For AI agents', 'Releases', 'Donate']),
        sidebar: nav('/en', ['Docs', 'For AI agents', 'Releases', 'Donate']),
        editLink: { pattern: editPattern, text: 'Edit this page on GitHub' },
        footer: { message: 'Code — MIT. Data — the official KATOTTG of the Ministry.' },
      },
    },
  },
})
