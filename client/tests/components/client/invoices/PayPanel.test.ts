/**
 * Unit tests for PayPanel: the gateway button the panel offers, the unavailable state and the
 * refusal sentence.
 *
 * The real invoice payment store, billing API composable and transport run against a stub server
 * playing the BFF; the assertions are on the requests that reached it.
 *
 * @module tests/components/client/invoices/PayPanel.test
 */
import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountClient, stubClientRuntime } from '~/tests/helpers/mountClient'
import PayPanel from '~/components/client/invoices/PayPanel.vue'
import { useInvoicePaymentStore } from '~/stores/invoicepayment'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'

/** The BFF path that starts the hosted-gateway payment of invoice 9. */
const GATEWAY_START_PATH = '/api/portal/client/invoices/9/gateway-payment/start'

/** The BFF path that chooses the bank transfer for invoice 9. */
const BANK_TRANSFER_PATH = '/api/portal/client/invoices/9/bank-transfer'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it acts. */
let table: StubRouteTable

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
  beforeEach(async () => {
    stubClientRuntime()
    table = {}
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
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
    table[`POST ${GATEWAY_START_PATH}`] = { body: { redirectUrl: 'javascript:alert(1)' } }
    const wrapper = mountPanel({ gatewayMethods: [{ module: 'hostedbank', displayName: 'Bank' }] })

    await wrapper.find('[data-testid="invoice-pay-hosted"]').trigger('click')

    await vi.waitFor(() => expect(requestsTo(stub.requests, GATEWAY_START_PATH)).toHaveLength(1))
    const sent = bodyOf(requestsTo(stub.requests, GATEWAY_START_PATH)[0]) as { module: string, returnUrl: string }
    expect(sent.module).toBe('hostedbank')
    expect(sent.returnUrl).toContain('/payment/result?invoice=9')
  })

  it('disables the gateway button while a payment is being started', () => {
    const wrapper = mountPanel({ gatewayMethods: [{ module: 'hostedbank', displayName: 'Bank' }], submitting: true })

    expect(wrapper.find('[data-testid="invoice-pay-hosted"]').attributes('disabled')).toBeDefined()
  })

  it('offers the bank transfer beside the card when the admin switched it on', () => {
    const wrapper = mountPanel({ gatewayMethods: [{ module: 'hostedbank', displayName: 'Bank' }, { module: 'bank_transfer', displayName: 'Transfer' }] })

    expect(wrapper.find('[data-testid="invoice-pay-hosted"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="invoice-pay-bank-transfer"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="invoice-pay-unavailable"]').exists()).toBe(false)
  })

  it('offers the bank transfer alone when no gateway plugin is enabled', () => {
    const wrapper = mountPanel({ gatewayMethods: [{ module: 'bank_transfer', displayName: 'Transfer' }] })

    expect(wrapper.find('[data-testid="invoice-pay-hosted"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-pay-bank-transfer"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="invoice-pay-unavailable"]').exists()).toBe(false)
  })

  it('chooses the bank transfer through the store and then shows the bank details', async () => {
    table[`POST ${BANK_TRANSFER_PATH}`] = { body: { invoiceId: 9, reference: 'INV-9', amountDue: 10, currency: 'USD', accountName: null, iban: 'AM00', swift: null, bankName: null, instructions: null } }
    const wrapper = mountPanel({
      gatewayMethods: [{ module: 'bank_transfer', displayName: 'Transfer' }],
      invoice: { id: 9, status: 'Unpaid' }
    })

    await wrapper.find('[data-testid="invoice-pay-bank-transfer"]').trigger('click')

    await vi.waitFor(() => expect(requestsTo(stub.requests, BANK_TRANSFER_PATH)).toHaveLength(1))
    await vi.waitFor(() => expect(wrapper.find('[data-testid="bank-transfer-card"]').exists()).toBe(true))
    expect(wrapper.find('[data-testid="bank-transfer-reference"]').text()).toBe('INV-9')
    expect(wrapper.find('[data-testid="invoice-pay-bank-transfer"]').exists()).toBe(false)
  })

  it('shows the bank details instead of the payment choices when the invoice already waits for a transfer', () => {
    const wrapper = mountPanel({
      gatewayMethods: [{ module: 'hostedbank', displayName: 'Bank' }, { module: 'bank_transfer', displayName: 'Transfer' }],
      invoice: { id: 9, status: 'PaymentPending', isBankTransferPending: true },
      bankTransfer: { invoiceId: 9, reference: 'INV-9', amountDue: 10, currency: 'USD', accountName: null, iban: 'AM00', swift: null, bankName: null, instructions: null }
    })

    expect(wrapper.find('[data-testid="bank-transfer-card"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="invoice-pay-hosted"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-pay-bank-transfer"]').exists()).toBe(false)
  })

  it('keeps the payment choices for a pending invoice that is not a bank transfer (an imported one)', () => {
    const wrapper = mountPanel({
      gatewayMethods: [{ module: 'hostedbank', displayName: 'Bank' }, { module: 'bank_transfer', displayName: 'Transfer' }],
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
    const wrapper = mountPanel({ gatewayMethods: [{ module: 'hostedbank', displayName: 'Bank' }], error: 'Gateway is down.' })

    expect(wrapper.find('[data-testid="invoice-pay-error"]').text()).toBe('Gateway is down.')
  })
})
