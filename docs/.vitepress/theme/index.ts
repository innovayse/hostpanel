import type { Theme } from 'vitepress'
import DefaultTheme from 'vitepress/theme'
import './custom.css'

import Callout from './components/Callout.vue'
import Steps from './components/Steps.vue'
import SettingTable from './components/SettingTable.vue'
import Label from './components/Label.vue'
import Screenshot from './components/Screenshot.vue'

export default {
  extends: DefaultTheme,
  enhanceApp({ app }) {
    app.component('Callout', Callout)
    app.component('Steps', Steps)
    app.component('SettingTable', SettingTable)
    app.component('Label', Label)
    app.component('Screenshot', Screenshot)
  },
} satisfies Theme
