/**
 * Mount tests for InvoiceSummaryTab: the invoice facts, the action buttons that depend on the
 * status, the read-only mode, the confirmation of cancel and mark paid, and the outcome of each
 * action shown next to the buttons.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import InvoiceSummaryTab from '@/components/billing/InvoiceSummaryTab.vue'
import { invoiceFixture } from '../../helpers/billingFixtures'
import { json, problem } from '../../helpers/fetch'
import { mountWithApp } from '../../helpers/mount'
import { stubRoutes } from '../../helpers/routes'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('InvoiceSummaryTab', () => {
  it('shows the client, the status and the balance the API reports as still owed', () => {
    const wrapper = mountWithApp(InvoiceSummaryTab, { props: { invoice: invoiceFixture() } })

    expect(wrapper.get('[data-testid="billing-invoice-client-name"]').text()).toBe('Ada Lovelace')
    expect(wrapper.get('[data-testid="billing-invoice-status"]').text()).toBe('Unpaid')
    expect(wrapper.get('[data-testid="billing-invoice-fact-balance"]').text()).toContain('60.00 USD')
  })

  it('keeps the invoice currency on the summary values even when the invoice is in AMD', () => {
    const wrapper = mountWithApp(InvoiceSummaryTab, {
      props: {
        invoice: invoiceFixture({
          currency: 'AMD',
          amounts: { ...invoiceFixture().amounts, total: 24000, credit: 0 },
          amountDue: 24000,
        }),
      },
    })

    expect(wrapper.get('[data-testid="billing-invoice-fact-amount"]').text()).toContain('24000.00 AMD')
    expect(wrapper.get('[data-testid="billing-invoice-fact-balance"]').text()).toContain('24000.00 AMD')
  })

  it('shows no balance for a paid invoice settled without a payment row of its own', () => {
    const wrapper = mountWithApp(InvoiceSummaryTab, { props: { invoice: invoiceFixture({ status: 'Paid', amountDue: 0, transactions: [] }) } })

    expect(wrapper.get('[data-testid="billing-invoice-fact-balance"]').text()).toContain('0.00 USD')
    expect(wrapper.get('[data-testid="billing-invoice-fact-balance"]').text()).not.toContain('60.00')
  })

  it('takes the balance from the API rather than from the payment rows', () => {
    // The payment rows (40 of 100) would give 60; the API's figure wins.
    const wrapper = mountWithApp(InvoiceSummaryTab, { props: { invoice: invoiceFixture({ amountDue: 25 }) } })

    expect(wrapper.get('[data-testid="billing-invoice-fact-balance"]').text()).toContain('25.00 USD')
  })

  it('offers publish for a draft and mark paid for an unpaid invoice', () => {
    const draft = mountWithApp(InvoiceSummaryTab, { props: { invoice: invoiceFixture({ status: 'Draft' }) } })
    expect(draft.find('[data-testid="billing-invoice-publish"]').exists()).toBe(true)
    expect(draft.find('[data-testid="billing-invoice-mark-paid"]').exists()).toBe(false)
    expect(draft.find('[data-testid="billing-invoice-draft-notice"]').exists()).toBe(true)

    const unpaid = mountWithApp(InvoiceSummaryTab, { props: { invoice: invoiceFixture() } })
    expect(unpaid.find('[data-testid="billing-invoice-mark-paid"]').exists()).toBe(true)
  })

  it('hides every action when read-only', () => {
    const wrapper = mountWithApp(InvoiceSummaryTab, { props: { invoice: invoiceFixture(), readonly: true } })

    expect(wrapper.find('[data-testid="billing-invoice-mark-paid"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="billing-invoice-cancel"]').exists()).toBe(false)
  })

  it('marks the invoice paid through the bulk endpoint after confirmation and says so', async () => {
    const mock = stubRoutes({
      'POST /api/billing/bulk': () => new Response(null, { status: 204 }),
      'GET /api/billing/1': () => json(invoiceFixture({ status: 'Paid' })),
    })
    const wrapper = mountWithApp(InvoiceSummaryTab, { props: { invoice: invoiceFixture() } })

    await wrapper.get('[data-testid="billing-invoice-mark-paid"]').trigger('click')
    expect(mock.mock.calls.some(([url]) => String(url).endsWith('/billing/bulk'))).toBe(false)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    const post = mock.mock.calls.find(([url]) => String(url).endsWith('/billing/bulk'))
    expect(JSON.parse(String((post?.[1] as RequestInit | undefined)?.body))).toEqual({ invoiceIds: [1], action: 'MarkPaid' })
    expect(wrapper.get('[data-testid="billing-invoice-summary-feedback"]').text()).toContain('Invoice marked as paid.')
  })

  it('cancels only after the danger confirmation and does nothing when dismissed', async () => {
    const mock = stubRoutes({
      'POST /api/billing/1/cancel': () => new Response(null, { status: 204 }),
      'GET /api/billing/1': () => json(invoiceFixture({ status: 'Cancelled' })),
    })
    const wrapper = mountWithApp(InvoiceSummaryTab, { props: { invoice: invoiceFixture() } })

    await wrapper.get('[data-testid="billing-invoice-cancel"]').trigger('click')
    expect(wrapper.get('[data-testid="dialog-confirm"]').classes().join(' ')).toContain('status-red')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()
    expect(mock.mock.calls.some(([url]) => String(url).endsWith('/cancel'))).toBe(false)

    await wrapper.get('[data-testid="billing-invoice-cancel"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([url]) => String(url).endsWith('/billing/1/cancel'))).toBe(true)
    expect(wrapper.get('[data-testid="billing-invoice-summary-feedback"]').text()).toContain('Invoice cancelled.')
  })

  it('publishes a draft without a prompt and says so', async () => {
    stubRoutes({
      'POST /api/billing/1/publish': () => new Response(null, { status: 204 }),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const wrapper = mountWithApp(InvoiceSummaryTab, { props: { invoice: invoiceFixture({ status: 'Draft' }) } })

    await wrapper.get('[data-testid="billing-invoice-publish"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-invoice-summary-feedback"]').text()).toContain('Invoice published.')
  })

  it('shows the API sentence next to the buttons when the action is refused', async () => {
    stubRoutes({ 'POST /api/billing/1/cancel': () => problem('Cannot cancel.', 409) })
    const wrapper = mountWithApp(InvoiceSummaryTab, { props: { invoice: invoiceFixture() } })

    await wrapper.get('[data-testid="billing-invoice-cancel"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-invoice-summary-feedback"]').text()).toContain('Cannot cancel.')
  })
})
