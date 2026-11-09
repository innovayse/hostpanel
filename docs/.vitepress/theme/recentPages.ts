/**
 * Logic behind the two groups the search box shows before anything is typed: the pages the
 * reader viewed last, and a short list of suggested pages.
 *
 * The module has no Vue and no VitePress import, so a unit test can run it in plain Node. The
 * browser pieces (the storage, the sidebar, the router) are handed in by the caller.
 *
 * Only the path of a page is stored. Its title and section are read from the sidebar every time,
 * so a renamed page shows its new name and a page that left the sidebar is dropped on the next read.
 *
 * The suggested pages are written once, in the English tree, and are put into the open language when the
 * rows are built: the address gets the language folder and the title comes from the language file.
 */

import { DEFAULT_LOCALE, localizeLink } from '../i18n/locales'
import type { LocaleKey } from '../i18n/locales'
import type { UiStrings } from '../i18n/ui'

/** Key of the list of recently viewed page paths in `localStorage`. */
export const RECENT_PAGES_KEY = 'hostpanel-docs:recent-pages'

/** How many recently viewed pages are kept and shown. */
export const MAX_RECENT_PAGES = 5

/** One sidebar entry as it appears in the site configuration: a group has `items`, a page has `link`. */
export interface SidebarNode {
  /** Title; may carry the decorative icon span the sidebar adds to a section title. */
  text?: string
  /** Page address without the site base, for example `/get-started/quick-start`. */
  link?: string
  /** Child entries of a group. */
  items?: SidebarNode[]
}

/** The `sidebar` option of the theme: a list, or lists keyed by the path prefix they belong to. */
export type SidebarConfig = SidebarNode[] | Record<string, SidebarNode[] | { items: SidebarNode[] }>

/** A documentation page the search box can link to. */
export interface CatalogPage {
  /** Normalized path, no base, no `.html`, a folder index ends with a slash. */
  path: string
  /** Plain page title from the sidebar. */
  title: string
  /** Plain title of the top level sidebar group the page sits in. */
  section: string
}

/** Every sidebar page by its normalized path. */
export type PageCatalog = Map<string, CatalogPage>

/** The part of the Web Storage interface this module uses; a test passes a plain object. */
export interface StringStore {
  /** Reads a value, or `null` when the key is not set. */
  getItem(key: string): string | null
  /** Writes a value. */
  setItem(key: string, value: string): void
}

/** The coloured tones a suggested page icon can take; each has a matching rule in the search box. */
export type SuggestedTone = 'blue' | 'green' | 'amber' | 'violet'

/** The icons the search box draws, a key of `SEARCH_ICONS` in `searchIcons.ts`. */
export type SearchIconName = 'file-text' | 'chevron-right' | 'rocket' | 'download' | 'sliders-horizontal' | 'book-open'

/** The titles of the suggested pages in one language: the `search.suggestedPages` object of a language file. */
export type SuggestedTitles = UiStrings['search']['suggestedPages']

/** Key of a suggested page title in `SuggestedTitles`. */
export type SuggestedTitleKey = keyof SuggestedTitles

/** One suggested page. */
export interface SuggestedPage {
  /** Normalized path of the page in the English tree; the unit test checks that its file exists. */
  path: string
  /** Key of the single line title shown in the row, looked up in the open language's `SuggestedTitles`. */
  titleKey: SuggestedTitleKey
  /** Icon drawn in the row. */
  icon: SearchIconName
  /** Colour of the icon. */
  tone: SuggestedTone
}

/** The pages offered to someone who opens the search box and has not typed anything. */
export const SUGGESTED_PAGES: readonly SuggestedPage[] = [
  { path: '/get-started/quick-start', titleKey: 'quickStart', icon: 'rocket', tone: 'blue' },
  { path: '/operators/install', titleKey: 'install', icon: 'download', tone: 'green' },
  { path: '/operators/configuration', titleKey: 'configuration', icon: 'sliders-horizontal', tone: 'amber' },
  { path: '/api-reference/', titleKey: 'apiReference', icon: 'book-open', tone: 'violet' },
]

/** Which group of the empty-query list a row belongs to. */
export type HomeGroupId = 'recent' | 'suggested'

/** One row of the empty-query list. */
export interface HomeItem {
  /** Normalized path of the page. */
  path: string
  /** Page title. */
  title: string
  /** Breadcrumb section; only recently viewed rows have one. */
  section?: string
  /** Icon of a suggested row. */
  icon?: SearchIconName
  /** Icon colour of a suggested row. */
  tone?: SuggestedTone
  /** Position in the single list the arrow keys walk, across both groups. */
  index: number
}

/** A titled group of rows. */
export interface HomeGroup {
  /** Which group this is. */
  id: HomeGroupId
  /** Rows of the group; their `index` continues from the group before. */
  items: HomeItem[]
}

/**
 * Turns an address into the form the catalog uses: no origin, base, query, hash or `.html`, and
 * `/folder/index` written as `/folder/`.
 * @param href Address as the router reports it (with the base) or as the sidebar writes it (without).
 * @param base Site base with a leading and a trailing slash, for example `/docs/`; use `/` for none.
 * @returns The normalized path, always starting with a slash.
 */
export const normalizePagePath = (href: string, base = '/'): string => {
  let path = href.replace(/^[a-z][a-z0-9+.-]*:\/\/[^/]+/i, '').split('#')[0].split('?')[0]
  try {
    path = decodeURI(path)
  } catch {
    // A malformed escape is kept as written; it will not be in the catalog.
  }
  if (base !== '/' && path.startsWith(base)) {
    path = `/${path.slice(base.length)}`
  }
  if (!path.startsWith('/')) {
    path = `/${path}`
  }
  return path.replace(/\.html$/, '').replace(/\/index$/, '/')
}

/**
 * Reduces a sidebar title to plain text. A section title carries a decorative icon span and escaped
 * characters (see `sidebarIcon.ts`), which must not reach the search box as text.
 * @param html Title as written in the sidebar.
 * @returns The title with tags removed and the three escapes undone.
 */
export const plainSidebarText = (html: string): string => {
  return html
    .replace(/<[^>]*>/g, '')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&amp;/g, '&')
    .trim()
}

/** Catalogs already built, by the sidebar object they came from, so the tree is walked once. */
const catalogCache = new WeakMap<object, PageCatalog>()

/**
 * Lists every page of the sidebar with its title and top level section.
 * @param sidebar The theme's `sidebar` option; `undefined` gives an empty catalog.
 * @returns The catalog; when a path appears twice the first entry wins.
 */
export const buildPageCatalog = (sidebar: SidebarConfig | undefined): PageCatalog => {
  if (!sidebar) {
    return new Map()
  }
  const cached = catalogCache.get(sidebar)
  if (cached) {
    return cached
  }
  const catalog: PageCatalog = new Map()
  const groups: SidebarNode[] = Array.isArray(sidebar)
    ? sidebar
    : Object.values(sidebar).flatMap((entry) => (Array.isArray(entry) ? entry : entry.items))

  /**
   * Adds one entry and everything below it.
   * @param node Sidebar entry.
   * @param section Plain title of the top level group the entry is in.
   */
  const walk = (node: SidebarNode, section: string): void => {
    if (node.link) {
      const path = normalizePagePath(node.link)
      if (!catalog.has(path)) {
        catalog.set(path, { path, title: plainSidebarText(node.text ?? path), section })
      }
    }
    for (const child of node.items ?? []) {
      walk(child, section)
    }
  }

  for (const group of groups) {
    walk(group, plainSidebarText(group.text ?? ''))
  }
  catalogCache.set(sidebar, catalog)
  return catalog
}

/**
 * Checks a stored value and keeps what is still valid.
 * @param value Whatever was parsed from storage.
 * @param catalog Pages that exist now.
 * @returns At most `MAX_RECENT_PAGES` distinct paths that are in the catalog, in the stored order.
 */
export const sanitizePaths = (value: unknown, catalog: PageCatalog): string[] => {
  if (!Array.isArray(value)) {
    return []
  }
  const kept: string[] = []
  for (const entry of value) {
    if (typeof entry === 'string' && catalog.has(entry) && !kept.includes(entry)) {
      kept.push(entry)
    }
    if (kept.length === MAX_RECENT_PAGES) {
      break
    }
  }
  return kept
}

/** Reads and records the recently viewed pages. */
export interface RecentTracker {
  /**
   * Lists the recently viewed pages that still exist, newest first.
   * @param catalog Pages that exist now.
   * @returns Paths, at most `MAX_RECENT_PAGES`.
   */
  read(catalog: PageCatalog): string[]
  /**
   * Records a page view: the page moves to the front and the list is cut to `MAX_RECENT_PAGES`.
   * A path that is not in the catalog is ignored.
   * @param path Normalized path of the viewed page.
   * @param catalog Pages that exist now.
   */
  record(path: string, catalog: PageCatalog): void
}

/**
 * Creates a tracker over a store. Every storage call is guarded: a store that is missing, full,
 * blocked or holding garbage never throws. The list is also kept in memory, so the feature keeps
 * working for the rest of the visit when the browser refuses storage.
 * @param store Storage to persist to, or `null` when there is none.
 * @param key Storage key, `RECENT_PAGES_KEY` by default.
 * @returns The tracker.
 */
export const createRecentTracker = (store: StringStore | null, key = RECENT_PAGES_KEY): RecentTracker => {
  let memory: string[] = []

  /**
   * Reads the stored list.
   * @returns The parsed value, or `undefined` when storage cannot be read at all.
   */
  const load = (): unknown => {
    if (!store) {
      return undefined
    }
    let raw: string | null
    try {
      raw = store.getItem(key)
    } catch {
      return undefined
    }
    if (raw === null) {
      return []
    }
    try {
      return JSON.parse(raw)
    } catch {
      return []
    }
  }

  /**
   * Lists the valid recent paths, from storage or, when storage cannot be read, from memory.
   * @param catalog Pages that exist now.
   * @returns Paths, newest first.
   */
  const read = (catalog: PageCatalog): string[] => {
    const stored = load()
    return sanitizePaths(stored === undefined ? memory : stored, catalog)
  }

  /**
   * Moves a page to the front of the list, in memory and in storage.
   * @param path Normalized path of the viewed page.
   * @param catalog Pages that exist now.
   */
  const record = (path: string, catalog: PageCatalog): void => {
    if (!catalog.has(path)) {
      return
    }
    const next = [path, ...read(catalog).filter((entry) => entry !== path)].slice(0, MAX_RECENT_PAGES)
    memory = next
    if (!store) {
      return
    }
    try {
      store.setItem(key, JSON.stringify(next))
    } catch {
      // Storage is full or blocked; the in-memory list above still serves this visit.
    }
  }

  return { read, record }
}

/**
 * Returns the browser's `localStorage`, or `null` outside a browser or when the browser refuses
 * access to it (some privacy modes throw on the property read itself).
 * @returns The storage or `null`.
 */
export const getBrowserStore = (): StringStore | null => {
  if (typeof window === 'undefined') {
    return null
  }
  try {
    return window.localStorage
  } catch {
    return null
  }
}

/** The tracker of this visit, created on first use in the browser. */
let sharedTracker: RecentTracker | undefined

/**
 * Returns the one tracker the router hook and the search box share, so both see the same
 * in-memory fallback when storage is unavailable.
 * @returns The shared tracker.
 */
export const getRecentTracker = (): RecentTracker => {
  sharedTracker ??= createRecentTracker(getBrowserStore())
  return sharedTracker
}

/**
 * Gives the address of a suggested page in a language.
 * @param page The suggested page.
 * @param locale The language; English keeps the address as written, the others get their folder.
 * @returns The normalized path, for example `/ru/get-started/quick-start`.
 */
export const suggestedPagePath = (page: SuggestedPage, locale: LocaleKey = DEFAULT_LOCALE): string => {
  return localizeLink(locale, page.path)
}

/**
 * Builds the rows of the empty-query list: recently viewed first, suggested after. The indexes run
 * on across both groups, so arrow keys and Enter treat them as one list.
 * @param recentPaths Valid recently viewed paths, newest first.
 * @param catalog Pages that exist now, for their titles and sections.
 * @param locale The open language; the suggested addresses get its folder (`/ru/...`).
 * @param titles The suggested page titles of the open language.
 * @returns One or two groups; the recent group is left out when it has no rows.
 */
export const buildHomeGroups = (
  recentPaths: string[],
  catalog: PageCatalog,
  locale: LocaleKey,
  titles: SuggestedTitles,
): HomeGroup[] => {
  let index = 0
  const groups: HomeGroup[] = []
  const recentItems: HomeItem[] = []
  for (const path of recentPaths) {
    const page = catalog.get(path)
    if (page) {
      recentItems.push({ path, title: page.title, section: page.section, index: index++ })
    }
  }
  if (recentItems.length > 0) {
    groups.push({ id: 'recent', items: recentItems })
  }
  groups.push({
    id: 'suggested',
    items: SUGGESTED_PAGES.map((page) => ({
      path: suggestedPagePath(page, locale),
      title: titles[page.titleKey],
      icon: page.icon,
      tone: page.tone,
      index: index++,
    })),
  })
  return groups
}

/**
 * Records the page the router just opened. Called after every route change, including the first
 * page load, so the list reflects what was read and not only what was opened from search.
 * @param href Address the router reports, with the site base.
 * @param base Site base with a leading and a trailing slash.
 * @param sidebar The theme's `sidebar` option, the source of the pages that exist.
 * @param tracker Tracker to record into; the shared one by default.
 */
export const recordRouteView = (
  href: string,
  base: string,
  sidebar: SidebarConfig | undefined,
  tracker: RecentTracker = getRecentTracker(),
): void => {
  tracker.record(normalizePagePath(href, base), buildPageCatalog(sidebar))
}
