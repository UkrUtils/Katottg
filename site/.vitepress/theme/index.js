import DefaultTheme from 'vitepress/theme'
import Releases from './Releases.vue'
import DonateButton from './DonateButton.vue'

// Мінімальне розширення стандартної теми; сюди реєструються власні компоненти.
export default {
  extends: DefaultTheme,
  enhanceApp({ app }) {
    app.component('Releases', Releases)
    app.component('DonateButton', DonateButton)
  },
}
