/**
 * Mount tests for QuotesListView: the table renders what the API sent, the subject filter
 * narrows the page, and a refused load shows the API sentence.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import QuotesListView from '@/views/QuotesListView.vue'
import { quoteRowFixture } from '../helpers/billingFixtures'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

/** The quotes endpoint answer: two quotes on one page. */
const quotesPage = () =>
  json({
    items: [quoteRowFixture(), quoteRowFixture({ id: 4, subject: 'Other work', stage: 'Draft' })],
    pageNumber: 1,
    pageSize: 25,
    totalCount: 2,
    totalPages: 1,
  })

/**
 * Mounts the list and waits for its first load.
 *
 * @returns The wrapper.
 */
const mountList = async () => {
  const { wrapper } = await mountRouted(QuotesListView, '/billing/quotes', '/billing/quotes')
  await flushPromises()
  return wrapper
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('QuotesListView', () => {
  it('renders a row per quote with its stage and a link to the quote', async () => {
    stubRoutes({ 'GET /api/billing/quotes': quotesPage, 'GET /api/clients': () => json({ items: [], totalCount: 0 }) })
    const wrapper = await mountList()

    expect(wrapper.findAll('[data-testid="billing-quotes-table"] tbody tr')).toHaveLength(2)
    expect(wrapper.get('[data-testid="billing-quotes-table"]').text()).toContain('Hosting renewal')
    expect(wrapper.get('[data-testid="billing-quote-open-3"]').attributes('href')).toBe('/billing/quotes/3')
    expect(wrapper.get('[data-testid="billing-quotes-create"]').attributes('href')).toBe('/billing/quotes/add')
  })

  it('prints the quote total in the currency of the quote', async () => {
    stubRoutes({
      'GET /api/billing/quotes': () => json({ items: [quoteRowFixture({ total: 250, currency: 'EUR' })], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 }),
      'GET /api/clients': () => json({ items: [], totalCount: 0 }),
    })
    const wrapper = await mountList()

    expect(wrapper.get('[data-testid="billing-quotes-table"]').text()).toContain('250.00 EUR')
  })

  it('narrows the page by the search text while typing and restores it when cleared', async () => {
    stubRoutes({ 'GET /api/billing/quotes': quotesPage, 'GET /api/clients': () => json({ items: [], totalCount: 0 }) })
    const wrapper = await mountList()

    await wrapper.get('[data-testid="billing-quotes-search"]').setValue('renewal')
    expect(wrapper.findAll('[data-testid="billing-quotes-table"] tbody tr')).toHaveLength(1)

    await wrapper.get('[data-testid="billing-quotes-search"]').setValue('')
    expect(wrapper.findAll('[data-testid="billing-quotes-table"] tbody tr')).toHaveLength(2)
  })

  it('narrows the page by the stage in the address and shows it as a removable chip', async () => {
    stubRoutes({ 'GET /api/billing/quotes': quotesPage, 'GET /api/clients': () => json({ items: [], totalCount: 0 }) })
    const { wrapper, router } = await mountRouted(QuotesListView, '/billing/quotes', '/billing/quotes?stage=Draft')
    await flushPromises()

    expect(wrapper.findAll('[data-testid="billing-quotes-table"] tbody tr')).toHaveLength(1)
    expect(wrapper.get('[data-testid="filter-chip-stage"]').text()).toContain('Draft')

    await wrapper.get('[data-testid="filter-chip-remove-stage"]').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.query.stage).toBeUndefined()
    expect(wrapper.findAll('[data-testid="billing-quotes-table"] tbody tr')).toHaveLength(2)
  })

  it('shows the API sentence when the load is refused', async () => {
    stubRoutes({
      'GET /api/billing/quotes': () => problem('Quotes are unavailable.', 403),
      'GET /api/clients': () => json({ items: [], totalCount: 0 }),
    })
    const wrapper = await mountList()

    expect(wrapper.get('[data-testid="billing-quotes-error"]').text()).toContain('Quotes are unavailable.')
  })

  it('loads the page again from the retry of a refused load', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/billing/quotes': () => {
        calls += 1
        return calls === 1 ? problem('Quotes are unavailable.', 403) : quotesPage()
      },
      'GET /api/clients': () => json({ items: [], totalCount: 0 }),
    })
    const wrapper = await mountList()

    await wrapper.get('[data-testid="billing-quotes-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="billing-quotes-error"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="billing-quotes-table"] tbody tr')).toHaveLength(2)
  })

  it('prints a quote stage as a name, not the value the API sends', async () => {
    stubRoutes({
      'GET /api/billing/quotes': () =>
        json({ items: [quoteRowFixture({ stage: 'OnHold' })], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 }),
      'GET /api/clients': () => json({ items: [], totalCount: 0 }),
    })
    const wrapper = await mountList()

    const table = wrapper.get('[data-testid="billing-quotes-table"]').text()
    expect(table).toContain('On Hold')
    expect(table).not.toContain('OnHold')
  })
})
