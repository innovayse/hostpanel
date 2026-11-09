/**
 * Builds the language specific part of the VitePress theme config: navigation bar, sidebar, the labels
 * of the default theme controls and the search box texts. The same builder serves English (the root)
 * and every other language, so the three configs cannot drift apart. Pure functions with no I/O; the
 * generated endpoint groups of the API reference are passed in by the config.
 */
import { sidebarSectionText } from '../sidebarIcon'
import type { SidebarIcon } from '../sidebarIcon'
import sidebarData from './sidebar.json'
import { DEFAULT_LOCALE, isTranslatable, localizeLink } from './locales'
import type { LocaleKey } from './locales'
import type { UiStrings } from './ui'

/** One entry of the sidebar: a page, or a group with items. */
export interface SidebarEntry {
  /** Title shown in the sidebar (HTML for a section with an icon). */
  text: string
  /** Address of the page; absent for a group. */
  link?: string
  /** Whether the group starts collapsed. */
  collapsed?: boolean
  /** The entries below a group. */
  items?: SidebarEntry[]
}

/** One link of the navigation bar. */
export interface NavEntry {
  /** Text of the link. */
  text: string
  /** Address of the page, or the full address of the demo. */
  link: string
}

/** The endpoint groups the API reference generator writes: groups nest, pages carry a link. */
export interface GeneratedGroup {
  /** Title of the group or page, in English. */
  text: string
  /** Address of a page. */
  link?: string
  /** Whether the group starts collapsed. */
  collapsed?: boolean
  /** Entries below a group. */
  items?: GeneratedGroup[]
}

/** What the config passes in because it is the same in every language. */
export interface LocaleThemeOptions {
  /** Address of the demo admin panel; empty hides the link. */
  demoUrl: string
  /** The endpoint groups the generator wrote; empty in a checkout without them. */
  generated: GeneratedGroup[]
  /** Edit link: a pattern with `:path`, or a function that gets the page and returns the address. */
  editPattern: string | ((page: { filePath: string }) => string)
  /** The copyright line of the footer. */
  copyright: string
}

/**
 * Gives a link of the English tree its language folder, unless the page is not translated.
 * @param locale The language.
 * @param link A link that starts with a slash.
 * @returns The link in that language; an untranslated page keeps its English address.
 */
export const linkIn = (locale: LocaleKey, link: string): string => {
  if (locale === DEFAULT_LOCALE || !isTranslatable(link)) {
    return link
  }
  return localizeLink(locale, link)
}

/**
 * The title of a page link in a language.
 * @param strings The texts of the language.
 * @param link The link in the English tree.
 * @returns The title, or the link itself when the language file has none (the test for complete files fails first).
 */
const pageTitle = (strings: UiStrings, link: string): string => {
  return (strings.pages as Record<string, string>)[link] ?? link
}

/**
 * The title of a section in a language.
 * @param strings The texts of the language.
 * @param key The section key.
 * @returns The title, or the key when the language file has none.
 */
const sectionTitle = (strings: UiStrings, key: string): string => {
  return (strings.sections as Record<string, string>)[key] ?? key
}

/**
 * The navigation bar.
 * @param locale The language.
 * @param strings The texts of the language.
 * @param demoUrl Address of the demo admin panel; empty for none, which hides the link.
 * @returns The links of the bar.
 */
export const buildNav = (locale: LocaleKey, strings: UiStrings, demoUrl: string): NavEntry[] => {
  return [
    { text: strings.nav.home, link: linkIn(locale, '/') },
    { text: strings.nav.getStarted, link: linkIn(locale, '/get-started/') },
    { text: strings.nav.guides, link: linkIn(locale, '/operators/') },
    { text: strings.nav.apiReference, link: linkIn(locale, '/api-reference/') },
    { text: strings.nav.changelog, link: linkIn(locale, '/changelog/') },
    ...(demoUrl ? [{ text: strings.nav.demo, link: demoUrl }] : []),
  ]
}

/**
 * The sidebar of the guides.
 * @param locale The language.
 * @param strings The texts of the language.
 * @returns One collapsed group per section.
 */
export const buildMainSidebar = (locale: LocaleKey, strings: UiStrings): SidebarEntry[] => {
  return sidebarData.main.map((section) => ({
    text: sidebarSectionText(section.icon as SidebarIcon, sectionTitle(strings, section.key)),
    collapsed: true,
    items: section.links.map((link) => ({ text: pageTitle(strings, link), link: linkIn(locale, link) })),
  }))
}

/**
 * Translates and localizes the generated endpoint groups.
 * @param locale The language.
 * @param strings The texts of the language.
 * @param groups The generated groups (page titles are the titles of the API document and stay as written).
 * @param top Whether these are the audience groups, which carry an icon.
 * @returns The same tree with titles in the language and links below its folder.
 */
const localizeGenerated = (locale: LocaleKey, strings: UiStrings, groups: GeneratedGroup[], top: boolean): SidebarEntry[] => {
  const icons = sidebarData.apiGroupIcons as Record<string, SidebarIcon>
  return groups.map((group) => {
    const title = top ? ((strings.apiGroups as Record<string, string>)[group.text] ?? group.text) : group.text
    return {
      ...group,
      text: top ? sidebarSectionText(icons[group.text] ?? 'book-open', title) : title,
      ...(group.link ? { link: linkIn(locale, group.link) } : {}),
      ...(group.items ? { items: localizeGenerated(locale, strings, group.items, false) } : {}),
    }
  })
}

/**
 * The sidebar of the API reference: the shared pages, then the generated endpoint groups.
 * @param locale The language.
 * @param strings The texts of the language.
 * @param generated The endpoint groups the generator wrote; empty in a checkout without them.
 * @returns The entries.
 */
export const buildApiSidebar = (locale: LocaleKey, strings: UiStrings, generated: GeneratedGroup[]): SidebarEntry[] => {
  const section = sidebarData.apiReference
  return [
    {
      text: sidebarSectionText(section.icon as SidebarIcon, sectionTitle(strings, section.key)),
      items: section.links.map((link) => ({ text: pageTitle(strings, link), link: linkIn(locale, link) })),
    },
    ...localizeGenerated(locale, strings, generated, true),
  ]
}

/**
 * Every link of the sidebar in the English tree, for the tests and the docs checks.
 * @returns The links of the guides and of the shared API reference pages.
 */
export const sidebarLinks = (): string[] => {
  return [...sidebarData.main.flatMap((section) => section.links), ...sidebarData.apiReference.links]
}

/**
 * The search box texts of a language, in the shape of the local search `translations` option.
 * @param strings The texts of the language.
 * @returns The button and modal translations.
 */
export const buildSearchTranslations = (strings: UiStrings) => {
  const search = strings.search
  return {
    button: { buttonText: search.buttonText, buttonAriaLabel: search.buttonAriaLabel },
    modal: {
      displayDetails: search.displayDetails,
      resetButtonTitle: search.resetButtonTitle,
      backButtonTitle: search.backButtonTitle,
      noResultsText: search.noResultsText,
      recentTitle: search.recentTitle,
      suggestedTitle: search.suggestedTitle,
      footer: {
        selectText: search.selectText,
        selectKeyAriaLabel: search.selectKeyAriaLabel,
        navigateText: search.navigateText,
        navigateUpKeyAriaLabel: search.navigateUpKeyAriaLabel,
        navigateDownKeyAriaLabel: search.navigateDownKeyAriaLabel,
        closeText: search.closeText,
        closeKeyAriaLabel: search.closeKeyAriaLabel,
      },
    },
  }
}

/**
 * The part of the theme config that differs per language.
 * @param locale The language.
 * @param strings The texts of the language.
 * @param options What is the same in every language.
 * @returns Navigation, sidebar and the labels of the default theme; spread into `themeConfig` or into a locale's `themeConfig`.
 */
export const buildLocaleThemeConfig = (locale: LocaleKey, strings: UiStrings, options: LocaleThemeOptions) => {
  const theme = strings.theme
  return {
    nav: buildNav(locale, strings, options.demoUrl),
    sidebar: {
      // The API reference has a sidebar of its own, so its endpoint pages do not weigh down every other page.
      [linkIn(locale, '/api-reference/')]: buildApiSidebar(locale, strings, options.generated),
      [linkIn(locale, '/')]: buildMainSidebar(locale, strings),
    },
    outline: { label: theme.outlineTitle },
    docFooter: { prev: theme.docFooterPrev, next: theme.docFooterNext },
    lastUpdated: { text: theme.lastUpdatedText },
    editLink: { pattern: options.editPattern, text: theme.editLinkText },
    darkModeSwitchLabel: theme.darkModeSwitchLabel,
    lightModeSwitchTitle: theme.lightModeSwitchTitle,
    darkModeSwitchTitle: theme.darkModeSwitchTitle,
    sidebarMenuLabel: theme.sidebarMenuLabel,
    returnToTopLabel: theme.returnToTopLabel,
    langMenuLabel: theme.langMenuLabel,
    skipToContentLabel: theme.skipToContentLabel,
    footer: { message: theme.footerMessage, copyright: options.copyright },
  }
}
