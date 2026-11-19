/**
 * Mount tests for ClientQuotesView: it renders against the real kit and settles on an API refusal
 * with a Retry, a delete is confirmed first and reports its outcome next to the list, and the search, the stage and
 * validity filters and the page are kept in the address, sent to the API and shown as removable chips.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import UiSelect from '@/components/ui/UiSelect.vue'
import ClientQuotesView from '@/views/ClientQuotesView.vue'
import schema from '@/views/ClientQuotesView.schema'
import { renderedCaptions, schemaCaptions, stubPendingFetch } from '../helpers/schemaTable'
import { quoteRowFixture } from '../helpers/billingFixtures'
import { mountClientView } from '../helpers/clientViews'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { useToastStore } from '@/stores/toast'

/** The one-quote page of client 7. */
const listPage = () => json({ items: [quoteRowFixture()], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 })

/**
 * Mounts the screen for client 7 and waits for its first load.
 *
 * @returns The wrapper.
 */
const mountQuotes = async () => {
  const { wrapper } = await mountRouted(ClientQuotesView, '/clients/:id/quotes', '/clients/7/quotes')
  await flushPromises()
  return wrapper
}

/**
 * Mounts the screen for client 7 at an address and waits for its first load.
 *
 * @param query - The query string to open, with its leading question mark or empty.
 * @returns The wrapper and the router.
 */
const mountTabAt = async (query = '') => {
  const screen = await mountRouted(ClientQuotesView, '/clients/:id/quotes', `/clients/7/quotes${query}`)
  await flushPromises()
  return screen
}

/**
 * Lists the requests the mock has seen for the client's quote list.
 *
 * @param mock - The stubbed fetch.
 * @returns The request URLs that went to the list endpoint.
 */
const listUrls = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/billing/quotes/client/7?'))

/**
 * Opens the filter dialog, chooses a value in one of its selects and applies it.
 *
 * @param wrapper - The mounted tab.
 * @param testid - The `data-testid` of the select to use.
 * @param value - The option value to choose.
 */
const applyFilter = async (wrapper: VueWrapper, testid: string, value: string) => {
  await wrapper.get('[data-testid="client-quotes-filter"]').trigger('click')
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

describe('ClientQuotesView', () => {
  it('mounts without throwing', async () => {
    const wrapper = await mountClientView(ClientQuotesView)

    expect(wrapper.exists()).toBe(true)
  })

  it('renders its main action', async () => {
    const wrapper = await mountClientView(ClientQuotesView)

    expect(wrapper.find('[data-testid="client-quotes-create"]').exists()).toBe(true)
  })

  it('gives the remaining width to the subject column and keeps the id column as narrow as its content', async () => {
    stubRoutes({ 'GET /api/billing/quotes/client/7': listPage })
    const wrapper = await mountQuotes()

    const headers = wrapper.findAll('th')
    expect(headers[0]?.classes()).toContain('w-px')
    expect(headers[1]?.classes()).toEqual(expect.arrayContaining(['w-full', 'max-w-0']))
  })

  it('shows the refusal sentence from the API', async () => {
    const wrapper = await mountClientView(ClientQuotesView, 'Server said no')

    expect(wrapper.text()).toContain('Server said no')
  })

  it('reports a deleted quote next to the list', async () => {
    stubRoutes({ 'GET /api/billing/quotes/client/7': listPage, 'DELETE /api/quotes/3': () => new Response(null, { status: 204 }) })
    const wrapper = await mountQuotes()

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="client-quotes-actions-3"]').trigger('click')
    await wrapper.get('[data-testid="client-quotes-delete-3"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Quote deleted.'])
  })

  it('shows the API sentence when the delete is refused, without the load error', async () => {
    stubRoutes({ 'GET /api/billing/quotes/client/7': listPage, 'DELETE /api/quotes/3': () => problem('The quote was accepted.', 409) })
    const wrapper = await mountQuotes()

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="client-quotes-actions-3"]').trigger('click')
    await wrapper.get('[data-testid="client-quotes-delete-3"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-quotes-feedback"]').text()).toBe('The quote was accepted.')
    expect(wrapper.find('[data-testid="client-quotes-error"]').exists()).toBe(false)
  })

  it('does not delete when the confirmation is cancelled', async () => {
    const mock = stubRoutes({ 'GET /api/billing/quotes/client/7': listPage })
    const wrapper = await mountQuotes()

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="client-quotes-actions-3"]').trigger('click')
    await wrapper.get('[data-testid="client-quotes-delete-3"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)
  })

  it('puts the typed search in the address and sends it after the pause', async () => {
    const mock = stubRoutes({ 'GET /api/billing/quotes/client/7': listPage })
    const { wrapper, router } = await mountTabAt()

    await wrapper.get('[data-testid="client-quotes-search"]').setValue('renewal')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'renewal' })
    expect(listUrls(mock).at(-1)).toBe('/api/billing/quotes/client/7?page=1&pageSize=25&search=renewal')
  })

  it('sends the stage and the validity picked in the filter dialog and shows them as chips', async () => {
    const mock = stubRoutes({ 'GET /api/billing/quotes/client/7': listPage })
    const { wrapper, router } = await mountTabAt()

    await applyFilter(wrapper, 'client-quotes-filter-stage', 'Accepted')
    await applyFilter(wrapper, 'client-quotes-filter-validity', 'Valid')

    expect(router.currentRoute.value.query).toEqual({ stage: 'Accepted', validity: 'Valid' })
    expect(listUrls(mock).at(-1)).toBe('/api/billing/quotes/client/7?page=1&pageSize=25&stage=Accepted&validity=Valid')
    expect(wrapper.get('[data-testid="filter-chip-stage"]').text()).toContain('Accepted')
    expect(wrapper.get('[data-testid="filter-chip-validity"]').text()).toContain('Valid')
  })

  it('restores the search, the filters and the page from the address and sends them together', async () => {
    const mock = stubRoutes({ 'GET /api/billing/quotes/client/7': listPage })
    const { wrapper } = await mountTabAt('?search=renewal&stage=Lost&validity=Expired&page=2')

    expect(listUrls(mock)[0]).toBe('/api/billing/quotes/client/7?page=2&pageSize=25&search=renewal&stage=Lost&validity=Expired')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('2')
  })

  it('does not send a filter the address holds that is not a known value', async () => {
    const mock = stubRoutes({ 'GET /api/billing/quotes/client/7': listPage })
    const { wrapper } = await mountTabAt('?stage=Archived&validity=Maybe')

    expect(listUrls(mock)[0]).toBe('/api/billing/quotes/client/7?page=1&pageSize=25')
    expect(wrapper.find('[data-testid="filter-chips"]').exists()).toBe(false)
  })

  it('removes a filter from the address and reloads when its chip is removed', async () => {
    const mock = stubRoutes({ 'GET /api/billing/quotes/client/7': listPage })
    const { wrapper, router } = await mountTabAt('?stage=Lost&validity=Expired')

    await wrapper.get('[data-testid="filter-chip-remove-stage"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ validity: 'Expired' })
    expect(listUrls(mock).at(-1)).toBe('/api/billing/quotes/client/7?page=1&pageSize=25&validity=Expired')
  })

  it('keeps the chosen page in the address and loads it', async () => {
    const mock = stubRoutes({ 'GET /api/billing/quotes/client/7': () => json({ items: [quoteRowFixture()], pageNumber: 1, pageSize: 25, totalCount: 60, totalPages: 3 }) })
    const { wrapper, router } = await mountTabAt()

    await wrapper.get('[data-testid="client-quotes-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
    expect(listUrls(mock).at(-1)).toBe('/api/billing/quotes/client/7?page=2&pageSize=25')
  })

  it('shows the empty state, and the filter hint with a way to clear the filters when the list is narrowed', async () => {
    stubRoutes({ 'GET /api/billing/quotes/client/7': () => json({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 }) })
    const plain = (await mountTabAt()).wrapper
    expect(plain.get('[data-testid="client-quotes-empty"]').text()).toContain('No quotes found.')
    expect(plain.find('[data-testid="client-quotes-clear-empty"]').exists()).toBe(false)

    const { wrapper, router } = await mountTabAt('?stage=Lost&search=zzz')
    expect(wrapper.get('[data-testid="client-quotes-empty"]').text()).toContain('No quotes match your filters')

    await wrapper.get('[data-testid="client-quotes-clear-empty"]').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.query).toEqual({ search: 'zzz' })
  })

  it('reloads the list from the Retry of a refused load', async () => {
    let refused = true
    stubRoutes({ 'GET /api/billing/quotes/client/7': () => (refused ? problem('Quotes are disabled.', 403) : listPage()) })
    const wrapper = await mountQuotes()

    refused = false
    await wrapper.get('[data-testid="client-quotes-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="client-quotes-error"]').exists()).toBe(false)
  })

  it('draws the table head from the schema columns', async () => {
    stubRoutes({ 'GET /api/billing/quotes/client/7': listPage })
    const wrapper = await mountQuotes()

    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })

  it('draws the table skeleton while the first load is in flight', async () => {
    stubPendingFetch()
    const { wrapper } = await mountRouted(ClientQuotesView, '/clients/:id/quotes', '/clients/7/quotes')
    await flushPromises()

    expect(wrapper.find('[data-testid="ui-table-skeleton"]').exists()).toBe(true)
    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })
})
