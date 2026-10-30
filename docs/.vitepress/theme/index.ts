/**
 * Custom VitePress theme that extends the default theme with a `doc-after` slot
 * for per-page feedback and globally registers the shared documentation components.
 */
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

/** The theme object exported to VitePress. */
export default {
  extends: DefaultTheme,
  /**
   * Wraps the default layout and injects the PageFeedback widget after every
   * documentation page via the `doc-after` slot.
   */
  Layout() {
    return h(DefaultTheme.Layout, null, {
      'doc-after': () => h(PageFeedback),
    })
  },
  /**
   * Registers all custom components globally so they can be used in any
   * Markdown file without an explicit import.
   *
   * @param app - The Vue application instance provided by VitePress.
   */
  enhanceApp({ app }) {
    app.component('Callout', Callout)
    app.component('Steps', Steps)
    app.component('SettingTable', SettingTable)
    app.component('Label', Label)
    app.component('Screenshot', Screenshot)
    app.component('PageFeedback', PageFeedback)
  },
} satisfies Theme
