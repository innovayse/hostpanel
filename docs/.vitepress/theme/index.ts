/**
 * Custom VitePress theme that extends the default theme with a `doc-after` slot
 * for per-page feedback and globally registers the shared documentation components.
 */
import { inBrowser } from 'vitepress'
import type { Theme } from 'vitepress'
import { recordRouteView } from './recentPages'
import DefaultTheme from 'vitepress/theme'
import { h } from 'vue'
import './custom.css'

import Callout from './components/Callout.vue'
import Steps from './components/Steps.vue'
import SettingTable from './components/SettingTable.vue'
import Label from './components/Label.vue'
import Screenshot from './components/Screenshot.vue'
import PageFeedback from './components/PageFeedback.vue'
import HomeSearch from './components/HomeSearch.vue'
import DemoLink from './components/DemoLink.vue'
import HomeDemoButton from './components/HomeDemoButton.vue'
import VersionBanner from './components/VersionBanner.vue'
import VersionSwitcher from './components/VersionSwitcher.vue'
import HomeLandmark from './components/HomeLandmark'
import NotFound from './components/NotFound.vue'

/** The theme object exported to VitePress. */
export default {
  extends: DefaultTheme,
  /**
   * Wraps the default layout: the PageFeedback widget after every documentation page (`doc-after`),
   * the version notice above the page (`layout-top`), the version list in the top bar and the main
   * landmark of the home page (`layout-bottom`). The not-found page is the themed one (`not-found`).
   */
  Layout() {
    return h(DefaultTheme.Layout, null, {
      'layout-top': () => h(VersionBanner),
      'layout-bottom': () => h(HomeLandmark),
      'nav-bar-content-after': () => h(VersionSwitcher),
      'doc-after': () => h(PageFeedback),
      'home-hero-actions-after': () => h(HomeDemoButton),
      'not-found': () => h(NotFound),
    })
  },
  /**
   * Registers all custom components globally so they can be used in any
   * Markdown file without an explicit import.
   *
   * It also records every page the reader opens for the search box's "Recently viewed" group.
   *
   * @param ctx - The VitePress context: the application, the router and the site data.
   */
  enhanceApp({ app, router, siteData }) {
    if (inBrowser) {
      const previous = router.onAfterRouteChange
      router.onAfterRouteChange = (to) => {
        recordRouteView(to, siteData.value.base, siteData.value.themeConfig.sidebar)
        return previous?.(to)
      }
    }
    app.component('Callout', Callout)
    app.component('Steps', Steps)
    app.component('SettingTable', SettingTable)
    app.component('Label', Label)
    app.component('Screenshot', Screenshot)
    app.component('PageFeedback', PageFeedback)
    app.component('HomeSearch', HomeSearch)
    app.component('DemoLink', DemoLink)
  },
} satisfies Theme
