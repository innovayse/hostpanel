/**
 * Mount tests for InvoiceCreditTab: the guards on the two credit buttons, applying credit through
 * the store, the confirmation before credit is taken back, and the outcome shown in the tab.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import InvoiceCreditTab from '@/components/billing/InvoiceCreditTab.vue'
import { invoiceFixture } from '../../helpers/billingFixtures'
import { json, problem } from '../../helpers/fetch'
import { mountWithApp } from '../../helpers/mount'
import { stubRoutes } from '../../helpers/routes'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('InvoiceCreditTab', () => {
  it('keeps both buttons disabled until an amount is typed', () => {
    const wrapper = mountWithApp(InvoiceCreditTab, { props: { invoice: invoiceFixture() } })

    expect(wrapper.get('[data-testid="billing-invoice-credit-add"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="billing-invoice-credit-remove"]').attributes('disabled')).toBeDefined()
  })

  it('shows the credit available under each box', () => {
    const wrapper = mountWithApp(InvoiceCreditTab, { props: { invoice: invoiceFixture() } })

    expect(wrapper.text()).toContain('5.00 USD available')
  })

  it('shows the credit available in the invoice currency when the invoice is in AMD', () => {
    const wrapper = mountWithApp(InvoiceCreditTab, {
      props: {
        invoice: invoiceFixture({
          currency: 'AMD',
          amounts: { ...invoiceFixture().amounts, credit: 1000 },
        }),
      },
    })

    expect(wrapper.text()).toContain('1000.00 AMD available')
    expect(wrapper.text()).not.toContain('$1000.00')
  })

  it('applies the credit and confirms it in the tab', async () => {
    const mock = stubRoutes({
      'POST /api/billing/1/credit': () => new Response(null, { status: 204 }),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const wrapper = mountWithApp(InvoiceCreditTab, { props: { invoice: invoiceFixture() } })

    await wrapper.get('[data-testid="billing-invoice-credit-add-amount"] input').setValue('20')
    await wrapper.get('[data-testid="billing-invoice-credit-add"]').trigger('click')
    await flushPromises()

    const post = mock.mock.calls.find(([url]) => String(url).endsWith('/billing/1/credit'))
    expect(JSON.parse(String((post?.[1] as RequestInit | undefined)?.body))).toEqual({ amount: 20 })
    expect(wrapper.get('[data-testid="billing-invoice-credit-feedback"]').text()).toBe('Credit applied to the invoice.')
  })

  it('takes credit back through the remove endpoint once confirmed', async () => {
    const mock = stubRoutes({
      'POST /api/billing/1/credit/remove': () => new Response(null, { status: 204 }),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const wrapper = mountWithApp(InvoiceCreditTab, { props: { invoice: invoiceFixture() } })

    await wrapper.get('[data-testid="billing-invoice-credit-remove-amount"] input').setValue('3')
    await wrapper.get('[data-testid="billing-invoice-credit-remove"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([url]) => String(url).endsWith('/billing/1/credit/remove'))).toBe(true)
    expect(wrapper.get('[data-testid="billing-invoice-credit-feedback"]').text()).toBe('Credit removed from the invoice.')
    expect(wrapper.find('[data-testid="dialog-confirm"]').exists()).toBe(false)
  })

  it('asks before taking credit back and sends nothing when the prompt is dismissed', async () => {
    const mock = stubRoutes({ 'GET /api/billing/1': () => json(invoiceFixture()) })
    const wrapper = mountWithApp(InvoiceCreditTab, { props: { invoice: invoiceFixture() } })

    await wrapper.get('[data-testid="billing-invoice-credit-remove-amount"] input').setValue('3')
    await wrapper.get('[data-testid="billing-invoice-credit-remove"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="dialog-body"]').text()).toContain('3.00 USD')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="dialog-confirm"]').exists()).toBe(false)
    expect(mock.mock.calls.some(([url]) => String(url).endsWith('/credit/remove'))).toBe(false)
  })

  it('shows the API sentence in the tab when the credit is refused', async () => {
    stubRoutes({ 'POST /api/billing/1/credit': () => problem('The client has no credit.', 409) })
    const wrapper = mountWithApp(InvoiceCreditTab, { props: { invoice: invoiceFixture() } })

    await wrapper.get('[data-testid="billing-invoice-credit-add-amount"] input').setValue('20')
    await wrapper.get('[data-testid="billing-invoice-credit-add"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-invoice-credit-feedback"]').text()).toBe('The client has no credit.')
  })
})
