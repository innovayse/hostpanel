/**
 * Unit tests for PayPanel: the gateway button the panel offers, the unavailable state and the
 * refusal sentence.
 */
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mountClient, stubClientRuntime } from '~/tests/helpers/mountClient'
import PayPanel from '~/components/client/invoices/PayPanel.vue'
import { useInvoicePaymentStore } from '~/stores/invoicepayment'

/** The mocked billing endpoints the store reaches. */
const billingApi = vi.hoisted(() => ({ startInvoiceGatewayPayment: vi.fn() }))

vi.mock('~/composables/apis/useBillingApi', () => ({ useBillingApi: () => billingApi }))

/**
 * Mounts the panel after filling the store the page would have filled.
 *
 * @param state - Store fields to set (gateways, flags).
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
    billingApi.startInvoiceGatewayPayment.mockReset()
    stubClientRuntime()
  })

  it('says online payment is unavailable when there is no gateway', () => {
    const wrapper = mountPanel()

    expect(wrapper.find('[data-testid="invoice-pay-hosted"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-pay-unavailable"]').text()).toContain('invoicePay.noPaymentOption')
  })

  it('offers the gateway button with its hint when a gateway is enabled', () => {
    const wrapper = mountPanel({ gatewayMethods: [{ module: 'hostedbank', displayName: 'Bank' }] })

    expect(wrapper.find('[data-testid="invoice-pay-hosted"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="invoice-pay-hosted-hint"]').text()).toContain('invoicePay.hostedHint')
    expect(wrapper.find('[data-testid="invoice-pay-unavailable"]').exists()).toBe(false)
  })

  it('offers no card form or saved-method choice', () => {
    const wrapper = mountPanel({ gatewayMethods: [{ module: 'hostedbank', displayName: 'Bank' }] })

    expect(wrapper.find('[data-testid="pay-method-selector"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-pay-submit"]').exists()).toBe(false)
  })

  it('starts the gateway payment for the invoice through the store', async () => {
    billingApi.startInvoiceGatewayPayment.mockResolvedValue({ redirectUrl: 'javascript:alert(1)' })
    const wrapper = mountPanel({ gatewayMethods: [{ module: 'hostedbank', displayName: 'Bank' }] })

    await wrapper.find('[data-testid="invoice-pay-hosted"]').trigger('click')

    await vi.waitFor(() => expect(billingApi.startInvoiceGatewayPayment).toHaveBeenCalledWith('9', 'hostedbank', expect.stringContaining('/payment/result?invoice=9')))
  })

  it('disables the gateway button while a payment is being started', () => {
    const wrapper = mountPanel({ gatewayMethods: [{ module: 'hostedbank', displayName: 'Bank' }], submitting: true })

    expect(wrapper.find('[data-testid="invoice-pay-hosted"]').attributes('disabled')).toBeDefined()
  })

  it('shows the refusal sentence the store holds', () => {
    const wrapper = mountPanel({ gatewayMethods: [{ module: 'hostedbank', displayName: 'Bank' }], error: 'Gateway is down.' })

    expect(wrapper.find('[data-testid="invoice-pay-error"]').text()).toBe('Gateway is down.')
  })
})
