/**
 * Mount tests for TransactionsListView: the ledger totals and rows render, the search text, the client, direction,
 * payment method, period and amount filters and the page are kept in the address, sent to the API (which narrows the
 * rows and their totals before it pages) and shown as removable chips, a delete asks first, and a refused load shows
 * the API sentence.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import UiSelect from '@/components/ui/UiSelect.vue'
import TransactionsListView from '@/views/TransactionsListView.vue'
import schema from '@/views/TransactionsListView.schema'
import { renderedCaptions, schemaCaptions, stubPendingFetch } from '../helpers/schemaTable'
import { transactionFixture } from '../helpers/billingFixtures'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { useToastStore } from '@/stores/toast'
import { toastMessages } from '../helpers/toasts'

// The chart library needs a canvas the test environment does not have; TransactionChart has its own test.
vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

/** The ledger endpoint answer: two transactions and the ledger totals. */
const ledger = () =>
  json({
    transactions: {
      items: [transactionFixture(), transactionFixture({ id: 10, description: 'Refund to client', amountIn: 0, amountOut: 20 })],
      pageNumber: 1,
      pageSize: 25,
      totalCount: 2,
      totalPages: 1,
    },
    totalIn: 120,
    totalOut: 20,
    totalFees: 3.5,
    balance: 96.5,
    currency: 'EUR',
  })

/**
 * Mounts the ledger and waits for its first load.
 *
 * @returns The wrapper.
 */
const mountLedger = async () => {
  const { wrapper } = await mountRouted(TransactionsListView, '/billing/transactions', '/billing/transactions')
  await flushPromises()
  return wrapper
}

/**
 * Stubs the endpoints the ledger calls.
 *
 * @param answer - The answer of the transactions endpoint.
 * @returns The mock, for asserting the requests made.
 */
const stubLedger = (answer: () => Response = ledger): ReturnType<typeof vi.fn> =>
  stubRoutes({
    'GET /api/transactions?': answer,
    'GET /api/clients': () =>
      json({
        items: [{ id: 7, firstName: 'Ada', lastName: 'Lovelace', companyName: null, email: 'ada@example.test', status: 'Active' }],
        totalCount: 1,
      }),
  })

/**
 * Mounts the ledger at an address and waits for its first load.
 *
 * @param query - The query string to open, with its leading question mark or empty.
 * @returns The wrapper and the router.
 */
const mountLedgerAt = async (query = '') => {
  const screen = await mountRouted(TransactionsListView, '/billing/transactions', `/billing/transactions${query}`)
  await flushPromises()
  return screen
}

/**
 * Lists the requests the mock has seen for the ledger.
 *
 * @param mock - The stubbed fetch.
 * @returns The request URLs that went to the list endpoint.
 */
const listUrls = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/transactions?'))

/**
 * Opens the filter dialog, chooses a value in one of its selects and applies it.
 *
 * @param wrapper - The mounted ledger.
 * @param testid - The `data-testid` of the select to use.
 * @param value - The option value to choose.
 */
const applyFilter = async (wrapper: VueWrapper, testid: string, value: string) => {
  await wrapper.get('[data-testid="billing-transactions-filter"]').trigger('click')
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

describe('TransactionsListView', () => {
  it('shows the ledger totals and a row per transaction', async () => {
    stubRoutes({ 'GET /api/transactions?': ledger })
    const wrapper = await mountLedger()

    expect(wrapper.get('[data-testid="billing-transactions-total-in"]').text()).toContain('120.00 EUR')
    expect(wrapper.get('[data-testid="billing-transactions-balance"]').text()).toContain('96.50 EUR')
    expect(wrapper.findAll('[data-testid="billing-transactions-table"] tbody tr')).toHaveLength(2)
    expect(wrapper.get('[data-testid="billing-transactions-table"]').findAll('th').find((header) => header.text() === 'Description')?.classes()).toEqual(expect.arrayContaining(['w-[28%]', 'max-w-0', 'truncate']))
  })

  it('opens the edit page from the row actions menu', async () => {
    stubRoutes({ 'GET /api/transactions?': ledger })
    const { wrapper, router } = await mountRouted(TransactionsListView, '/billing/transactions', '/billing/transactions')
    await flushPromises()

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="billing-transaction-actions-9"]').trigger('click')
    const edit = wrapper.get('[data-testid="billing-transaction-edit-9"]')
    // A real link, so a new tab and a middle click open the same page.
    expect(edit.element.tagName).toBe('A')
    expect(edit.attributes('href')).toBe('/billing/transactions/9/edit')
    await edit.trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.fullPath).toBe('/billing/transactions/9/edit')
  })

  it('prints each row in its own currency while the tiles stay in the base currency', async () => {
    stubRoutes({
      'GET /api/transactions?': () =>
        json({
          transactions: { items: [transactionFixture({ amountIn: 5000, fees: 100, currency: 'AMD' })], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 },
          totalIn: 12,
          totalOut: 0,
          totalFees: 0.25,
          balance: 11.75,
          currency: 'EUR',
        }),
    })
    const wrapper = await mountLedger()

    const row = wrapper.get('[data-testid="billing-transactions-table"] tbody tr').text()
    expect(row).toContain('5,000.00 AMD')
    expect(row).toContain('100.00 AMD')
    expect(wrapper.get('[data-testid="billing-transactions-total-in"]').text()).toContain('12.00 EUR')
    expect(wrapper.get('[data-testid="billing-transactions-total-fees"]').text()).toContain('0.25 EUR')
  })

  it('asks the API for the first page with no filter when the address holds none', async () => {
    const mock = stubLedger()
    await mountLedger()

    expect(listUrls(mock)).toEqual(['/api/transactions?page=1&pageSize=25'])
  })

  it('puts the typed search in the address and sends it to the API after the pause, not narrowing the page itself', async () => {
    const mock = stubLedger()
    const { wrapper, router } = await mountLedgerAt()

    await wrapper.get('[data-testid="billing-transactions-search"]').setValue('refund')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'refund' })
    expect(listUrls(mock).at(-1)).toBe('/api/transactions?page=1&pageSize=25&search=refund')
    // The API answered with both rows, so the screen shows both: it does not filter what it was sent.
    expect(wrapper.findAll('[data-testid="billing-transactions-table"] tbody tr')).toHaveLength(2)
  })

  it('shows the totals the API answered for the filters, not the totals of the whole ledger', async () => {
    stubLedger(() =>
      json({
        transactions: { items: [transactionFixture({ id: 10, amountIn: 0, amountOut: 20 })], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 },
        totalIn: 0,
        totalOut: 20,
        totalFees: 0,
        balance: -20,
        currency: 'EUR',
      }),
    )
    const { wrapper } = await mountLedgerAt('?direction=Out')

    expect(wrapper.get('[data-testid="billing-transactions-total-out"]').text()).toContain('20.00 EUR')
    expect(wrapper.get('[data-testid="billing-transactions-total-in"]').text()).toContain('0.00 EUR')
    expect(wrapper.get('[data-testid="billing-transactions-balance"]').text()).toContain('-20.00 EUR')
  })

  it('sends the direction and the payment method picked in the filter dialog on Apply and shows them as chips', async () => {
    const mock = stubLedger()
    const { wrapper, router } = await mountLedgerAt()

    await applyFilter(wrapper, 'billing-transactions-filter-direction', 'Out')
    await applyFilter(wrapper, 'billing-transactions-filter-method', 'Card')

    expect(router.currentRoute.value.query).toEqual({ direction: 'Out', paymentMethod: 'Card' })
    expect(listUrls(mock).at(-1)).toBe('/api/transactions?page=1&pageSize=25&direction=Out&paymentMethod=Card')
    expect(wrapper.get('[data-testid="filter-chip-direction"]').text()).toContain('Payments Sent')
    expect(wrapper.get('[data-testid="filter-chip-paymentMethod"]').text()).toContain('Credit/Debit Card')
  })

  it('restores the search, the client, the filters and the page from the address and sends them together', async () => {
    const mock = stubLedger()
    const { wrapper } = await mountLedgerAt(
      '?search=refund&clientId=7&direction=In&paymentMethod=Bank%20Transfer&from=2026-03-01&to=2026-03-31&amount=12.5&page=2',
    )

    expect(listUrls(mock)[0]).toBe(
      '/api/transactions?page=2&pageSize=25&search=refund&clientId=7&direction=In&paymentMethod=Bank+Transfer&from=2026-03-01&to=2026-03-31&amount=12.5',
    )
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('6')
    expect(wrapper.get('[data-testid="filter-chip-clientId"]').text()).toContain('Ada Lovelace')
    expect(wrapper.get('[data-testid="filter-chip-amount"]').text()).toContain('12.5')
  })

  it('does not send a filter the address holds that is not a known value', async () => {
    const mock = stubLedger()
    const { wrapper } = await mountLedgerAt('?direction=Sideways&clientId=abc&from=soon&amount=-3')

    expect(listUrls(mock)[0]).toBe('/api/transactions?page=1&pageSize=25')
    expect(wrapper.find('[data-testid="filter-chips"]').exists()).toBe(false)
  })

  it('removes a filter from the address and reloads when its chip is removed', async () => {
    const mock = stubLedger()
    const { wrapper, router } = await mountLedgerAt('?direction=Out&amount=20')

    await wrapper.get('[data-testid="filter-chip-remove-direction"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ amount: '20' })
    expect(listUrls(mock).at(-1)).toBe('/api/transactions?page=1&pageSize=25&amount=20')
  })

  it('keeps the chosen page in the address and loads it', async () => {
    const mock = stubLedger(() =>
      json({ transactions: { items: [transactionFixture()], pageNumber: 1, pageSize: 25, totalCount: 60, totalPages: 3 }, totalIn: 0, totalOut: 0, totalFees: 0, balance: 0, currency: 'EUR' }),
    )
    const { wrapper, router } = await mountLedgerAt()

    expect(wrapper.find('[data-testid="ui-card-footer"] [data-testid="billing-transactions-pagination-next"]').exists()).toBe(true)

    await wrapper.get('[data-testid="billing-transactions-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
    expect(listUrls(mock).at(-1)).toBe('/api/transactions?page=2&pageSize=25')
  })

  it('deletes a transaction only after the confirmation', async () => {
    const mock = stubRoutes({
      'GET /api/transactions?': ledger,
      'DELETE /api/transactions/9': () => new Response(null, { status: 204 }),
    })
    const wrapper = await mountLedger()

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="billing-transaction-actions-9"]').trigger('click')
    await wrapper.get('[data-testid="billing-transaction-delete-9"]').trigger('click')
    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(true)
    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Record deleted.'])
  })

  it('shows the API sentence when a delete is refused, and sends nothing when dismissed', async () => {
    const mock = stubRoutes({
      'GET /api/transactions?': ledger,
      'DELETE /api/transactions/9': () => problem('Transaction is settled.', 409),
    })
    const wrapper = await mountLedger()

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="billing-transaction-actions-9"]').trigger('click')
    await wrapper.get('[data-testid="billing-transaction-delete-9"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()
    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="billing-transaction-actions-9"]').trigger('click')
    await wrapper.get('[data-testid="billing-transaction-delete-9"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(toastMessages()).toContain('Transaction is settled.')
    expect(wrapper.find('[data-testid="billing-transactions-error"]').exists()).toBe(false)
  })

  it('shows the API sentence when the load is refused', async () => {
    stubRoutes({ 'GET /api/transactions?': () => problem('Ledger is unavailable.', 403) })
    const wrapper = await mountLedger()

    expect(wrapper.get('[data-testid="billing-transactions-error"]').text()).toContain('Ledger is unavailable.')
  })

  it('loads the ledger again from the retry of a refused load', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/transactions?': () => {
        calls += 1
        return calls === 1 ? problem('Ledger is unavailable.', 403) : ledger()
      },
    })
    const wrapper = await mountLedger()

    await wrapper.get('[data-testid="billing-transactions-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="billing-transactions-error"]').exists()).toBe(false)
  })

  it('hands the revenue chart its loading state while the first load is pending', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const wrapper = await mountLedger()

    expect(wrapper.get('[data-testid="billing-transaction-chart"]').find('[data-testid="chart-skeleton"]').exists()).toBe(true)
  })

  it('draws the table skeleton under the schema head while the first load is in flight', async () => {
    stubPendingFetch()
    const { wrapper } = await mountRouted(TransactionsListView, '/billing/transactions', '/billing/transactions')
    await flushPromises()

    expect(wrapper.find('[data-testid="ui-table-skeleton"]').exists()).toBe(true)
    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })
})
