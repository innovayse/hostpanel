/**
 * Tests for the knowledge-base category page against the real content store, API composable and
 * transport, with a stub BFF answering on a real socket (nothing of ours is mocked): the slug
 * resolves to its category, the heading and the articles of that category, its SEO tags, its empty,
 * failure and loading states, the 404 for an unknown slug, and the retryable error (not a 404) when
 * the categories cannot be read.
 *
 * @module tests/pages/knowledgebase/category/[id].test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import KbArticleRow from '~/components/kb/ArticleRow.vue'
import Page from '~/pages/knowledgebase/category/[id].vue'
import { ContentPath } from '~/tests/helpers/contentPaths'
import { mountPage, registerPageComponents, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import type { PublicPageSpies } from '~/tests/helpers/publicPage'
import { mountRenderedPage } from '~/tests/helpers/renderedPage'
import { queryOf, requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The root element of the page. */
const ROOT = 'kb-category-page'

/** The key of the categories read in a route table. */
const CATEGORIES_READ = `GET ${ContentPath.KbCategories}`

/** The key of the articles read in a route table. */
const ARTICLES_READ = `GET ${ContentPath.KbArticles}`

/** The one category the page's slug names. */
const BILLING = [{ name: 'Billing', articleCount: 2 }]

/** The refusal the content endpoints answer with when they are unavailable. */
const UNAVAILABLE = { status: 400, body: { detail: 'Content is unavailable.' } }

/** Spies of the stood-in runtime, renewed before each test. */
let spies: PublicPageSpies

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * The requests made to the article endpoint so far.
 *
 * @returns The requests, oldest first.
 */
const articleReads = () => requestsTo(stub.requests, ContentPath.KbArticles)

describe('pages/knowledgebase/category/[id]', () => {
  beforeEach(async () => {
    spies = stubPublicPageRuntime({ params: { id: 'billing' } })
    registerPageComponents({ KbArticleRow })
    table = { [CATEGORIES_READ]: { body: BILLING } }
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('shows the category name and how many articles it holds', async () => {
    table[ARTICLES_READ] = { body: { items: [{ id: 1, title: 'Invoices' }, { id: 2, title: 'Refunds' }] } }

    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="kb-category-heading"]').text()).toBe('Billing')
    expect(wrapper.find('[data-testid="kb-category-count"]').text()).toBe('2 kb.articles')
  })

  it('asks the API for the articles of this category', async () => {
    table[ARTICLES_READ] = { body: { items: [] } }

    await mountRenderedPage(Page, ROOT)

    expect(articleReads().map(queryOf)).toContainEqual(expect.objectContaining({ category: 'Billing' }))
  })

  it('lists the articles as rows', async () => {
    table[ARTICLES_READ] = { body: { items: [{ id: 1, title: 'Invoices' }, { id: 2, title: 'Refunds' }] } }

    const wrapper = await mountRenderedPage(Page, ROOT)

    const rows = wrapper.find('[data-testid="kb-category-articles"]').findAll('[data-testid^="kb-article-"][href]')
    expect(rows).toHaveLength(2)
    expect(rows[0]?.attributes('href')).toBe('/en/knowledgebase/1')
  })

  it('resolves the slug whatever its case', async () => {
    spies = stubPublicPageRuntime({ params: { id: 'Billing-Payments' } })
    table[CATEGORIES_READ] = { body: [{ name: 'Billing & Payments', articleCount: 0 }] }
    table[ARTICLES_READ] = { body: { items: [] } }

    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="kb-category-heading"]').text()).toBe('Billing & Payments')
    expect(articleReads().map(queryOf)).toContainEqual(expect.objectContaining({ category: 'Billing & Payments' }))
  })

  it('raises a 404 for a slug no category has', async () => {
    table[CATEGORIES_READ] = { body: [] }
    table[ARTICLES_READ] = { body: { items: [] } }
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined)
    const consoleWarn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    const errorHandler = vi.fn()

    const wrapper = await mountPage(Page, { errorHandler })
    await waitUntil(() => expect(errorHandler).toHaveBeenCalled())

    expect(spies.createError).toHaveBeenCalledWith(expect.objectContaining({ statusCode: 404 }))
    expect(errorHandler).toHaveBeenCalledWith(expect.objectContaining({ statusCode: 404 }), expect.anything(), expect.anything())
    // The setup threw, so nothing of a category renders (the static page frame does, as for any
    // async setup that rejects under a test Suspense; under Nuxt the error page replaces it).
    expect(wrapper.find('[data-testid="kb-category-heading"]').exists()).toBe(false)
    expect(articleReads()).toHaveLength(0)
    consoleError.mockRestore()
    consoleWarn.mockRestore()
  })

  it('shows the category read failure with a retry instead of a 404, and reads again on retry', async () => {
    table[CATEGORIES_READ] = UNAVAILABLE
    table[ARTICLES_READ] = { body: { items: [{ id: 1, title: 'Invoices' }] } }

    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(spies.createError).not.toHaveBeenCalled()
    expect(wrapper.find('[data-testid="kb-category-error"]').text()).toContain('Content is unavailable.')
    expect(wrapper.find('[data-testid="kb-category-heading"]').exists()).toBe(false)
    expect(articleReads()).toHaveLength(0)

    table[CATEGORIES_READ] = { body: BILLING }
    await wrapper.find('[data-testid="kb-category-retry"]').trigger('click')
    await waitUntil(() => expect(wrapper.find('[data-testid="kb-category-articles"]').exists()).toBe(true))

    expect(requestsTo(stub.requests, ContentPath.KbCategories)).toHaveLength(2)
    expect(wrapper.find('[data-testid="kb-category-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="kb-category-heading"]').text()).toBe('Billing')
    expect(wrapper.find('[data-testid="kb-category-articles"]').text()).toContain('Invoices')
  })

  it('shows the 404 page when a retried category read no longer has the slug', async () => {
    table[CATEGORIES_READ] = UNAVAILABLE
    const showError = vi.fn()
    vi.stubGlobal('showError', showError)

    const wrapper = await mountRenderedPage(Page, ROOT)
    table[CATEGORIES_READ] = { body: [] }
    await wrapper.find('[data-testid="kb-category-retry"]').trigger('click')
    await waitUntil(() => expect(showError).toHaveBeenCalled())

    expect(showError).toHaveBeenCalledWith(expect.objectContaining({ statusCode: 404 }))
    expect(articleReads()).toHaveLength(0)
  })

  it('shows the read failure with a retry instead of the empty state, and reads again on retry', async () => {
    table[ARTICLES_READ] = UNAVAILABLE

    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="kb-category-error"]').text()).toContain('Content is unavailable.')
    expect(wrapper.find('[data-testid="kb-category-empty"]').exists()).toBe(false)

    table[ARTICLES_READ] = { body: { items: [{ id: 1, title: 'Invoices' }] } }
    await wrapper.find('[data-testid="kb-category-retry"]').trigger('click')
    await waitUntil(() => expect(wrapper.find('[data-testid="kb-category-articles"]').exists()).toBe(true))

    expect(articleReads()).toHaveLength(2)
    expect(wrapper.find('[data-testid="kb-category-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="kb-category-articles"]').text()).toContain('Invoices')
  })

  it('shows the empty state for a category without articles', async () => {
    table[ARTICLES_READ] = { body: { items: [] } }

    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="kb-category-empty"]').text()).toContain('kb.empty')
  })

  it('sets the SEO tags once, with the canonical path on the slug', async () => {
    table[ARTICLES_READ] = { body: { items: [] } }

    await mountRenderedPage(Page, ROOT)

    expect(spies.useSeo).toHaveBeenCalledTimes(1)
    expect(spies.useSeo).toHaveBeenCalledWith(expect.objectContaining({
      title: 'kb.categorySeoTitle',
      description: 'kb.categorySeoDescription',
      path: '/knowledgebase/category/billing',
    }))
    expect(spies.useSeoMeta).not.toHaveBeenCalled()
  })

  it('publishes the breadcrumb trail as structured data', async () => {
    table[ARTICLES_READ] = { body: { items: [] } }

    await mountRenderedPage(Page, ROOT)

    expect(spies.injectSchema).toHaveBeenCalledWith({
      '@type': 'BreadcrumbList',
      items: [
        { name: 'kb.title', url: '/en/knowledgebase' },
        { name: 'Billing', url: '/en/knowledgebase/category/billing' },
      ],
    })
  })

  it('shows placeholders while the articles load', async () => {
    table[ARTICLES_READ] = { body: { items: [] } }
    vi.stubGlobal('useAsyncData', async (_key: unknown, handler: () => Promise<unknown>) => {
      await handler()
      return { pending: ref(true) }
    })

    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="kb-category-loading"]').findAll('[data-testid="ui-skeleton"]')).toHaveLength(6)
  })
})
