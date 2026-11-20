/**
 * Mount tests for ClientTransactionsView: it renders against the real kit and settles on an API
 * refusal with a Retry, a delete is confirmed first and reports its outcome next to the ledger, and the search, the
 * direction, payment method, period and amount filters and the page are kept in the address, sent to the API (which
 * narrows the rows and their totals before it pages) and shown as removable chips.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import UiSelect from '@/components/ui/UiSelect.vue'
import ClientTransactionsView from '@/views/ClientTransactionsView.vue'
import schema from '@/views/ClientTransactionsView.schema'
import { renderedCaptions, schemaCaptions, stubPendingFetch } from '../helpers/schemaTable'
import { transactionFixture } from '../helpers/billingFixtures'
import { mountClientView } from '../helpers/clientViews'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { useToastStore } from '@/stores/toast'

/** The one-transaction ledger of client 7. */
const ledger = () =>
  json({
    transactions: { items: [transactionFixture()], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 },
    totalIn: 120,
    totalOut: 0,
    totalFees: 3.5,
    balance: 116.5,
    currency: 'USD',
  })

/**
 * Mounts the screen for client 7 and waits for its first load.
 *
 * @returns The wrapper.
 */
const mountLedger = async () => {
  const { wrapper } = await mountRouted(ClientTransactionsView, '/clients/:id/transactions', '/clients/7/transactions')
  await flushPromises()
  return wrapper
}

/**
 * Mounts the screen for client 7 at an address and waits for its first load.
 *
 * @param query - The query string to open, with its leading question mark or empty.
 * @returns The wrapper and the router.
 */
const mountLedgerAt = async (query = '') => {
  const screen = await mountRouted(ClientTransactionsView, '/clients/:id/transactions', `/clients/7/transactions${query}`)
  await flushPromises()
  return screen
}

/**
 * Lists the requests the mock has seen for the client's ledger.
 *
 * @param mock - The stubbed fetch.
 * @returns The request URLs that went to the list endpoint.
 */
const listUrls = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/transactions/client/7?'))

/**
 * Opens the filter dialog, chooses a value in one of its selects and applies it.
 *
 * @param wrapper - The mounted tab.
 * @param testid - The `data-testid` of the select to use.
 * @param value - The option value to choose.
 */
const applyFilter = async (wrapper: VueWrapper, testid: string, value: string) => {
  await wrapper.get('[data-testid="client-transactions-filter"]').trigger('click')
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

describe('ClientTransactionsView', () => {
  it('mounts without throwing', async () => {
    const wrapper = await mountClientView(ClientTransactionsView)

    expect(wrapper.exists()).toBe(true)
  })

  it('renders its main action', async () => {
    const wrapper = await mountClientView(ClientTransactionsView)

    expect(wrapper.find('[data-testid="client-transactions-add"]').exists()).toBe(true)
  })

  it('shows the refusal sentence from the API', async () => {
    const wrapper = await mountClientView(ClientTransactionsView, 'Server said no')

    expect(wrapper.text()).toContain('Server said no')
  })

  it('reports a deleted transaction next to the ledger', async () => {
    stubRoutes({ 'GET /api/transactions/client/7': ledger, 'DELETE /api/transactions/9': () => new Response(null, { status: 204 }) })
    const wrapper = await mountLedger()

    await wrapper.get('[data-testid="client-transactions-delete-9"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Transaction deleted.'])
  })

  it('shows the API sentence when the delete is refused, without the load error', async () => {
    stubRoutes({ 'GET /api/transactions/client/7': ledger, 'DELETE /api/transactions/9': () => problem('The transaction is reconciled.', 409) })
    const wrapper = await mountLedger()

    await wrapper.get('[data-testid="client-transactions-delete-9"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-transactions-feedback"]').text()).toBe('The transaction is reconciled.')
    expect(wrapper.find('[data-testid="client-transactions-error"]').exists()).toBe(false)
  })

  it('does not delete when the confirmation is cancelled', async () => {
    const mock = stubRoutes({ 'GET /api/transactions/client/7': ledger })
    const wrapper = await mountLedger()

    await wrapper.get('[data-testid="client-transactions-delete-9"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)
  })

  it('puts the typed search in the address and sends it after the pause', async () => {
    const mock = stubRoutes({ 'GET /api/transactions/client/7': ledger })
    const { wrapper, router } = await mountLedgerAt()

    await wrapper.get('[data-testid="client-transactions-search"]').setValue('refund')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'refund' })
    expect(listUrls(mock).at(-1)).toBe('/api/transactions/client/7?page=1&pageSize=25&search=refund')
  })

  it('sends the direction picked in the filter dialog on Apply and shows it as a chip', async () => {
    const mock = stubRoutes({ 'GET /api/transactions/client/7': ledger })
    const { wrapper, router } = await mountLedgerAt()

    await applyFilter(wrapper, 'client-transactions-filter-direction', 'In')

    expect(router.currentRoute.value.query).toEqual({ direction: 'In' })
    expect(listUrls(mock).at(-1)).toBe('/api/transactions/client/7?page=1&pageSize=25&direction=In')
    expect(wrapper.get('[data-testid="filter-chip-direction"]').text()).toContain('Payments Received')
  })

  it('restores the search, the filters and the page from the address and sends them together', async () => {
    const mock = stubRoutes({ 'GET /api/transactions/client/7': ledger })
    const { wrapper } = await mountLedgerAt('?search=refund&direction=Out&paymentMethod=Credit%2FDebit%20Card&from=2026-03-01&to=2026-03-31&amount=20&page=2')

    expect(listUrls(mock)[0]).toBe(
      '/api/transactions/client/7?page=2&pageSize=25&search=refund&direction=Out&paymentMethod=Credit%2FDebit+Card&from=2026-03-01&to=2026-03-31&amount=20',
    )
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('5')
  })

  it('does not send a filter the address holds that is not a known value', async () => {
    const mock = stubRoutes({ 'GET /api/transactions/client/7': ledger })
    const { wrapper } = await mountLedgerAt('?direction=Sideways&from=soon&amount=0')

    expect(listUrls(mock)[0]).toBe('/api/transactions/client/7?page=1&pageSize=25')
    expect(wrapper.find('[data-testid="filter-chips"]').exists()).toBe(false)
  })

  it('shows the totals the API answered for the filters', async () => {
    stubRoutes({
      'GET /api/transactions/client/7': () =>
        json({
          transactions: { items: [transactionFixture()], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 },
          totalIn: 40,
          totalOut: 0,
          totalFees: 1,
          balance: 39,
          currency: 'USD',
        }),
    })
    const { wrapper } = await mountLedgerAt('?direction=In')

    expect(wrapper.text()).toContain('40.00 USD')
    expect(wrapper.text()).toContain('39.00 USD')
    expect(wrapper.get('[data-testid="client-transactions-stat-totalIn"]').text()).toContain('Total In')
    expect(wrapper.get('[data-testid="client-transactions-stat-balance"]').text()).toContain('39.00 USD')
  })

  it('removes a filter from the address and reloads when its chip is removed', async () => {
    const mock = stubRoutes({ 'GET /api/transactions/client/7': ledger })
    const { wrapper, router } = await mountLedgerAt('?direction=Out&amount=20')

    await wrapper.get('[data-testid="filter-chip-remove-direction"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ amount: '20' })
    expect(listUrls(mock).at(-1)).toBe('/api/transactions/client/7?page=1&pageSize=25&amount=20')
  })

  it('keeps the chosen page in the address and loads it', async () => {
    const mock = stubRoutes({
      'GET /api/transactions/client/7': () =>
        json({ transactions: { items: [transactionFixture()], pageNumber: 1, pageSize: 25, totalCount: 60, totalPages: 3 }, totalIn: 0, totalOut: 0, totalFees: 0, balance: 0, currency: 'USD' }),
    })
    const { wrapper, router } = await mountLedgerAt()

    await wrapper.get('[data-testid="client-transactions-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
    expect(listUrls(mock).at(-1)).toBe('/api/transactions/client/7?page=2&pageSize=25')
  })

  it('shows the empty state, and the filter hint with a way to clear the filters when the ledger is narrowed', async () => {
    stubRoutes({
      'GET /api/transactions/client/7': () =>
        json({ transactions: { items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 }, totalIn: 0, totalOut: 0, totalFees: 0, balance: 0, currency: 'USD' }),
    })
    const plain = (await mountLedgerAt()).wrapper
    expect(plain.get('[data-testid="client-transactions-empty"]').text()).toContain('No Records Found')
    expect(plain.find('[data-testid="client-transactions-clear-empty"]').exists()).toBe(false)

    const { wrapper, router } = await mountLedgerAt('?direction=Out&search=zzz')
    expect(wrapper.get('[data-testid="client-transactions-empty"]').text()).toContain('No transactions match your filters')

    await wrapper.get('[data-testid="client-transactions-clear-empty"]').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.query).toEqual({ search: 'zzz' })
  })

  it('reloads the ledger from the Retry of a refused load', async () => {
    let refused = true
    stubRoutes({ 'GET /api/transactions/client/7': () => (refused ? problem('Ledger is unavailable.', 503) : ledger()) })
    const wrapper = await mountLedger()

    refused = false
    await wrapper.get('[data-testid="client-transactions-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="client-transactions-error"]').exists()).toBe(false)
  })

  it('draws the table head from the schema columns', async () => {
    stubRoutes({ 'GET /api/transactions/client/7': ledger })
    const wrapper = await mountLedger()

    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })

  it('draws the table skeleton while the first load is in flight', async () => {
    stubPendingFetch()
    const { wrapper } = await mountRouted(ClientTransactionsView, '/clients/:id/transactions', '/clients/7/transactions')
    await flushPromises()

    expect(wrapper.find('[data-testid="ui-table-skeleton"]').exists()).toBe(true)
    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })
})
