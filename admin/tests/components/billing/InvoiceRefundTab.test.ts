/**
 * Mount tests for InvoiceRefundTab: the notices that replace the form, the credit question that
 * gates the refund button, the confirmation naming amount and method, sending a refund through
 * the store, and the outcome shown in the tab.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import InvoiceRefundTab from '@/components/billing/InvoiceRefundTab.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { invoiceFixture } from '../../helpers/billingFixtures'
import { json, problem } from '../../helpers/fetch'
import { mountWithApp } from '../../helpers/mount'
import { stubRoutes } from '../../helpers/routes'
import { InvoiceRefundType, InvoiceStatus } from '@/types/invoice'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('InvoiceRefundTab', () => {
  it('replaces the form with a notice for a draft, a cancelled and a refunded invoice', () => {
    const draft = mountWithApp(InvoiceRefundTab, { props: { invoice: invoiceFixture({ status: 'Draft' }) } })
    const cancelled = mountWithApp(InvoiceRefundTab, { props: { invoice: invoiceFixture({ status: 'Cancelled' }) } })
    const refunded = mountWithApp(InvoiceRefundTab, { props: { invoice: invoiceFixture({ status: 'Refunded' }) } })

    expect(draft.find('[data-testid="billing-invoice-refund-draft"]').exists()).toBe(true)
    expect(cancelled.find('[data-testid="billing-invoice-refund-cancelled"]').exists()).toBe(true)
    expect(refunded.find('[data-testid="billing-invoice-refund-refunded"]').exists()).toBe(true)
    expect(draft.find('[data-testid="billing-invoice-refund-form"]').exists()).toBe(false)
  })

  it('keeps the form for a partially refunded invoice, which can still be refunded up to what is left', () => {
    const wrapper = mountWithApp(InvoiceRefundTab, {
      props: { invoice: invoiceFixture({ status: InvoiceStatus.PartiallyRefunded }) },
    })

    expect(wrapper.find('[data-testid="billing-invoice-refund-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="billing-invoice-refund-refunded"]').exists()).toBe(false)
  })

  it('warns read-only when no payment can be refunded', () => {
    const wrapper = mountWithApp(InvoiceRefundTab, {
      props: { invoice: invoiceFixture({ transactions: [] }), readonly: true },
    })

    expect(wrapper.find('[data-testid="billing-invoice-refund-none"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="billing-invoice-refund-form"]').exists()).toBe(false)
  })

  it('asks what to do with the credit and enables refund only once answered', async () => {
    const wrapper = mountWithApp(InvoiceRefundTab, { props: { invoice: invoiceFixture() } })
    expect(wrapper.find('[data-testid="billing-invoice-refund-credit"]').exists()).toBe(false)

    wrapper.findAllComponents(UiSelect)[0]?.vm.$emit('update:modelValue', '21')
    await flushPromises()
    expect(wrapper.find('[data-testid="billing-invoice-refund-credit"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="billing-invoice-refund-submit"]').attributes('disabled')).toBeDefined()

    await wrapper.get('[data-testid="billing-invoice-refund-credit-leave"]').setValue(true)
    expect(wrapper.get('[data-testid="billing-invoice-refund-submit"]').attributes('disabled')).toBeUndefined()
  })

  it('sends a gateway refund capped at the refundable amount, without transaction id or gateway', async () => {
    const mock = stubRoutes({
      'POST /api/billing/1/refund': () => new Response(null, { status: 204 }),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const wrapper = mountWithApp(InvoiceRefundTab, { props: { invoice: invoiceFixture() } })

    wrapper.findAllComponents(UiSelect)[0]?.vm.$emit('update:modelValue', '21')
    await flushPromises()
    await wrapper.get('[data-testid="billing-invoice-refund-credit-leave"]').setValue(true)
    await wrapper.get('[data-testid="billing-invoice-refund-form"]').trigger('submit')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    const post = mock.mock.calls.find(([url]) => String(url).endsWith('/billing/1/refund'))
    // The 40 payment less the 10 already refunded leaves 30 refundable.
    expect(JSON.parse(String((post?.[1] as RequestInit | undefined)?.body))).toEqual({
      amount: 30,
      refundType: InvoiceRefundType.Gateway,
    })
    expect(wrapper.get('[data-testid="billing-invoice-refund-feedback"]').text()).toContain('Refund sent.')
  })

  it('asks for confirmation naming the amount and the method before sending anything', async () => {
    const mock = stubRoutes({ 'GET /api/billing/1': () => json(invoiceFixture()) })
    const wrapper = mountWithApp(InvoiceRefundTab, { props: { invoice: invoiceFixture() } })

    wrapper.findAllComponents(UiSelect)[0]?.vm.$emit('update:modelValue', '21')
    await flushPromises()
    await wrapper.get('[data-testid="billing-invoice-refund-credit-leave"]').setValue(true)
    await wrapper.get('[data-testid="billing-invoice-refund-form"]').trigger('submit')
    await flushPromises()

    const body = wrapper.get('[data-testid="dialog-body"]').text()
    expect(body).toContain('30.00 USD')
    expect(body).toContain('Refund through Gateway')
    expect(mock.mock.calls.some(([url]) => String(url).endsWith('/refund'))).toBe(false)

    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="dialog-confirm"]').exists()).toBe(false)
  })

  it('shows the API sentence in the tab when the refund is refused', async () => {
    stubRoutes({ 'POST /api/billing/1/refund': () => problem('The gateway declined the refund.', 409) })
    const wrapper = mountWithApp(InvoiceRefundTab, { props: { invoice: invoiceFixture() } })

    wrapper.findAllComponents(UiSelect)[0]?.vm.$emit('update:modelValue', '21')
    await flushPromises()
    await wrapper.get('[data-testid="billing-invoice-refund-credit-leave"]').setValue(true)
    await wrapper.get('[data-testid="billing-invoice-refund-form"]').trigger('submit')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-invoice-refund-feedback"]').text()).toContain('The gateway declined the refund.')
    expect(wrapper.find('[data-testid="dialog-confirm"]').exists()).toBe(false)
  })

  it('sends the payment gateway and the reference only for a manual refund', async () => {
    const mock = stubRoutes({
      'POST /api/billing/1/refund': () => new Response(null, { status: 204 }),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const wrapper = mountWithApp(InvoiceRefundTab, { props: { invoice: invoiceFixture() } })

    wrapper.findAllComponents(UiSelect)[0]?.vm.$emit('update:modelValue', '21')
    await flushPromises()
    wrapper.findAllComponents(UiSelect)[1]?.vm.$emit('update:modelValue', InvoiceRefundType.Manual)
    await flushPromises()
    await wrapper.get('[data-testid="billing-invoice-refund-reference"]').setValue('EXT-1')
    await wrapper.get('[data-testid="billing-invoice-refund-credit-leave"]').setValue(true)
    await wrapper.get('[data-testid="billing-invoice-refund-form"]').trigger('submit')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    const post = mock.mock.calls.find(([url]) => String(url).endsWith('/billing/1/refund'))
    expect(JSON.parse(String((post?.[1] as RequestInit | undefined)?.body))).toEqual({
      amount: 30,
      refundType: InvoiceRefundType.Manual,
      gateway: 'Stripe',
      refundTransactionId: 'EXT-1',
    })
  })
})
