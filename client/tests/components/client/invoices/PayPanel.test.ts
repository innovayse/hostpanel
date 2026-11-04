/**
 * Unit tests for PayPanel: which ways to pay are offered, the saved-card payment through the
 * store and the success and refusal states.
 */
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mountClient, stubClientRuntime } from '~/tests/helpers/mountClient'
import PayPanel from '~/components/client/invoices/PayPanel.vue'
import { useInvoicePaymentStore } from '~/stores/invoicepayment'
import { PaymentMethodType } from '~/types/payment'
import type { PaymentMethod } from '~/types/payment'

/** The mocked billing endpoints the store reaches. */
const billingApi = vi.hoisted(() => ({ payInvoice: vi.fn(), startInvoiceGatewayPayment: vi.fn() }))

vi.mock('~/composables/apis/useBillingApi', () => ({ useBillingApi: () => billingApi }))

/** A saved card. */
const card = { gatewayModuleId: 'stripe', id: 'pm_1', type: PaymentMethodType.Card, description: 'Visa', gatewayName: 'stripe', cardType: 'visa', cardLastFour: '4242' } as PaymentMethod

/**
 * Mounts the panel after filling the store the page would have filled.
 *
 * @param state - Store fields to set (saved methods, gateways).
 * @returns The wrapper.
 */
const mountPanel = (state: Record<string, unknown> = {}) => {
  const pinia = createPinia()
  setActivePinia(pinia)
  Object.assign(useInvoicePaymentStore(), state)
  return mountClient(PayPanel, { props: { invoiceId: '9' }, pinia })
}

describe('PayPanel', () => {
  beforeEach(() => {
    billingApi.payInvoice.mockReset()
    billingApi.startInvoiceGatewayPayment.mockReset()
    stubClientRuntime()
  })

  it('preselects the first saved card and offers to submit', () => {
    const wrapper = mountPanel({ savedMethods: [card] })

    expect(wrapper.find('[data-testid="pay-method-pm_1"]').attributes('data-state')).toBe('checked')
    expect(wrapper.find('[data-testid="invoice-pay-submit"]').exists()).toBe(true)
  })

  it('pays with the saved card and shows the success state', async () => {
    billingApi.payInvoice.mockResolvedValue({})
    const wrapper = mountPanel({ savedMethods: [card] })

    await wrapper.find('[data-testid="invoice-pay-submit"]').trigger('click')

    await vi.waitFor(() => expect(wrapper.find('[data-testid="invoice-pay-success"]').exists()).toBe(true))
    expect(billingApi.payInvoice).toHaveBeenCalledWith('9', 'stripe', 'pm_1', expect.any(String))
    expect(wrapper.find('[data-testid="invoice-pay-submit"]').exists()).toBe(false)
  })

  it('shows the refusal and keeps the button when the payment fails', async () => {
    billingApi.payInvoice.mockRejectedValue({ data: { detail: 'Card declined.' } })
    const wrapper = mountPanel({ savedMethods: [card] })

    await wrapper.find('[data-testid="invoice-pay-submit"]').trigger('click')

    await vi.waitFor(() => expect(wrapper.find('[data-testid="invoice-pay-error"]').text()).toBe('Card declined.'))
    expect(wrapper.find('[data-testid="invoice-pay-submit"]').exists()).toBe(true)
  })

  it('pays again after a decline as a new attempt, so the gateway does not replay the decline', async () => {
    billingApi.payInvoice.mockRejectedValueOnce({ data: { detail: 'Card declined.' } }).mockResolvedValueOnce({})
    const wrapper = mountPanel({ savedMethods: [card] })

    await wrapper.find('[data-testid="invoice-pay-submit"]').trigger('click')
    await vi.waitFor(() => expect(wrapper.find('[data-testid="invoice-pay-error"]').exists()).toBe(true))
    await wrapper.find('[data-testid="invoice-pay-submit"]').trigger('click')

    await vi.waitFor(() => expect(wrapper.find('[data-testid="invoice-pay-success"]').exists()).toBe(true))
    const [first, second] = billingApi.payInvoice.mock.calls.map(call => call[3] as string)
    expect(first).toBeTruthy()
    expect(second).toBeTruthy()
    expect(second).not.toBe(first)
  })

  it('says online payment is unavailable when there is neither a saved card nor a gateway', () => {
    const wrapper = mountPanel()

    expect(wrapper.find('[data-testid="pay-method-selector"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-pay-submit"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-pay-hosted"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-pay-unavailable"]').text()).toContain('invoicePay.noPaymentOption')
  })

  it('tells a customer without a saved card to use the hosted gateway button', () => {
    const wrapper = mountPanel({ gatewayMethods: [{ module: 'hostedbank', name: 'Bank' }] })

    expect(wrapper.find('[data-testid="invoice-pay-hosted"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="invoice-pay-no-cards"]').text()).toContain('invoicePay.noSavedCardsHosted')
    expect(wrapper.find('[data-testid="invoice-pay-unavailable"]').exists()).toBe(false)
  })

  it('disables both pay buttons while a payment is being submitted', () => {
    const wrapper = mountPanel({ savedMethods: [card], gatewayMethods: [{ module: 'hostedbank', name: 'Bank' }], submitting: true })

    expect(wrapper.find('[data-testid="invoice-pay-hosted"]').attributes('disabled')).toBeDefined()
    expect(wrapper.find('[data-testid="invoice-pay-submit"]').attributes('disabled')).toBeDefined()
  })

  it('shows no empty-state hint when a saved card exists', () => {
    const wrapper = mountPanel({ savedMethods: [card], gatewayMethods: [{ module: 'hostedbank', name: 'Bank' }] })

    expect(wrapper.find('[data-testid="invoice-pay-no-cards"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-pay-unavailable"]').exists()).toBe(false)
  })
})
