/**
 * Mount tests for TransactionsListView: the ledger totals and rows render, the description
 * filter narrows the page, a delete asks first, and a refused load shows the API sentence.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import TransactionsListView from '@/views/TransactionsListView.vue'
import { transactionFixture } from '../helpers/billingFixtures'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

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
    expect(row).toContain('5000.00 AMD')
    expect(row).toContain('100.00 AMD')
    expect(wrapper.get('[data-testid="billing-transactions-total-in"]').text()).toContain('12.00 EUR')
    expect(wrapper.get('[data-testid="billing-transactions-total-fees"]').text()).toContain('0.25 EUR')
  })

  it('narrows the page by the search text', async () => {
    stubRoutes({ 'GET /api/transactions?': ledger })
    const wrapper = await mountLedger()

    await wrapper.get('[data-testid="billing-transactions-search"]').setValue('refund')

    expect(wrapper.findAll('[data-testid="billing-transactions-table"] tbody tr')).toHaveLength(1)
  })

  it('narrows the page by the direction in the address and shows it as a chip', async () => {
    stubRoutes({ 'GET /api/transactions?': ledger })
    const { wrapper } = await mountRouted(TransactionsListView, '/billing/transactions', '/billing/transactions?direction=out')
    await flushPromises()

    expect(wrapper.findAll('[data-testid="billing-transactions-table"] tbody tr')).toHaveLength(1)
    expect(wrapper.find('[data-testid="filter-chip-direction"]').exists()).toBe(true)
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
    expect(wrapper.get('[data-testid="billing-transactions-feedback"]').text()).toBe('Record deleted.')
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

    expect(wrapper.get('[data-testid="billing-transactions-feedback"]').text()).toBe('Transaction is settled.')
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

  it('hands the revenue chart its loading state and draws a bar for the count while the first load is pending', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const wrapper = await mountLedger()

    expect(wrapper.get('[data-testid="billing-transaction-chart"]').find('[data-testid="chart-skeleton"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="billing-transactions-count"]').text()).toBe('')
  })
})
