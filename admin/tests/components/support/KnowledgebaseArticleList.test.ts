/**
 * Mount tests for KnowledgebaseArticleList: the list loads the page the address names, the search, the category and
 * the published filters and the page are kept in the address, sent to the API and shown as removable chips, a row's
 * Edit action is reported to the screen, `reload` reads the page again, and a refused load offers a retry.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import KnowledgebaseArticleList from '@/components/support/KnowledgebaseArticleList.vue'
import type { KbCategory } from '@/types/kbarticle'
import { json, problem } from '../../helpers/fetch'
import { applyListFilter } from '../../helpers/listFilter'
import { mountScreen } from '../../helpers/mount'
import { optionValues } from '../../helpers/select'
import { stubRoutes } from '../../helpers/routes'

/** The categories the category filter offers. */
const categories: KbCategory[] = [
  { id: 3, name: 'Billing', description: '', isHidden: false, parentCategoryId: null, articleCount: 2, slug: 'billing' },
  { id: 4, name: 'Hosting', description: '', isHidden: false, parentCategoryId: null, articleCount: 0, slug: 'hosting' },
]

/** One stored article, written in English and published. */
const article = {
  id: 11,
  slug: 'how-to-pay',
  categoryId: 3,
  categoryName: 'Billing',
  isPublished: true,
  translations: { en: { title: 'How to pay', content: 'Use a card' } },
}

/**
 * Builds the page of the article list the API answers.
 *
 * @param items - The articles of the page.
 * @param totalCount - The total across all pages.
 * @returns The response.
 */
const pageOf = (items: unknown[], totalCount = items.length) =>
  json({ items, pageNumber: 1, pageSize: 20, totalCount, totalPages: Math.max(1, Math.ceil(totalCount / 20)) })

/**
 * Mounts the list at an address and waits for its first load.
 *
 * @param query - The query string to open, with its leading question mark or empty.
 * @returns The wrapper and the router.
 */
const mountListAt = async (query = '') => {
  return mountScreen(KnowledgebaseArticleList, { categories }, `/support/knowledgebase${query}`)
}

/**
 * Lists the requests the mock has seen for the article list.
 *
 * @param mock - The stubbed fetch.
 * @returns The request URLs that went to the list endpoint.
 */
const listUrls = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/admin/knowledgebase?'))

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('KnowledgebaseArticleList', () => {
  it('loads the first page on mount and lists the article with its title, address, category and status', async () => {
    const mock = stubRoutes({ 'GET /api/admin/knowledgebase': () => pageOf([article]) })
    const { wrapper } = await mountListAt()

    expect(listUrls(mock)).toEqual(['/api/admin/knowledgebase?page=1&pageSize=20'])
    const row = wrapper.get('[data-testid="knowledgebase-article-11"]')
    expect(row.text()).toContain('How to pay')
    expect(row.text()).toContain('how-to-pay')
    expect(row.text()).toContain('Billing')
    expect(row.get('[role="img"]').attributes('aria-label')).toBe('Published')
    expect(wrapper.findAll('th').some((header) => header.classes().includes('max-w-0'))).toBe(true)
    expect(wrapper.get('[data-testid="knowledgebase-articles-count"]').text()).toContain('1 Records Found')
  })

  it('offers any first, then one category choice per category and the two published states', async () => {
    stubRoutes({ 'GET /api/admin/knowledgebase': () => pageOf([article]) })
    const { wrapper } = await mountListAt()

    await wrapper.get('[data-testid="knowledgebase-articles-filter"]').trigger('click')
    await flushPromises()

    expect(optionValues(wrapper, 'knowledgebase-articles-filter-category')).toEqual(['', '3', '4'])
    expect(optionValues(wrapper, 'knowledgebase-articles-filter-published')).toEqual(['', 'published', 'draft'])
  })

  it('puts the typed search in the address and sends it after the pause', async () => {
    const mock = stubRoutes({ 'GET /api/admin/knowledgebase': () => pageOf([article]) })
    const { wrapper, router } = await mountListAt()

    await wrapper.get('[data-testid="knowledgebase-articles-search"]').setValue('invoice')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'invoice' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/knowledgebase?page=1&pageSize=20&search=invoice')
  })

  it('sends the category and the published state picked in the filter dialog on Apply and shows them as chips', async () => {
    const mock = stubRoutes({ 'GET /api/admin/knowledgebase': () => pageOf([article]) })
    const { wrapper, router } = await mountListAt()

    await applyListFilter(
      wrapper,
      { 'knowledgebase-articles-filter-category': '3', 'knowledgebase-articles-filter-published': 'draft' },
      'knowledgebase-articles-filter',
    )

    expect(router.currentRoute.value.query).toEqual({ categoryId: '3', published: 'draft' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/knowledgebase?page=1&pageSize=20&categoryId=3&isPublished=false')
    expect(wrapper.get('[data-testid="filter-chip-categoryId"]').text()).toContain('Billing')
    expect(wrapper.get('[data-testid="filter-chip-published"]').text()).toContain('Draft')
  })

  it('restores the search, the filters and the page from the address and sends them together', async () => {
    const mock = stubRoutes({ 'GET /api/admin/knowledgebase': () => pageOf([article]) })
    const { wrapper } = await mountListAt('?search=pay&categoryId=4&published=published&page=2')

    expect(listUrls(mock)[0]).toBe('/api/admin/knowledgebase?page=2&pageSize=20&search=pay&categoryId=4&isPublished=true')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('2')
  })

  it('does not send a filter the address holds that is not a known value', async () => {
    const mock = stubRoutes({ 'GET /api/admin/knowledgebase': () => pageOf([article]) })
    const { wrapper } = await mountListAt('?categoryId=0&published=maybe')

    expect(listUrls(mock)[0]).toBe('/api/admin/knowledgebase?page=1&pageSize=20')
    expect(wrapper.find('[data-testid="filter-chips"]').exists()).toBe(false)
  })

  it('removes a filter from the address and reloads when its chip is removed', async () => {
    const mock = stubRoutes({ 'GET /api/admin/knowledgebase': () => pageOf([article]) })
    const { wrapper, router } = await mountListAt('?categoryId=3&published=draft')

    await wrapper.get('[data-testid="filter-chip-remove-categoryId"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ published: 'draft' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/knowledgebase?page=1&pageSize=20&isPublished=false')
  })

  it('keeps the chosen page in the address and loads it', async () => {
    const mock = stubRoutes({ 'GET /api/admin/knowledgebase': () => pageOf([article], 60) })
    const { wrapper, router } = await mountListAt()

    await wrapper.get('[data-testid="knowledgebase-articles-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/knowledgebase?page=2&pageSize=20')
  })

  it('reports the article whose Edit action is chosen', async () => {
    stubRoutes({ 'GET /api/admin/knowledgebase': () => pageOf([article]) })
    const { wrapper } = await mountListAt()

    await wrapper.get('[data-testid="knowledgebase-article-11-edit"]').trigger('click')

    expect(wrapper.emitted('edit')).toEqual([[article]])
  })

  it('reads the page again when the screen asks for a reload', async () => {
    const mock = stubRoutes({ 'GET /api/admin/knowledgebase': () => pageOf([article]) })
    const { wrapper } = await mountListAt('?page=2')

    await (wrapper.vm as unknown as { reload: () => Promise<boolean> }).reload()

    expect(listUrls(mock)).toEqual([
      '/api/admin/knowledgebase?page=2&pageSize=20',
      '/api/admin/knowledgebase?page=2&pageSize=20',
    ])
  })

  it('shows the empty state when there are no articles, and a narrower one when a filter is applied', async () => {
    stubRoutes({ 'GET /api/admin/knowledgebase': () => pageOf([]) })
    const plain = (await mountListAt()).wrapper
    expect(plain.find('[data-testid="knowledgebase-articles-empty"]').exists()).toBe(true)

    const narrowed = (await mountListAt('?published=draft')).wrapper
    expect(narrowed.find('[data-testid="knowledgebase-articles-empty-narrowed"]').exists()).toBe(true)
    expect(narrowed.find('[data-testid="knowledgebase-articles-empty"]').exists()).toBe(false)
  })

  it('shows an error with a Retry when the articles are refused, not the empty state, and reloads them', async () => {
    let refuse = true
    stubRoutes({ 'GET /api/admin/knowledgebase': () => (refuse ? problem('Not allowed.', 403) : pageOf([article])) })
    const { wrapper } = await mountListAt()

    expect(wrapper.get('[data-testid="knowledgebase-articles-error"]').text()).toContain('Not allowed.')
    expect(wrapper.find('[data-testid="knowledgebase-articles-empty"]').exists()).toBe(false)

    refuse = false
    await wrapper.get('[data-testid="knowledgebase-articles-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="knowledgebase-articles-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="knowledgebase-article-11"]').exists()).toBe(true)
  })
})
