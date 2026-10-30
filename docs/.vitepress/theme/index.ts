import type { Theme } from 'vitepress'
import DefaultTheme from 'vitepress/theme'
import { h } from 'vue'
import './custom.css'

import Callout from './components/Callout.vue'
import Steps from './components/Steps.vue'
import SettingTable from './components/SettingTable.vue'
import Label from './components/Label.vue'
import Screenshot from './components/Screenshot.vue'
import PageFeedback from './components/PageFeedback.vue'

export default {
  extends: DefaultTheme,
  Layout() {
    return h(DefaultTheme.Layout, null, {
      'doc-after': () => h(PageFeedback),
    })
  },
  enhanceApp({ app }) {
    app.component('Callout', Callout)
    app.component('Steps', Steps)
    app.component('SettingTable', SettingTable)
    app.component('Label', Label)
    app.component('Screenshot', Screenshot)
    app.component('PageFeedback', PageFeedback)
  },
} satisfies Theme
