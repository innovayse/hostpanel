/**
 * Tests for the knowledge-base landing page against the real content store, API composable and
 * transport, with a stub BFF answering on a real socket (nothing of ours is mocked): categories and
 * the landing article list read through the content store, and the search that swaps them for
 * results and writes `?q=`.
 *
 * @module tests/pages/knowledgebase/index.test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import { ref } from 'vue'
import KbArticleRow from '~/components/kb/ArticleRow.vue'
import Page from '~/pages/knowledgebase/index.vue'
import { ContentPath } from '~/tests/helpers/contentPaths'
import { registerPageComponents, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import { mountRenderedPage } from '~/tests/helpers/renderedPage'
import { queryOf, requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'

/** The root element of the page. */
const ROOT = 'kb-page'

/** Spy for the router the page writes the query to. */
const router = { push: vi.fn(), replace: vi.fn(), back: vi.fn() }

/** Two categories, one holding a single article. */
const CATEGORIES = [
  { name: 'Billing', articleCount: 4 },
  { name: 'Domains', articleCount: 1 },
]

/** Articles the landing list and the search both return. */
const ARTICLES = {
  items: [
    { id: 31, title: 'Getting started', excerpt: 'Start here', category: 'General' },
    { id: 30, title: 'Next steps', excerpt: '', category: 'General' },
  ],
}

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Answers the content endpoints with the fixtures above.
 *
 * @param articles - What the article endpoint returns (landing list and search alike).
 */
const answerWith = (articles: { items: unknown[] } = ARTICLES): void => {
  table[`GET ${ContentPath.KbCategories}`] = { body: CATEGORIES }
  table[`GET ${ContentPath.KbArticles}`] = { body: articles }
}

/**
 * The requests made to the article endpoint so far.
 *
 * @returns The requests, oldest first.
 */
const articleReads = () => requestsTo(stub.requests, ContentPath.KbArticles)

/**
 * Types a term in the search box and submits the form.
 *
 * @param wrapper - The mounted page.
 * @param term - What to type.
 */
const submitSearch = async (wrapper: VueWrapper, term: string): Promise<void> => {
  await wrapper.find('[data-testid="kb-search-input"]').setValue(term)
  await wrapper.find('form').trigger('submit')
}

describe('pages/knowledgebase/index', () => {
  beforeEach(async () => {
    router.replace.mockReset()
    stubPublicPageRuntime()
    vi.stubGlobal('useRouter', () => router)
    registerPageComponents({ KbArticleRow })
    table = {}
    stub = await startTransport(routeTable(table))
  })

  // One test swaps `useAsyncData` for a read that stays pending; undo it so the next test gets the
  // setup file's stand-in again (it reinstalls the auto-imports before every test).
  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('shows the hero and the search box with its placeholder', async () => {
    answerWith()

    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('h1').text()).toBe('kb.title')
    expect(wrapper.find('[data-testid="kb-search-input"]').attributes('placeholder')).toBe('kb.searchPlaceholder')
  })

  it('links each category to its page with an article count', async () => {
    answerWith()

    const wrapper = await mountRenderedPage(Page, ROOT)

    const billing = wrapper.find('[data-testid="kb-category-card-billing"]')
    expect(billing.attributes('href')).toBe('/en/knowledgebase/category/billing')
    expect(billing.text()).toContain('Billing')
    expect(billing.text()).toContain('4 kb.articles')
    expect(wrapper.find('[data-testid="kb-category-card-domains"]').text()).toContain('1 kb.article')
  })

  it('lists the first articles in the order the backend sent them', async () => {
    answerWith()

    const wrapper = await mountRenderedPage(Page, ROOT)

    const rows = wrapper.findAll('[data-testid="kb-articles"] [data-testid^="kb-article-"][href]')
    expect(rows.map(row => row.attributes('data-testid'))).toEqual(['kb-article-31', 'kb-article-30'])
  })

  it('says so when there are no categories and no articles', async () => {
    table[`GET ${ContentPath.KbCategories}`] = { body: [] }
    table[`GET ${ContentPath.KbArticles}`] = { body: { items: [] } }

    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="kb-categories"]').text()).toContain('kb.empty')
    expect(wrapper.find('[data-testid="kb-articles"]').text()).toContain('kb.empty')
  })

  it('runs a search, writes it to the URL and shows the results instead of the landing view', async () => {
    answerWith()
    const wrapper = await mountRenderedPage(Page, ROOT)
    table[`GET ${ContentPath.KbArticles}`] = { body: { items: [{ id: 40, title: 'DNS basics' }] } }

    await submitSearch(wrapper, 'dns')
    await vi.waitFor(() => expect(wrapper.find('[data-testid="kb-search-count"]').exists()).toBe(true))

    expect(router.replace).toHaveBeenCalledWith({ query: { q: 'dns' } })
    expect(articleReads().map(queryOf).at(-1)).toMatchObject({ search: 'dns' })
    expect(wrapper.find('[data-testid="kb-search-count"]').text()).toBe('1 kb.articles')
    expect(wrapper.find('[data-testid="kb-search-results"] [data-testid="kb-article-40"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="kb-categories"]').exists()).toBe(false)
  })

  it('shows the empty-result state for a search that finds nothing', async () => {
    answerWith()
    const wrapper = await mountRenderedPage(Page, ROOT)
    table[`GET ${ContentPath.KbArticles}`] = { body: { items: [] } }

    await submitSearch(wrapper, 'zzz')
    await vi.waitFor(() => expect(wrapper.find('[data-testid="kb-search-empty"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="kb-search-empty"]').text()).toContain('kb.noResults')
  })

  it('returns to the landing view and clears the query', async () => {
    answerWith()
    const wrapper = await mountRenderedPage(Page, ROOT)
    await submitSearch(wrapper, 'dns')
    await vi.waitFor(() => expect(wrapper.find('[data-testid="kb-search-results"]').exists()).toBe(true))

    await wrapper.find('[data-testid="kb-clear-search"]').trigger('click')

    expect(router.replace).toHaveBeenLastCalledWith({ query: {} })
    expect(wrapper.find('[data-testid="kb-categories"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="kb-search-input"]').element).toHaveProperty('value', '')
  })

  it('treats a blank search as clearing it', async () => {
    answerWith()
    const wrapper = await mountRenderedPage(Page, ROOT)
    const readsBefore = articleReads().length

    await submitSearch(wrapper, '   ')
    await flushPromises()

    expect(articleReads()).toHaveLength(readsBefore)
    expect(wrapper.find('[data-testid="kb-categories"]').exists()).toBe(true)
  })

  it('repeats the search of a shared ?q= link on load', async () => {
    answerWith()
    stubPublicPageRuntime({ query: { q: 'ssl' } })
    vi.stubGlobal('useRouter', () => router)
    table[`GET ${ContentPath.KbArticles}`] = { body: { items: [{ id: 41, title: 'Install a certificate' }] } }

    const wrapper = await mountRenderedPage(Page, ROOT)
    await vi.waitFor(() => expect(wrapper.find('[data-testid="kb-search-results"]').text()).toContain('Install a certificate'))

    expect(articleReads().map(queryOf)).toContainEqual(expect.objectContaining({ search: 'ssl' }))
  })

  it('shows placeholders while the landing data loads', async () => {
    answerWith()
    vi.stubGlobal('useAsyncData', async () => ({ pending: ref(true) }))

    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="kb-categories"]').findAll('[data-testid="ui-skeleton"]')).toHaveLength(4)
    expect(wrapper.find('[data-testid="kb-articles"]').findAll('[data-testid="ui-skeleton"]')).toHaveLength(5)
  })

  it('shows the read failure with a retry instead of the empty text, and reads again on retry', async () => {
    table[`GET ${ContentPath.KbCategories}`] = { status: 400, body: { detail: 'Content is unavailable.' } }
    table[`GET ${ContentPath.KbArticles}`] = { body: ARTICLES }

    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="kb-load-error"]').text()).toContain('Content is unavailable.')
    expect(wrapper.text()).not.toContain('kb.empty')

    answerWith()
    await wrapper.find('[data-testid="kb-retry"]').trigger('click')
    await vi.waitFor(() => expect(wrapper.find('[data-testid="kb-categories"]').exists()).toBe(true))

    expect(requestsTo(stub.requests, ContentPath.KbCategories)).toHaveLength(2)
    expect(wrapper.find('[data-testid="kb-load-error"]').exists()).toBe(false)
  })
})
