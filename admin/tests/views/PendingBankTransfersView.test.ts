/**
 * Mount tests of PendingBankTransfersView: it lists the waiting transfers with their reference and amount, shows the empty
 * state, asks before confirming, rejects only with a note and sends it, reports the outcome and reloads, and on a refused load
 * shows the API sentence with a Retry.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import PendingBankTransfersView from '@/views/PendingBankTransfersView.vue'
import { mountRoutedSettled } from '../helpers/mount'
import { json, problem } from '../helpers/fetch'
import { stubRoutes } from '../helpers/routes'

afterEach(() => {
  vi.unstubAllGlobals()
})

/** One waiting transfer as the list endpoint returns it. */
const transfer = {
  invoiceId: 5,
  reference: 'INV-5',
  clientId: 9,
  clientName: 'Ada Lovelace',
  total: 100,
  amountDue: 100,
  currency: 'USD',
  dueDate: '2026-10-30T00:00:00Z',
  requestedAt: '2026-10-14T09:00:00Z',
}

/** A page with the one waiting transfer. */
const page = { items: [transfer], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 }

/** An empty page. */
const emptyPage = { items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 }

/**
 * Reads the verbs and paths of every request the mock saw.
 *
 * @param mock - The fetch mock.
 * @returns The request lines, in order.
 */
const lines = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url, init]) => `${(init as RequestInit | undefined)?.method ?? 'GET'} ${url as string}`)

describe('PendingBankTransfersView', () => {
  it('renders the waiting transfer with its reference, amount and links', async () => {
    stubRoutes({ 'GET /api/billing/bank-transfers/pending': () => json(page) })
    const wrapper = await mountRoutedSettled(PendingBankTransfersView, '/billing/bank-transfers')

    const table = wrapper.get('[data-testid="billing-bank-transfers-table"]').text()
    expect(table).toContain('Ada Lovelace')
    expect(table).toContain('INV-5')
    expect(table).toContain('$100.00')
    expect(wrapper.get('[data-testid="billing-bank-transfer-invoice-5"]').attributes('href')).toBe('/billing/5')
    expect(wrapper.get('[data-testid="billing-bank-transfer-client-5"]').attributes('href')).toBe('/clients/9')
    expect(wrapper.get('[data-testid="billing-bank-transfers-count"]').text()).toContain('1 Records Found')
  })

  it('shows the empty state when nothing waits', async () => {
    stubRoutes({ 'GET /api/billing/bank-transfers/pending': () => json(emptyPage) })
    const wrapper = await mountRoutedSettled(PendingBankTransfersView, '/billing/bank-transfers')

    expect(wrapper.get('[data-testid="billing-bank-transfers-table"]').text()).toContain('No bank transfers are waiting.')
  })

  it('asks before confirming, then confirms and reloads', async () => {
    const mock = stubRoutes({
      'POST /api/billing/5/bank-transfer/confirm': () => new Response(null, { status: 204 }),
      'GET /api/billing/bank-transfers/pending': () => json(page),
    })
    const wrapper = await mountRoutedSettled(PendingBankTransfersView, '/billing/bank-transfers')

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="billing-bank-transfer-actions-5"]').trigger('click')
    await wrapper.get('[data-testid="billing-bank-transfer-confirm-5"]').trigger('click')
    expect(wrapper.find('[data-testid="billing-bank-transfers-confirm-dialog"]').exists()).toBe(true)
    expect(lines(mock).some((line) => line.startsWith('POST'))).toBe(false)

    await wrapper.get('[data-testid="billing-bank-transfers-confirm-dialog"] [data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(lines(mock)).toContain('POST /api/billing/5/bank-transfer/confirm')
    expect(lines(mock).filter((line) => line.startsWith('GET /api/billing/bank-transfers/pending')).length).toBe(2)
    expect(wrapper.get('[data-testid="billing-bank-transfers-feedback"]').text()).toBe('Invoice #5 is paid.')
  })

  it('does not confirm when the prompt is cancelled', async () => {
    const mock = stubRoutes({ 'GET /api/billing/bank-transfers/pending': () => json(page) })
    const wrapper = await mountRoutedSettled(PendingBankTransfersView, '/billing/bank-transfers')

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="billing-bank-transfer-actions-5"]').trigger('click')
    await wrapper.get('[data-testid="billing-bank-transfer-confirm-5"]').trigger('click')
    await wrapper.get('[data-testid="billing-bank-transfers-confirm-dialog"] [data-testid="dialog-cancel"]').trigger('click')

    expect(wrapper.find('[data-testid="billing-bank-transfers-confirm-dialog"]').exists()).toBe(false)
    expect(lines(mock).some((line) => line.startsWith('POST'))).toBe(false)
  })

  it('refuses to reject without a note and sends nothing', async () => {
    const mock = stubRoutes({ 'GET /api/billing/bank-transfers/pending': () => json(page) })
    const wrapper = await mountRoutedSettled(PendingBankTransfersView, '/billing/bank-transfers')

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="billing-bank-transfer-actions-5"]').trigger('click')
    await wrapper.get('[data-testid="billing-bank-transfer-reject-5"]').trigger('click')
    await wrapper.get('[data-testid="billing-bank-transfers-reject-dialog"] [data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-bank-transfers-note-error"]').text()).toContain('Write a note for the client.')
    expect(lines(mock).some((line) => line.startsWith('POST'))).toBe(false)
  })

  it('rejects with the typed note and reloads', async () => {
    const mock = stubRoutes({
      'POST /api/billing/5/bank-transfer/reject': () => new Response(null, { status: 204 }),
      'GET /api/billing/bank-transfers/pending': () => json(page),
    })
    const wrapper = await mountRoutedSettled(PendingBankTransfersView, '/billing/bank-transfers')

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="billing-bank-transfer-actions-5"]').trigger('click')
    await wrapper.get('[data-testid="billing-bank-transfer-reject-5"]').trigger('click')
    await wrapper.get('[data-testid="billing-bank-transfers-note"]').setValue('  Amount too low  ')
    await wrapper.get('[data-testid="billing-bank-transfers-reject-dialog"] [data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    const post = mock.mock.calls.find(([url]) => (url as string).endsWith('/reject'))
    const init = post?.[1] as RequestInit | undefined
    expect(JSON.parse(String(init?.body))).toEqual({ note: 'Amount too low' })
    expect(wrapper.get('[data-testid="billing-bank-transfers-feedback"]').text()).toBe('Invoice #5 is open again and the client was told why.')
  })

  it('shows the API sentence when the decision is refused and still reloads the list', async () => {
    const mock = stubRoutes({
      'POST /api/billing/5/bank-transfer/confirm': () => problem('No bank transfer is waiting on this invoice.', 409),
      'GET /api/billing/bank-transfers/pending': () => json(page),
    })
    const wrapper = await mountRoutedSettled(PendingBankTransfersView, '/billing/bank-transfers')

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="billing-bank-transfer-actions-5"]').trigger('click')
    await wrapper.get('[data-testid="billing-bank-transfer-confirm-5"]').trigger('click')
    await wrapper.get('[data-testid="billing-bank-transfers-confirm-dialog"] [data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-bank-transfers-feedback"]').text()).toBe('No bank transfer is waiting on this invoice.')
    expect(lines(mock).filter((line) => line.startsWith('GET /api/billing/bank-transfers/pending')).length).toBe(2)
  })

  it('shows the refusal sentence of a refused load and loads again from Retry', async () => {
    const mock = stubRoutes({ 'GET /api/billing/bank-transfers/pending': () => problem('Not allowed.', 403) })
    const wrapper = await mountRoutedSettled(PendingBankTransfersView, '/billing/bank-transfers')

    expect(wrapper.get('[data-testid="billing-bank-transfers-error"]').text()).toContain('Not allowed.')
    expect(wrapper.find('[data-testid="billing-bank-transfers-count"]').exists()).toBe(false)
    const before = mock.mock.calls.length

    await wrapper.get('[data-testid="billing-bank-transfers-error-retry"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.length).toBeGreaterThan(before)
  })
})
