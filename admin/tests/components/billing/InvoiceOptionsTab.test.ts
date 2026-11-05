/**
 * Mount tests for InvoiceOptionsTab: the draft notice, the form filled from the invoice, and the
 * save that sends the tax rate, with its outcome shown under the form.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import InvoiceOptionsTab from '@/components/billing/InvoiceOptionsTab.vue'
import { invoiceFixture } from '../../helpers/billingFixtures'
import { json, problem } from '../../helpers/fetch'
import { mountWithApp } from '../../helpers/mount'
import { stubRoutes } from '../../helpers/routes'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('InvoiceOptionsTab', () => {
  it('shows the draft notice only for a draft invoice', () => {
    const draft = mountWithApp(InvoiceOptionsTab, { props: { invoice: invoiceFixture({ status: 'Draft' }) } })
    const unpaid = mountWithApp(InvoiceOptionsTab, { props: { invoice: invoiceFixture() } })

    expect(draft.find('[data-testid="billing-invoice-options-draft"]').exists()).toBe(true)
    expect(unpaid.find('[data-testid="billing-invoice-options-draft"]').exists()).toBe(false)
  })

  it('offers forward-looking quick dates for the due date, not past ones', async () => {
    const wrapper = mountWithApp(InvoiceOptionsTab, { props: { invoice: invoiceFixture() } })
    const dueDate = wrapper.findAll('[data-testid="date-picker-input"]')[1]!

    await dueDate.trigger('click')

    const quick = wrapper.findAll('[data-testid^="date-picker-quick-"]').map(button => button.text())
    expect(quick.join('|')).toMatch(/Tomorrow/)
    expect(quick.join('|')).not.toMatch(/Ago/)
  })

  it('saves the dates, the payment method and the tax rate', async () => {
    const mock = stubRoutes({
      'PUT /api/billing/1/options': () => new Response(null, { status: 204 }),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const wrapper = mountWithApp(InvoiceOptionsTab, { props: { invoice: invoiceFixture() } })

    await wrapper.get('[data-testid="billing-invoice-tax"] input').setValue('15')
    await wrapper.get('[data-testid="billing-invoice-options-form"]').trigger('submit')
    await flushPromises()

    const put = mock.mock.calls.find(([, init]) => (init as RequestInit | undefined)?.method === 'PUT')
    expect(JSON.parse(String((put?.[1] as RequestInit | undefined)?.body))).toEqual({
      invoiceDate: '2026-10-14',
      dueDate: '2026-12-31',
      paymentMethod: 'Example Gateway',
      taxRate: 15,
    })
    expect(wrapper.get('[data-testid="billing-invoice-options-feedback"]').text()).toBe('Changes saved successfully.')
  })

  it('shows the API sentence under the form when the save is refused', async () => {
    stubRoutes({ 'PUT /api/billing/1/options': () => problem('The due date is before the invoice date.', 400) })
    const wrapper = mountWithApp(InvoiceOptionsTab, { props: { invoice: invoiceFixture() } })

    await wrapper.get('[data-testid="billing-invoice-options-form"]').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-invoice-options-feedback"]').text())
      .toBe('The due date is before the invoice date.')
  })
})
