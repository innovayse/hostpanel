/**
 * Mount tests for InvoicePaymentTab: the notices that replace the form for a draft or cancelled
 * invoice, the submit guard, and recording a payment through the store.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import InvoicePaymentTab from '@/components/billing/InvoicePaymentTab.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import { invoiceFixture } from '../../helpers/billingFixtures'
import { json, problem } from '../../helpers/fetch'
import { mountWithApp } from '../../helpers/mount'
import { stubRoutes } from '../../helpers/routes'
import { useToastStore } from '@/stores/toast'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('InvoicePaymentTab', () => {
  it('replaces the form with a notice for a draft', () => {
    const wrapper = mountWithApp(InvoicePaymentTab, { props: { invoice: invoiceFixture({ status: 'Draft' }) } })

    expect(wrapper.find('[data-testid="billing-invoice-payment-draft"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="billing-invoice-payment-form"]').exists()).toBe(false)
  })

  it('replaces the form with a warning for a cancelled invoice', () => {
    const wrapper = mountWithApp(InvoicePaymentTab, { props: { invoice: invoiceFixture({ status: 'Cancelled' }) } })

    expect(wrapper.find('[data-testid="billing-invoice-payment-cancelled"]').exists()).toBe(true)
  })

  it('keeps submit disabled until a date and an amount are set', async () => {
    const wrapper = mountWithApp(InvoicePaymentTab, { props: { invoice: invoiceFixture() } })
    expect(wrapper.get('[data-testid="billing-invoice-payment-submit"]').attributes('disabled')).toBeDefined()

    wrapper.findComponent(UiDatePicker).vm.$emit('update:modelValue', '2026-10-20')
    await wrapper.get('[data-testid="billing-invoice-payment-amount"] input').setValue('25')

    expect(wrapper.get('[data-testid="billing-invoice-payment-submit"]').attributes('disabled')).toBeUndefined()
  })

  it('records the payment and confirms it under the form', async () => {
    const mock = stubRoutes({
      'POST /api/billing/1/payment': () => new Response(null, { status: 204 }),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const wrapper = mountWithApp(InvoicePaymentTab, { props: { invoice: invoiceFixture() } })

    wrapper.findComponent(UiDatePicker).vm.$emit('update:modelValue', '2026-10-20')
    await wrapper.get('[data-testid="billing-invoice-payment-amount"] input').setValue('25')
    await wrapper.get('[data-testid="billing-invoice-payment-form"]').trigger('submit')
    await flushPromises()

    const post = mock.mock.calls.find(([url]) => String(url).endsWith('/billing/1/payment'))
    expect(JSON.parse(String((post?.[1] as RequestInit | undefined)?.body))).toMatchObject({ amount: 25, fees: 0 })
    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Payment recorded.'])
  })

  it('shows the API sentence under the form when the payment is refused', async () => {
    stubRoutes({ 'POST /api/billing/1/payment': () => problem('The amount exceeds the balance.', 409) })
    const wrapper = mountWithApp(InvoicePaymentTab, { props: { invoice: invoiceFixture() } })

    wrapper.findComponent(UiDatePicker).vm.$emit('update:modelValue', '2026-10-20')
    await wrapper.get('[data-testid="billing-invoice-payment-amount"] input').setValue('25')
    await wrapper.get('[data-testid="billing-invoice-payment-form"]').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-invoice-payment-feedback"]').text()).toBe('The amount exceeds the balance.')
  })
})
