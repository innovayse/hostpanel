/**
 * Mount tests for InvoiceCreditNotesTab: the guards on the issue button (unpaid invoice, credit top-up, nothing left to credit), the
 * notes of the invoice listed with their state, and issuing a note through the store with the outcome shown in the tab.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import InvoiceCreditNotesTab from '@/components/billing/InvoiceCreditNotesTab.vue'
import { InvoiceStatus } from '@/types/invoice'
import { creditNoteFixture, creditNotePage } from '../../helpers/creditNoteFixtures'
import { invoiceFixture } from '../../helpers/billingFixtures'
import { json, problem } from '../../helpers/fetch'
import { mountWithApp } from '../../helpers/mount'
import { stubRoutes } from '../../helpers/routes'

afterEach(() => {
  vi.unstubAllGlobals()
})

/**
 * Builds a paid invoice that can still be credited.
 *
 * @param overrides - Fields that differ.
 * @returns The invoice.
 */
const paid = (overrides: Parameters<typeof invoiceFixture>[0] = {}) =>
  invoiceFixture({ status: InvoiceStatus.Paid, creditableAmount: 100, ...overrides })

/**
 * Mounts the tab and lets its first load settle.
 *
 * @param invoice - The invoice.
 * @returns The wrapper.
 */
const mountTab = async (invoice = paid()) => {
  const wrapper = mountWithApp(InvoiceCreditNotesTab, { props: { invoice } })
  await flushPromises()
  return wrapper
}

describe('InvoiceCreditNotesTab', () => {
  it('lists the notes of the invoice with their number and state, and what can still be credited', async () => {
    const mock = stubRoutes({ 'GET /api/billing/credit-notes': () => json(creditNotePage([creditNoteFixture()])) })
    const wrapper = await mountTab()

    expect(wrapper.get('[data-testid="billing-invoice-credit-note-3"]').text()).toBe('CN-0003')
    expect(wrapper.get('[data-testid="billing-invoice-creditable"]').text()).toContain('100.00')
    expect(String(mock.mock.calls[0]?.[0])).toContain('invoiceId=1')
  })

  it('says a credit note is not invoice credit', async () => {
    stubRoutes({ 'GET /api/billing/credit-notes': () => json(creditNotePage([])) })
    const wrapper = await mountTab()

    expect(wrapper.get('[data-testid="billing-invoice-credit-notes-explain"]').text()).toContain('not invoice credit')
  })

  it.each([
    ['an unpaid invoice', { status: InvoiceStatus.Unpaid }, 'only be issued on an invoice that was paid'],
    ['a credit top-up', { isCreditTopUp: true }, 'credit already'],
    ['an invoice with nothing left', { creditableAmount: 0 }, 'already cover the whole invoice'],
  ])('keeps the issue button off for %s and says why', async (_name, overrides, sentence) => {
    stubRoutes({ 'GET /api/billing/credit-notes': () => json(creditNotePage([])) })
    const wrapper = await mountTab(paid(overrides))

    expect(wrapper.get('[data-testid="billing-invoice-credit-note-issue"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="billing-invoice-credit-note-blocked"]').text()).toContain(sentence)
  })

  it('issues a note, closes the form, confirms in the tab and reloads the notes and the invoice', async () => {
    const mock = stubRoutes({
      'POST /api/billing/1/credit-notes': () => json(3, 201),
      'GET /api/billing/credit-notes': () => json(creditNotePage([creditNoteFixture()])),
      'GET /api/billing/1': () => json(paid()),
    })
    const wrapper = await mountTab()

    await wrapper.get('[data-testid="billing-invoice-credit-note-issue"]').trigger('click')
    await wrapper.get('[data-testid="billing-credit-note-line-0-description"]').setValue('Outage')
    await wrapper.get('[data-testid="billing-credit-note-line-0-price"] input').setValue('40')
    await wrapper.get('[data-testid="billing-credit-note-reason"]').setValue('Two days down')
    await wrapper.get('[data-testid="billing-credit-note-submit"]').trigger('click')
    await flushPromises()

    const post = mock.mock.calls.find(([url]) => String(url).endsWith('/billing/1/credit-notes'))
    expect(JSON.parse(String((post?.[1] as RequestInit | undefined)?.body))).toEqual({
      items: [{ description: 'Outage', unitPrice: 40, quantity: 1 }],
      method: 'AccountCredit',
      reason: 'Two days down',
    })
    expect(wrapper.get('[data-testid="billing-invoice-credit-notes-feedback"]').text()).toBe('The credit note was issued.')
    expect(wrapper.find('[data-testid="billing-credit-note-dialog"]').exists()).toBe(false)
    expect(mock.mock.calls.some(([url]) => String(url) === '/api/billing/1')).toBe(true)
  })

  it('keeps the form open with the API sentence when the note is refused', async () => {
    stubRoutes({
      'POST /api/billing/1/credit-notes': () => problem('The credit note is more than this invoice can still be credited.', 400),
      'GET /api/billing/credit-notes': () => json(creditNotePage([])),
    })
    const wrapper = await mountTab()

    await wrapper.get('[data-testid="billing-invoice-credit-note-issue"]').trigger('click')
    await wrapper.get('[data-testid="billing-credit-note-line-0-description"]').setValue('Outage')
    await wrapper.get('[data-testid="billing-credit-note-line-0-price"] input').setValue('40')
    await wrapper.get('[data-testid="billing-credit-note-reason"]').setValue('Two days down')
    await wrapper.get('[data-testid="billing-credit-note-submit"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-credit-note-dialog-error"]').text()).toBe('The credit note is more than this invoice can still be credited.')
  })

  it('shows the notes but no issue button on a read-only tab', async () => {
    stubRoutes({ 'GET /api/billing/credit-notes': () => json(creditNotePage([creditNoteFixture()])) })
    const wrapper = mountWithApp(InvoiceCreditNotesTab, { props: { invoice: paid(), readonly: true } })
    await flushPromises()

    expect(wrapper.find('[data-testid="billing-invoice-credit-note-issue"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="billing-invoice-credit-note-3"]').exists()).toBe(true)
  })
})
