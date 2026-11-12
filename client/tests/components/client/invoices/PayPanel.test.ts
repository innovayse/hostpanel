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
const billingApi = vi.hoisted(() => ({ startInvoiceGatewayPayment: vi.fn(), selectBankTransfer: vi.fn() }))

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
    billingApi.selectBankTransfer.mockReset()
    stubClientRuntime()
  })

  it('says online payment is unavailable when there is no gateway', () => {
    const wrapper = mountPanel()

    expect(wrapper.find('[data-testid="invoice-pay-hosted"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-pay-unavailable"]').text()).toContain('invoicePay.noPaymentOption')
  })

  it('offers the gateway button with its hint when a gateway is enabled', () => {
    const wrapper = mountPanel({ gatewayMethods: [{ module: 'hostedbank', displayName: 'Bank', supportsStoredCredentials: false }] })

    expect(wrapper.find('[data-testid="invoice-pay-hosted"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="invoice-pay-hosted-hint"]').text()).toContain('invoicePay.hostedHint')
    expect(wrapper.find('[data-testid="invoice-pay-unavailable"]').exists()).toBe(false)
  })

  it('offers no card form or saved-method choice', () => {
    const wrapper = mountPanel({ gatewayMethods: [{ module: 'hostedbank', displayName: 'Bank', supportsStoredCredentials: false }] })

    expect(wrapper.find('[data-testid="pay-method-selector"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-pay-submit"]').exists()).toBe(false)
  })

  it('starts the gateway payment for the invoice through the store', async () => {
    billingApi.startInvoiceGatewayPayment.mockResolvedValue({ redirectUrl: 'javascript:alert(1)' })
    const wrapper = mountPanel({ gatewayMethods: [{ module: 'hostedbank', displayName: 'Bank', supportsStoredCredentials: false }] })

    await wrapper.find('[data-testid="invoice-pay-hosted"]').trigger('click')

    await vi.waitFor(() => expect(billingApi.startInvoiceGatewayPayment).toHaveBeenCalledWith('9', 'hostedbank', expect.stringContaining('/payment/result?invoice=9')))
  })

  it('disables the gateway button while a payment is being started', () => {
    const wrapper = mountPanel({ gatewayMethods: [{ module: 'hostedbank', displayName: 'Bank', supportsStoredCredentials: false }], submitting: true })

    expect(wrapper.find('[data-testid="invoice-pay-hosted"]').attributes('disabled')).toBeDefined()
  })

  it('offers the bank transfer beside the card when the admin switched it on', () => {
    const wrapper = mountPanel({ gatewayMethods: [{ module: 'hostedbank', displayName: 'Bank', supportsStoredCredentials: false }, { module: 'bank_transfer', displayName: 'Transfer', supportsStoredCredentials: false }] })

    expect(wrapper.find('[data-testid="invoice-pay-hosted"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="invoice-pay-bank-transfer"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="invoice-pay-unavailable"]').exists()).toBe(false)
  })

  it('offers the bank transfer alone when no gateway plugin is enabled', () => {
    const wrapper = mountPanel({ gatewayMethods: [{ module: 'bank_transfer', displayName: 'Transfer', supportsStoredCredentials: false }] })

    expect(wrapper.find('[data-testid="invoice-pay-hosted"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-pay-bank-transfer"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="invoice-pay-unavailable"]').exists()).toBe(false)
  })

  it('chooses the bank transfer through the store and then shows the bank details', async () => {
    billingApi.selectBankTransfer.mockResolvedValue({ invoiceId: 9, reference: 'INV-9', amountDue: 10, currency: 'USD', accountName: null, iban: 'AM00', swift: null, bankName: null, instructions: null })
    const wrapper = mountPanel({
      gatewayMethods: [{ module: 'bank_transfer', displayName: 'Transfer', supportsStoredCredentials: false }],
      invoice: { id: 9, status: 'Unpaid' }
    })

    await wrapper.find('[data-testid="invoice-pay-bank-transfer"]').trigger('click')

    await vi.waitFor(() => expect(billingApi.selectBankTransfer).toHaveBeenCalledWith('9'))
    await vi.waitFor(() => expect(wrapper.find('[data-testid="bank-transfer-card"]').exists()).toBe(true))
    expect(wrapper.find('[data-testid="bank-transfer-reference"]').text()).toBe('INV-9')
    expect(wrapper.find('[data-testid="invoice-pay-bank-transfer"]').exists()).toBe(false)
  })

  it('shows the bank details instead of the payment choices when the invoice already waits for a transfer', () => {
    const wrapper = mountPanel({
      gatewayMethods: [{ module: 'hostedbank', displayName: 'Bank', supportsStoredCredentials: false }, { module: 'bank_transfer', displayName: 'Transfer', supportsStoredCredentials: false }],
      invoice: { id: 9, status: 'PaymentPending', isBankTransferPending: true },
      bankTransfer: { invoiceId: 9, reference: 'INV-9', amountDue: 10, currency: 'USD', accountName: null, iban: 'AM00', swift: null, bankName: null, instructions: null }
    })

    expect(wrapper.find('[data-testid="bank-transfer-card"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="invoice-pay-hosted"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-pay-bank-transfer"]').exists()).toBe(false)
  })

  it('keeps the payment choices for a pending invoice that is not a bank transfer (an imported one)', () => {
    const wrapper = mountPanel({
      gatewayMethods: [{ module: 'hostedbank', displayName: 'Bank', supportsStoredCredentials: false }, { module: 'bank_transfer', displayName: 'Transfer', supportsStoredCredentials: false }],
      invoice: { id: 9, status: 'PaymentPending', isBankTransferPending: false }
    })

    expect(wrapper.find('[data-testid="bank-transfer-card"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-pay-hosted"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="invoice-pay-bank-transfer"]').exists()).toBe(true)
  })

  it('shows the sentence of a refused details read while the invoice waits for a transfer', () => {
    const wrapper = mountPanel({ invoice: { id: 9, status: 'PaymentPending', isBankTransferPending: true }, bankTransferError: 'Could not read the bank details.' })

    expect(wrapper.find('[data-testid="invoice-bank-transfer-error"]').text()).toBe('Could not read the bank details.')
    expect(wrapper.find('[data-testid="bank-transfer-card"]').exists()).toBe(false)
  })

  it('shows the refusal sentence the store holds', () => {
    const wrapper = mountPanel({ gatewayMethods: [{ module: 'hostedbank', displayName: 'Bank', supportsStoredCredentials: false }], error: 'Gateway is down.' })

    expect(wrapper.find('[data-testid="invoice-pay-error"]').text()).toBe('Gateway is down.')
  })
})
