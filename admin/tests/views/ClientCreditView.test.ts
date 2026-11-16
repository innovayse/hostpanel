/**
 * Mount tests of ClientCreditView, the Credit tab of a client: it shows the balance and the history with each line's type, reason,
 * signed change and balance, adds or deducts credit only with an amount and a reason and sends them, reports the outcome and
 * reloads, and on a refused load shows the API sentence with a Retry.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import ClientCreditView from '@/views/ClientCreditView.vue'
import { CreditMovement } from '@/types/creditmovement'
import { mountRouted } from '../helpers/mount'
import { callOf, json, problem } from '../helpers/fetch'
import { stubRoutes } from '../helpers/routes'

afterEach(() => {
  vi.unstubAllGlobals()
})

/**
 * Mounts the tab for client 9 under its own route pattern, so the view reads the client id from the path, and waits for its first load.
 *
 * @returns The wrapper.
 */
const mountCredit = async () => {
  const { wrapper } = await mountRouted(ClientCreditView, '/clients/:id/credit', '/clients/9/credit')
  await flushPromises()
  return wrapper
}

/** A credit with two history lines, newest first. */
const credit = {
  clientId: 9,
  balance: 25,
  currency: 'USD',
  entries: {
    items: [
      { id: 12, date: '2026-10-02T09:00:00Z', movement: CreditMovement.InvoiceApplied, change: -15, balanceAfter: 25, invoiceId: 7, description: 'Credit applied to Invoice #7' },
      { id: 11, date: '2026-10-01T09:00:00Z', movement: CreditMovement.AdminAdd, change: 40, balanceAfter: 40, invoiceId: null, description: 'Goodwill after the outage' },
    ],
    pageNumber: 1,
    pageSize: 25,
    totalCount: 2,
    totalPages: 1,
  },
}

/**
 * Reads the verbs and paths of every request the mock saw.
 *
 * @param mock - The fetch mock.
 * @returns The request lines, in order.
 */
const lines = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url, init]) => `${(init as RequestInit | undefined)?.method ?? 'GET'} ${url as string}`)

describe('ClientCreditView', () => {
  it('shows the balance and the history with the reasons and the invoice links', async () => {
    stubRoutes({ 'GET /api/clients/9/credit?page=1&pageSize=25': () => json(credit) })
    const wrapper = await mountCredit()

    expect(wrapper.get('[data-testid="client-credit-balance"]').text()).toContain('25.00')
    const table = wrapper.get('[data-testid="client-credit-table"]').text()
    expect(table).toContain('Goodwill after the outage')
    expect(table).toContain('Applied to an invoice')
    expect(table).toContain('Credit added by staff')
    expect(wrapper.get('[data-testid="client-credit-change-12"]').text()).toContain('15.00')
    expect(wrapper.get('[data-testid="client-credit-change-11"]').text()).toContain('+')
    expect(wrapper.find('a[href="/billing/7"]').exists()).toBe(true)
  })

  it('says nothing is itemised yet when there are no lines', async () => {
    stubRoutes({
      'GET /api/clients/9/credit?page=1&pageSize=25': () => json({ ...credit, balance: 7, entries: { ...credit.entries, items: [], totalCount: 0, totalPages: 0 } }),
    })
    const wrapper = await mountCredit()

    expect(wrapper.find('[data-testid="client-credit-empty"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="client-credit-balance"]').text()).toContain('7.00')
  })

  it('does not send an adjustment without an amount and a reason', async () => {
    const mock = stubRoutes({ 'GET /api/clients/9/credit?page=1&pageSize=25': () => json(credit) })
    const wrapper = await mountCredit()

    expect(wrapper.get('[data-testid="client-credit-submit"]').attributes('disabled')).toBeDefined()
    await wrapper.get('[data-testid="client-credit-amount"] input').setValue('5')
    expect(wrapper.get('[data-testid="client-credit-submit"]').attributes('disabled')).toBeDefined()
    await wrapper.get('[data-testid="client-credit-reason"]').setValue('   ')
    expect(wrapper.get('[data-testid="client-credit-submit"]').attributes('disabled')).toBeDefined()

    expect(lines(mock).some((line) => line.startsWith('POST'))).toBe(false)
  })

  it('sends the adjustment with its reason, reports it and reloads the first page', async () => {
    const mock = stubRoutes({
      'POST /api/clients/9/credit/adjustments': () => new Response(null, { status: 204 }),
      'GET /api/clients/9/credit?page=1&pageSize=25': () => json(credit),
    })
    const wrapper = await mountCredit()

    await wrapper.get('[data-testid="client-credit-amount"] input').setValue('12.5')
    await wrapper.get('[data-testid="client-credit-reason"]').setValue('Promo')
    await wrapper.get('[data-testid="client-credit-form"]').trigger('submit')
    await flushPromises()

    const posted = mock.mock.calls.findIndex(([url, init]) => String(url).endsWith('/adjustments') && (init as RequestInit).method === 'POST')
    expect(posted).toBeGreaterThanOrEqual(0)
    expect(JSON.parse(String(callOf(mock, posted).init?.body))).toEqual({ direction: 'Add', amount: 12.5, reason: 'Promo' })
    expect(lines(mock).filter((line) => line.startsWith('GET /api/clients/9/credit')).length).toBe(2)
    expect(wrapper.get('[data-testid="client-credit-feedback"]').text()).toContain('adjusted')
  })

  it('shows the API sentence when the adjustment is refused', async () => {
    stubRoutes({
      'POST /api/clients/9/credit/adjustments': () => problem('There is not enough credit.', 400),
      'GET /api/clients/9/credit?page=1&pageSize=25': () => json(credit),
    })
    const wrapper = await mountCredit()

    await wrapper.get('[data-testid="client-credit-amount"] input').setValue('500')
    await wrapper.get('[data-testid="client-credit-reason"]').setValue('Too much')
    await wrapper.get('[data-testid="client-credit-form"]').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-credit-feedback"]').text()).toBe('There is not enough credit.')
  })

  it('keeps the loaded page and says why when a later page cannot be read', async () => {
    const paged = { ...credit, entries: { ...credit.entries, totalCount: 60, totalPages: 3 } }
    stubRoutes({
      'GET /api/clients/9/credit?page=1&pageSize=25': () => json(paged),
      'GET /api/clients/9/credit?page=2&pageSize=25': () => problem('The history could not be read.', 500),
    })
    const wrapper = await mountCredit()

    await wrapper.get('[data-testid="client-credit-pagination-next"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-credit-page-error"]').text()).toBe('The history could not be read.')
    expect(wrapper.find('[data-testid="client-credit-table"]').exists()).toBe(true)
  })

  it('shows the API sentence with a retry when the load is refused', async () => {
    stubRoutes({ 'GET /api/clients/9/credit?page=1&pageSize=25': () => problem('The client was not found.', 404) })
    const wrapper = await mountCredit()

    expect(wrapper.get('[data-testid="client-credit-error"]').text()).toContain('The client was not found.')
    expect(wrapper.find('[data-testid="client-credit-balance"]').exists()).toBe(false)
  })

  it('keeps the tiles and the cards in place with placeholders while the first load is pending', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const wrapper = await mountCredit()

    expect(wrapper.findAll('[data-testid="ui-stat-tile-skeleton"]')).toHaveLength(2)
    expect(wrapper.get('[data-testid="client-credit-form"]').findAll('[data-testid="ui-field-skeleton"]')).toHaveLength(3)
    expect(wrapper.get('[data-testid="client-credit-table"]').find('[data-testid="ui-table-skeleton"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="client-credit-submit"]').attributes('disabled')).toBeDefined()
  })
})
