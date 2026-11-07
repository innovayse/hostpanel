/**
 * Unit tests for the search box start view logic: path normalization, the sidebar catalog, the
 * recently viewed tracker (including every storage failure) and the grouped rows.
 */
import { existsSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import {
  MAX_RECENT_PAGES,
  RECENT_PAGES_KEY,
  SUGGESTED_PAGES,
  buildHomeGroups,
  buildPageCatalog,
  createRecentTracker,
  normalizePagePath,
  plainSidebarText,
  recordRouteView,
  sanitizePaths,
} from '../.vitepress/theme/recentPages'
import type { SidebarConfig, StringStore } from '../.vitepress/theme/recentPages'
import { SEARCH_ICONS } from '../.vitepress/theme/searchIcons'

/** Folder of the documentation sources. */
const docsRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')

/** A sidebar shaped like the real one: icon spans in section titles, a nested group, two sidebars. */
const sidebar: SidebarConfig = {
  '/api-reference/': [
    {
      text: '<span class="hp-sidebar-icon hp-sidebar-icon--book-open" aria-hidden="true"></span>API reference',
      items: [{ text: 'Overview', link: '/api-reference/' }],
    },
  ],
  '/': [
    {
      text: '<span class="hp-sidebar-icon hp-sidebar-icon--server" aria-hidden="true"></span>Servers &amp; SSH',
      items: [
        { text: 'Overview', link: '/servers/' },
        { text: 'Provisioning', link: '/servers/provisioning' },
        { text: 'Nested', items: [{ text: 'Deep page', link: '/servers/deep.html' }] },
      ],
    },
    { text: 'Get started', items: [{ text: 'Quick start', link: '/get-started/quick-start' }] },
  ],
}

/**
 * Builds an in-memory store.
 * @param initial Value to start with, stored under the default key.
 * @returns The store and its backing map.
 */
const memoryStore = (initial?: string): { store: StringStore; data: Map<string, string> } => {
  const data = new Map<string, string>()
  if (initial !== undefined) {
    data.set(RECENT_PAGES_KEY, initial)
  }
  return {
    data,
    store: {
      getItem: (key) => data.get(key) ?? null,
      setItem: (key, value) => {
        data.set(key, value)
      },
    },
  }
}

/** A store that throws on every call, like a browser with storage blocked. */
const throwingStore: StringStore = {
  getItem: () => {
    throw new Error('blocked')
  },
  setItem: () => {
    throw new Error('blocked')
  },
}

describe('normalizePagePath', () => {
  it('strips the base, the hash, the query and .html', () => {
    expect(normalizePagePath('/docs/operators/install.html?x=1#step', '/docs/')).toBe('/operators/install')
  })

  it('writes a folder index with a trailing slash', () => {
    expect(normalizePagePath('/docs/get-started/index.html', '/docs/')).toBe('/get-started/')
    expect(normalizePagePath('/staff/index')).toBe('/staff/')
  })

  it('removes an origin and adds a missing leading slash', () => {
    expect(normalizePagePath('https://example.test/docs/staff/orders', '/docs/')).toBe('/staff/orders')
    expect(normalizePagePath('staff/orders')).toBe('/staff/orders')
  })

  it('keeps a malformed escape as written', () => {
    expect(normalizePagePath('/a/%E0%A4%A')).toBe('/a/%E0%A4%A')
  })
})

describe('plainSidebarText', () => {
  it('removes the icon span and undoes the escapes', () => {
    expect(plainSidebarText('<span class="x" aria-hidden="true"></span>Servers &amp; SSH')).toBe('Servers & SSH')
  })
})

describe('buildPageCatalog', () => {
  it('lists pages of every sidebar with a plain title and top level section', () => {
    const catalog = buildPageCatalog(sidebar)
    expect(catalog.get('/servers/provisioning')).toEqual({
      path: '/servers/provisioning',
      title: 'Provisioning',
      section: 'Servers & SSH',
    })
    expect(catalog.get('/servers/deep')?.section).toBe('Servers & SSH')
    expect(catalog.get('/api-reference/')?.section).toBe('API reference')
    expect(catalog.has('/nope')).toBe(false)
  })

  it('accepts a plain list and an undefined sidebar', () => {
    expect(buildPageCatalog([{ text: 'A', items: [{ text: 'B', link: '/b' }] }]).get('/b')?.section).toBe('A')
    expect(buildPageCatalog(undefined).size).toBe(0)
  })
})

describe('sanitizePaths', () => {
  const catalog = buildPageCatalog(sidebar)

  it('keeps only known, distinct strings', () => {
    expect(sanitizePaths(['/servers/', 7, '/gone', '/servers/', null], catalog)).toEqual(['/servers/'])
  })

  it('answers an empty list for anything that is not an array', () => {
    expect(sanitizePaths({ a: 1 }, catalog)).toEqual([])
    expect(sanitizePaths('x', catalog)).toEqual([])
  })

  it('cuts the list to the maximum', () => {
    const many = buildPageCatalog(
      Array.from({ length: 9 }, (_, i) => ({ text: `P${i}`, link: `/p${i}` })),
    )
    expect(sanitizePaths(Array.from({ length: 9 }, (_, i) => `/p${i}`), many)).toHaveLength(MAX_RECENT_PAGES)
  })
})

describe('createRecentTracker', () => {
  const catalog = buildPageCatalog(sidebar)

  it('puts the newest page first and does not repeat a page', () => {
    const { store, data } = memoryStore()
    const tracker = createRecentTracker(store)
    tracker.record('/servers/', catalog)
    tracker.record('/get-started/quick-start', catalog)
    tracker.record('/servers/', catalog)
    expect(tracker.read(catalog)).toEqual(['/servers/', '/get-started/quick-start'])
    expect(JSON.parse(data.get(RECENT_PAGES_KEY) ?? '[]')).toEqual(['/servers/', '/get-started/quick-start'])
  })

  it('keeps the last five pages', () => {
    const pages = buildPageCatalog(Array.from({ length: 8 }, (_, i) => ({ text: `P${i}`, link: `/p${i}` })))
    const tracker = createRecentTracker(memoryStore().store)
    for (let i = 0; i < 8; i++) {
      tracker.record(`/p${i}`, pages)
    }
    expect(tracker.read(pages)).toEqual(['/p7', '/p6', '/p5', '/p4', '/p3'])
  })

  it('ignores a page that is not in the catalog', () => {
    const { store, data } = memoryStore()
    createRecentTracker(store).record('/unknown', catalog)
    expect(data.size).toBe(0)
  })

  it('drops stored pages that no longer exist when it reads', () => {
    const { store } = memoryStore(JSON.stringify(['/removed', '/servers/provisioning']))
    expect(createRecentTracker(store).read(catalog)).toEqual(['/servers/provisioning'])
  })

  it('reads garbage in storage as an empty list and recovers on the next record', () => {
    const { store } = memoryStore('{not json')
    const tracker = createRecentTracker(store)
    expect(tracker.read(catalog)).toEqual([])
    tracker.record('/servers/', catalog)
    expect(tracker.read(catalog)).toEqual(['/servers/'])
  })

  it('never throws when storage throws, and keeps the list in memory', () => {
    const tracker = createRecentTracker(throwingStore)
    expect(tracker.read(catalog)).toEqual([])
    expect(() => tracker.record('/servers/', catalog)).not.toThrow()
    tracker.record('/get-started/quick-start', catalog)
    expect(tracker.read(catalog)).toEqual(['/get-started/quick-start', '/servers/'])
  })

  it('works with no storage at all', () => {
    const tracker = createRecentTracker(null)
    tracker.record('/servers/', catalog)
    expect(tracker.read(catalog)).toEqual(['/servers/'])
  })
})

describe('recordRouteView', () => {
  it('records the normalized route of a known page and skips an unknown one', () => {
    const tracker = createRecentTracker(memoryStore().store)
    recordRouteView('/docs/servers/provisioning.html#x', '/docs/', sidebar, tracker)
    recordRouteView('/docs/not-in-sidebar', '/docs/', sidebar, tracker)
    expect(tracker.read(buildPageCatalog(sidebar))).toEqual(['/servers/provisioning'])
  })
})

describe('buildHomeGroups', () => {
  const catalog = buildPageCatalog(sidebar)

  it('shows only suggested pages when nothing was viewed', () => {
    const groups = buildHomeGroups([], catalog)
    expect(groups.map((group) => group.id)).toEqual(['suggested'])
    expect(groups[0].items.map((item) => item.title)).toEqual([
      'Quick start',
      'Install Hostpanel',
      'Configuration reference',
      'API reference',
    ])
  })

  it('numbers the rows of both groups as one list', () => {
    const groups = buildHomeGroups(['/servers/', '/get-started/quick-start'], catalog)
    expect(groups.map((group) => group.id)).toEqual(['recent', 'suggested'])
    const indexes = groups.flatMap((group) => group.items.map((item) => item.index))
    expect(indexes).toEqual([0, 1, 2, 3, 4, 5])
    expect(groups[0].items[0]).toMatchObject({ title: 'Overview', section: 'Servers & SSH' })
  })
})

describe('suggested pages', () => {
  it.each(SUGGESTED_PAGES.map((page) => [page.path]))('%s has a source file', (path) => {
    const file = path.endsWith('/') ? `${path}index.md` : `${path}.md`
    expect(existsSync(resolve(docsRoot, `.${file}`))).toBe(true)
  })

  it('gives every suggested page a one line title and a drawable icon', () => {
    for (const page of SUGGESTED_PAGES) {
      expect(page.title).not.toMatch(/\n/)
      expect(SEARCH_ICONS[page.icon].length).toBeGreaterThan(0)
    }
    expect(SEARCH_ICONS['file-text'].length).toBeGreaterThan(0)
    expect(SEARCH_ICONS['chevron-right'].length).toBeGreaterThan(0)
  })
})
