/**
 * Tests for `stores/content.ts`, through the real API composable and transport against a stub server.
 *
 * The store, not the announcement and knowledgebase pages, owns the lists, the loading flags and
 * the failure sentence, so these assertions pin the contract the pages render from.
 *
 * @module tests/stores/content.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { startTransport } from '../helpers/stubTransport'
import { queryOf, requestsTo, routeTable } from '../helpers/stubRoutes'
import type { StubRouteTable } from '../helpers/stubRoutes'
import type { StubReply, StubServer } from '../helpers/stubServer'
import { useContentStore } from '~/stores/content'

/** The BFF path of the signed-in announcement list. */
const ANNOUNCEMENTS_PATH = '/api/portal/client/announcements'

/** The BFF path of the public announcement list. */
const PUBLIC_ANNOUNCEMENTS_PATH = '/api/portal/public/announcements'

/** The BFF path of the knowledgebase categories. */
const KB_CATEGORIES_PATH = '/api/portal/knowledgebase/categories'

/** The BFF path of the knowledgebase article list. */
const KB_ARTICLES_PATH = '/api/portal/knowledgebase/articles'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test, by `METHOD /path`. */
let table: StubRouteTable

/** An API refusal as the BFF answers it. */
const refusal: StubReply = { status: 404, body: { detail: 'Content unavailable.' } }

beforeEach(async () => {
  setActivePinia(createPinia())
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('useContentStore', () => {
  it('keeps the announcement list', async () => {
    table[`GET ${ANNOUNCEMENTS_PATH}`] = { body: { items: [{ id: 3, title: 'A', content: '', publishedAt: '2026-01-01' }, { id: 4, title: 'B', content: '', publishedAt: '2026-02-01' }] } }
    const store = useContentStore()

    await store.loadAnnouncements()

    expect(store.announcements).toHaveLength(2)
    expect(store.loading).toBe(false)
  })

  it('reads one announcement by id from the detail endpoint, whatever page the list is on', async () => {
    table[`GET ${PUBLIC_ANNOUNCEMENTS_PATH}/104`] = { body: { id: 104, title: 'Old news', content: '<p>x</p>', publishedAt: '2024-01-01' } }
    const store = useContentStore()

    const loaded = await store.loadAnnouncement('104')

    expect(stub.requests.map(request => request.url)).toEqual([`${PUBLIC_ANNOUNCEMENTS_PATH}/104`])
    expect(requestsTo(stub.requests, ANNOUNCEMENTS_PATH)).toHaveLength(0)
    expect(loaded).toMatchObject({ title: 'Old news' })
    expect(store.announcementFor('104')).toMatchObject({ title: 'Old news' })
    expect(store.announcementFor('99')).toBeNull()
  })

  it('keeps the API sentence and no announcement when the detail is refused', async () => {
    table[`GET ${PUBLIC_ANNOUNCEMENTS_PATH}/9`] = refusal
    const store = useContentStore()

    expect(await store.loadAnnouncement('9')).toBeNull()
    expect(store.announcementFor('9')).toBeNull()
    expect(store.error).toBe('Content unavailable.')
  })

  it('keeps the failure status of a refused read and clears it on the next good one', async () => {
    // A 503 on a read is retried once by the transport, so the stub answers it for both attempts.
    table[`GET ${PUBLIC_ANNOUNCEMENTS_PATH}/9`] = { status: 503, body: { detail: 'Unavailable.' } }
    const store = useContentStore()

    await store.loadAnnouncement('9')
    expect(store.errorStatus).toBe(503)

    table[`GET ${PUBLIC_ANNOUNCEMENTS_PATH}/9`] = { body: { id: 9, title: 'Back', content: '', publishedAt: '2026-03-01' } }
    await store.loadAnnouncement('9')
    expect(store.errorStatus).toBeNull()
  })

  it('records 502 when a read got no answer', async () => {
    table[`GET ${PUBLIC_ANNOUNCEMENTS_PATH}/9`] = { status: 502 }
    const store = useContentStore()

    await store.loadAnnouncement('9')

    expect(store.errorStatus).toBe(502)
  })

  it('keeps the API sentence and an empty list when the announcements are refused', async () => {
    table[`GET ${ANNOUNCEMENTS_PATH}`] = refusal
    const store = useContentStore()

    const list = await store.loadAnnouncements()

    expect(list).toEqual([])
    expect(store.error).toBe('Content unavailable.')
  })

  it('keeps the dashboard news', async () => {
    table[`GET ${PUBLIC_ANNOUNCEMENTS_PATH}`] = { body: { items: [{ id: 1, title: 'News', content: '', publishedAt: '2026-03-01' }] } }
    const store = useContentStore()

    await store.loadRecentNews()

    expect(store.recentNews).toEqual([{ id: 1, title: 'News', content: '', publishedAt: '2026-03-01' }])
  })

  it('keeps the first page of articles in the order the backend sent them', async () => {
    table[`GET ${KB_ARTICLES_PATH}`] = { body: { items: [{ id: 2, title: 'b', excerpt: '', category: 'x' }, { id: 1, title: 'a', excerpt: '', category: 'x' }, { id: 3, title: 'c', excerpt: '', category: 'x' }] } }
    const store = useContentStore()

    await store.loadLandingArticles()

    expect(queryOf(requestsTo(stub.requests, KB_ARTICLES_PATH)[0]!)).toEqual({ pageSize: '10' })
    expect(store.landingArticles.map(a => a.id)).toEqual([2, 1, 3])
  })

  it('keeps articles per category and names the category from the loaded list', async () => {
    table[`GET ${KB_CATEGORIES_PATH}`] = { body: [{ name: 'Billing', articleCount: 1 }] }
    table[`GET ${KB_ARTICLES_PATH}`] = { body: { items: [{ id: 1, title: 'a' }] } }
    const store = useContentStore()

    await store.loadKbCategories()
    await store.loadCategoryArticles('Billing')

    expect(queryOf(requestsTo(stub.requests, KB_ARTICLES_PATH)[0]!)).toEqual({ category: 'Billing', pageSize: '100' })
    expect(store.articlesFor('Billing')).toHaveLength(1)
    expect(store.articlesFor('Domains')).toEqual([])
    expect(store.categoryBySlug('billing')?.name).toBe('Billing')
    expect(store.categoryBySlug('domains')).toBeNull()
  })

  it('finds a category by its slug whatever the case', async () => {
    table[`GET ${KB_CATEGORIES_PATH}`] = { body: [{ name: 'Billing & Payments', articleCount: 3 }] }
    const store = useContentStore()

    await store.loadKbCategories()

    expect(store.categoryBySlug('Billing-Payments')?.name).toBe('Billing & Payments')
    expect(store.categoryBySlug('billing-payments')?.articleCount).toBe(3)
    expect(store.categoryBySlug('billing')).toBeNull()
  })

  it('keeps one article in full, and null when it cannot be read', async () => {
    table['GET /api/portal/knowledgebase/5'] = { body: { id: 5, title: 'T', body: '<p>x</p>' } }
    table['GET /api/portal/knowledgebase/6'] = refusal
    const store = useContentStore()

    expect(await store.loadArticle('5')).toMatchObject({ title: 'T' })
    expect(store.articleFor('5')).toMatchObject({ title: 'T' })

    expect(await store.loadArticle('6')).toBeNull()
    expect(store.articleFor('6')).toBeNull()
    expect(store.error).toBe('Content unavailable.')
  })

  it('searches the knowledgebase, flags the search while it runs and can clear it', async () => {
    table[`GET ${KB_ARTICLES_PATH}`] = { body: { items: [{ id: 1, title: 'match' }] } }
    const store = useContentStore()

    const pending = store.searchArticles('vps')
    expect(store.searching).toBe(true)
    await pending

    expect(queryOf(requestsTo(stub.requests, KB_ARTICLES_PATH)[0]!)).toEqual({ search: 'vps', pageSize: '50' })
    expect(store.searchResults).toHaveLength(1)
    expect(store.searching).toBe(false)

    store.clearSearch()
    expect(store.searchResults).toEqual([])
  })
})
