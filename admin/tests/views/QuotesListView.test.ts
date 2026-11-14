/**
 * Mount tests for QuotesListView: the table renders what the API sent, the search text, the client, stage and
 * validity filters and the page are kept in the address, sent to the API (which narrows the list before it pages)
 * and shown as removable chips, and a refused load shows the API sentence.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import UiSelect from '@/components/ui/UiSelect.vue'
import QuotesListView from '@/views/QuotesListView.vue'
import { quoteRowFixture } from '../helpers/billingFixtures'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

/** The clients the client filter offers. */
const clientsPage = () =>
  json({
    items: [{ id: 7, firstName: 'Ada', lastName: 'Lovelace', companyName: null, email: 'ada@example.test', status: 'Active' }],
    totalCount: 1,
  })

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
 * Stubs the endpoints the list calls.
 *
 * @param quotes - The answer of the quotes endpoint.
 * @returns The mock, for asserting the requests made.
 */
const stubList = (quotes: () => Response = quotesPage): ReturnType<typeof vi.fn> =>
  stubRoutes({ 'GET /api/billing/quotes': quotes, 'GET /api/clients': clientsPage })

/**
 * Mounts the list at an address and waits for its first load.
 *
 * @param query - The query string to open, with its leading question mark or empty.
 * @returns The wrapper and the router.
 */
const mountList = async (query = '') => {
  const screen = await mountRouted(QuotesListView, '/billing/quotes', `/billing/quotes${query}`)
  await flushPromises()
  return screen
}

/**
 * Lists the requests the mock has seen for the quote list.
 *
 * @param mock - The stubbed fetch.
 * @returns The request URLs that went to the list endpoint.
 */
const listUrls = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/billing/quotes?'))

/**
 * Opens the filter dialog, chooses a value in one of its selects and applies it.
 *
 * @param wrapper - The mounted list.
 * @param testid - The `data-testid` of the select to use.
 * @param value - The option value to choose.
 */
const applyFilter = async (wrapper: VueWrapper, testid: string, value: string) => {
  await wrapper.get('[data-testid="billing-quotes-filter"]').trigger('click')
  await flushPromises()
  wrapper
    .findAllComponents(UiSelect)
    .find((select) => select.attributes('data-testid') === testid)
    ?.vm.$emit('update:modelValue', value)
  await flushPromises()
  await wrapper.get('[data-testid="filter-apply"]').trigger('click')
  await flushPromises()
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('QuotesListView', () => {
  it('renders a row per quote with its stage and a link to the quote', async () => {
    stubList()
    const { wrapper } = await mountList()

    expect(wrapper.findAll('[data-testid="billing-quotes-table"] tbody tr')).toHaveLength(2)
    expect(wrapper.get('[data-testid="billing-quotes-table"]').text()).toContain('Hosting renewal')
    expect(wrapper.get('[data-testid="billing-quote-open-3"]').attributes('href')).toBe('/billing/quotes/3')
    expect(wrapper.get('[data-testid="billing-quotes-create"]').attributes('href')).toBe('/billing/quotes/add')
  })

  it('prints the quote total in the currency of the quote', async () => {
    stubList(() => json({ items: [quoteRowFixture({ total: 250, currency: 'EUR' })], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 }))
    const { wrapper } = await mountList()

    expect(wrapper.get('[data-testid="billing-quotes-table"]').text()).toContain('250.00 EUR')
  })

  it('prints a quote stage as a name, not the value the API sends', async () => {
    stubList(() => json({ items: [quoteRowFixture({ stage: 'OnHold' })], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 }))
    const { wrapper } = await mountList()

    const table = wrapper.get('[data-testid="billing-quotes-table"]').text()
    expect(table).toContain('On Hold')
    expect(table).not.toContain('OnHold')
  })

  it('asks the API for the first page with no filter when the address holds none', async () => {
    const mock = stubList()
    await mountList()

    expect(listUrls(mock)).toEqual(['/api/billing/quotes?page=1&pageSize=25'])
  })

  it('puts the typed search in the address and sends it to the API after the pause, not narrowing the page itself', async () => {
    const mock = stubList()
    const { wrapper, router } = await mountList()

    await wrapper.get('[data-testid="billing-quotes-search"]').setValue('renewal')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'renewal' })
    expect(listUrls(mock).at(-1)).toBe('/api/billing/quotes?page=1&pageSize=25&search=renewal')
    // The API answered with both rows, so the screen shows both: it does not filter what it was sent.
    expect(wrapper.findAll('[data-testid="billing-quotes-table"] tbody tr')).toHaveLength(2)
  })

  it('sends the stage picked in the filter dialog on Apply and shows it as a chip', async () => {
    const mock = stubList()
    const { wrapper, router } = await mountList()

    await applyFilter(wrapper, 'billing-quotes-filter-stage', 'Delivered')

    expect(router.currentRoute.value.query).toEqual({ stage: 'Delivered' })
    expect(listUrls(mock).at(-1)).toBe('/api/billing/quotes?page=1&pageSize=25&stage=Delivered')
    expect(wrapper.get('[data-testid="filter-chip-stage"]').text()).toContain('Delivered')
  })

  it('sends the validity picked in the filter dialog on Apply and shows it as a chip', async () => {
    const mock = stubList()
    const { wrapper, router } = await mountList()

    await applyFilter(wrapper, 'billing-quotes-filter-validity', 'Expired')

    expect(router.currentRoute.value.query).toEqual({ validity: 'Expired' })
    expect(listUrls(mock).at(-1)).toBe('/api/billing/quotes?page=1&pageSize=25&validity=Expired')
    expect(wrapper.get('[data-testid="filter-chip-validity"]').text()).toContain('Expired')
  })

  it('restores the search, the client, the stage, the validity and the page from the address and sends them together', async () => {
    const mock = stubList()
    const { wrapper } = await mountList('?search=renewal&clientId=7&stage=Draft&validity=Valid&page=2')

    expect(listUrls(mock)[0]).toBe('/api/billing/quotes?page=2&pageSize=25&search=renewal&clientId=7&stage=Draft&validity=Valid')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('3')
    expect(wrapper.get('[data-testid="filter-chip-clientId"]').text()).toContain('Ada Lovelace')
  })

  it('does not send a filter the address holds that is not a known value', async () => {
    const mock = stubList()
    const { wrapper } = await mountList('?stage=Archived&validity=Maybe&clientId=abc')

    expect(listUrls(mock)[0]).toBe('/api/billing/quotes?page=1&pageSize=25')
    expect(wrapper.find('[data-testid="filter-chips"]').exists()).toBe(false)
  })

  it('removes a filter from the address and reloads when its chip is removed', async () => {
    const mock = stubList()
    const { wrapper, router } = await mountList('?stage=Draft&validity=Valid')

    await wrapper.get('[data-testid="filter-chip-remove-stage"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ validity: 'Valid' })
    expect(listUrls(mock).at(-1)).toBe('/api/billing/quotes?page=1&pageSize=25&validity=Valid')
  })

  it('keeps the chosen page in the address and loads it', async () => {
    const mock = stubList(() => json({ items: [quoteRowFixture()], pageNumber: 1, pageSize: 25, totalCount: 60, totalPages: 3 }))
    const { wrapper, router } = await mountList()

    await wrapper.get('[data-testid="billing-quotes-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
    expect(listUrls(mock).at(-1)).toBe('/api/billing/quotes?page=2&pageSize=25')
  })

  it('shows the API sentence when the load is refused', async () => {
    stubList(() => problem('Quotes are unavailable.', 403))
    const { wrapper } = await mountList()

    expect(wrapper.get('[data-testid="billing-quotes-error"]').text()).toContain('Quotes are unavailable.')
  })

  it('loads the page again from the retry of a refused load', async () => {
    let calls = 0
    stubList(() => {
      calls += 1
      return calls === 1 ? problem('Quotes are unavailable.', 403) : quotesPage()
    })
    const { wrapper } = await mountList()

    await wrapper.get('[data-testid="billing-quotes-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="billing-quotes-error"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="billing-quotes-table"] tbody tr')).toHaveLength(2)
  })
})
