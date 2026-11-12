/**
 * Tests for `pages/client/invoices/[id]/pay.vue` against the real invoice payment store, the real
 * billing API composable and transport, with a stub BFF answering on a real socket (nothing of ours
 * is mocked): the layout, the already-paid and not-found states and the gateway payment button.
 *
 * @module tests/pages/client/invoices/[id]/pay.test
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { bankTransferPath, BillingEndpoint, invoicePath } from '~/tests/helpers/billingPaths'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import { readOf, refusal, requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { waitUntil } from '~/tests/helpers/waitUntil'
import PayPage from '~/pages/client/invoices/[id]/pay.vue'
import { InvoiceStatus } from '~/types/invoicestatus'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/** The one hosted gateway the stub offers when a test wants a gateway on. */
const HOSTED_GATEWAY = { module: 'hostedbank', displayName: 'Bank', supportsStoredCredentials: false }

/**
 * Builds an invoice.
 *
 * @param status - Invoice status.
 * @param isBankTransferPending - Whether a bank transfer waits on it; true for a payment-pending invoice by default.
 * @returns The invoice.
 */
const invoice = (status: InvoiceStatus, isBankTransferPending = status === InvoiceStatus.PaymentPending) => ({
  id: 41, status, isBankTransferPending, currency: 'USD', subTotal: 100, credit: 0, total: 100, amountDue: 100, items: [{ id: 1, description: 'Hosting', amount: 100 }], transactions: []
})

beforeEach(async () => {
  stubClientRuntime({ params: { id: '41' } })
  table = { [readOf(BillingEndpoint.OrderPaymentMethods)]: { body: [] } }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('invoice payment page', () => {
  it('shows the payment panel beside the invoice summary and the security notice with a hosted gateway', async () => {
    table[readOf(invoicePath(41))] = { body: invoice(InvoiceStatus.Unpaid) }
    table[readOf(BillingEndpoint.OrderPaymentMethods)] = { body: [HOSTED_GATEWAY] }

    const wrapper = await mountPage(PayPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="invoice-pay-security"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="invoice-pay-panel"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="pay-summary"]').text()).toContain('Hosting')
  })

  it('says online payment is unavailable and hides the security notice when no gateway exists', async () => {
    table[readOf(invoicePath(41))] = { body: invoice(InvoiceStatus.Unpaid) }

    const wrapper = await mountPage(PayPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="invoice-pay-unavailable"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="invoice-pay-security"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-pay-options-error"]').exists()).toBe(false)
  })

  it('shows an error with a retry when the gateway list cannot be read, and recovers on retry', async () => {
    table[readOf(invoicePath(41))] = { body: invoice(InvoiceStatus.Unpaid) }
    table[readOf(BillingEndpoint.OrderPaymentMethods)] = refusal('Gateways down.')
    const wrapper = await mountPage(PayPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="invoice-pay-options-error"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="invoice-pay-options-error"]').text()).toContain('Gateways down.')
    expect(wrapper.find('[data-testid="invoice-pay-unavailable"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-pay-security"]').exists()).toBe(false)

    table[readOf(BillingEndpoint.OrderPaymentMethods)] = { body: [HOSTED_GATEWAY] }
    await wrapper.find('[data-testid="invoice-pay-options-retry"]').trigger('click')
    await waitUntil(() => expect(wrapper.find('[data-testid="invoice-pay-hosted"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="invoice-pay-options-error"]').exists()).toBe(false)
  })

  it('says an already paid invoice has been paid and offers no payment', async () => {
    table[readOf(invoicePath(41))] = { body: invoice(InvoiceStatus.Paid) }

    const wrapper = await mountPage(PayPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="invoice-already-paid"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="invoice-pay-panel"]').exists()).toBe(false)
  })

  it('treats a partially refunded invoice as already paid and offers no payment', async () => {
    table[readOf(invoicePath(41))] = { body: invoice(InvoiceStatus.PartiallyRefunded) }

    const wrapper = await mountPage(PayPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="invoice-already-paid"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="invoice-pay-panel"]').exists()).toBe(false)
  })

  it('says the invoice was not found when the read fails', async () => {
    table[readOf(invoicePath(41))] = refusal('No such invoice.')

    const wrapper = await mountPage(PayPage)
    await waitUntil(() => expect(wrapper.text()).toContain('client.invoices.notFound'))

    expect(wrapper.find('[data-testid="invoice-pay-panel"]').exists()).toBe(false)
  })

  it('offers the gateway payment button when a gateway is enabled', async () => {
    table[readOf(invoicePath(41))] = { body: invoice(InvoiceStatus.Unpaid) }
    table[readOf(BillingEndpoint.OrderPaymentMethods)] = { body: [HOSTED_GATEWAY] }

    const wrapper = await mountPage(PayPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="invoice-pay-hosted"]').exists()).toBe(true))
  })

  it('shows the bank details of an invoice that waits for a transfer, with no payment choices or gateway notice', async () => {
    table[readOf(invoicePath(41))] = { body: invoice(InvoiceStatus.PaymentPending) }
    table[readOf(BillingEndpoint.OrderPaymentMethods)] = { body: [HOSTED_GATEWAY] }
    table[readOf(bankTransferPath(41))] = { body: { invoiceId: 41, reference: 'INV-41', amountDue: 100, currency: 'USD', accountName: null, iban: 'AM00', swift: null, bankName: null, instructions: null } }

    const wrapper = await mountPage(PayPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="bank-transfer-card"]').exists()).toBe(true))

    expect(requestsTo(stub.requests, bankTransferPath(41)).map(request => request.method)).toEqual(['GET'])
    expect(wrapper.find('[data-testid="bank-transfer-reference"]').text()).toBe('INV-41')
    expect(wrapper.find('[data-testid="invoice-pay-hosted"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-pay-security"]').exists()).toBe(false)
  })

  it('does not read bank details for an invoice that does not wait for a transfer', async () => {
    table[readOf(invoicePath(41))] = { body: invoice(InvoiceStatus.Unpaid) }

    const wrapper = await mountPage(PayPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="invoice-pay-panel"]').exists()).toBe(true))

    expect(requestsTo(stub.requests, bankTransferPath(41))).toHaveLength(0)
  })

  it('links the breadcrumb back to the invoice', async () => {
    table[readOf(invoicePath(41))] = { body: invoice(InvoiceStatus.Unpaid) }

    const wrapper = await mountPage(PayPage)
    await waitUntil(() => expect(wrapper.find('nav a[href="/client/invoices/41"]').exists()).toBe(true))
  })
})
