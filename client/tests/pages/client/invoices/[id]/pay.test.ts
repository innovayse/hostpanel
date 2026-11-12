/**
 * Tests for `pages/client/invoices/[id]/pay.vue`: the layout, the already-paid and not-found
 * states and the gateway payment button.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import PayPage from '~/pages/client/invoices/[id]/pay.vue'
import { InvoiceStatus } from '~/types/invoicestatus'

/** The mocked billing endpoints the store reaches. */
const billingApi = vi.hoisted(() => ({
  fetchInvoice: vi.fn(), fetchGatewayMethods: vi.fn(), fetchBankTransfer: vi.fn()
}))

vi.mock('~/composables/apis/useBillingApi', () => ({ useBillingApi: () => billingApi }))

/**
 * Builds an invoice.
 *
 * @param status - Invoice status.
 * @returns The invoice.
 */
const invoice = (status: InvoiceStatus, isBankTransferPending = status === InvoiceStatus.PaymentPending) => ({
  id: 41, status, isBankTransferPending, currency: 'USD', subTotal: 100, credit: 0, total: 100, amountDue: 100, items: [{ id: 1, description: 'Hosting', amount: 100 }], transactions: []
})

describe('invoice payment page', () => {
  beforeEach(() => {
    Object.values(billingApi).forEach((fn) => fn.mockReset())
    billingApi.fetchGatewayMethods.mockResolvedValue([])
    stubClientRuntime({ params: { id: '41' } })
  })

  it('shows the payment panel beside the invoice summary and the security notice with a hosted gateway', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice(InvoiceStatus.Unpaid))
    billingApi.fetchGatewayMethods.mockResolvedValue([{ module: 'hostedbank', displayName: 'Bank', supportsStoredCredentials: false }])

    const wrapper = await mountPage(PayPage)

    expect(wrapper.find('[data-testid="invoice-pay-panel"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="pay-summary"]').text()).toContain('Hosting')
    expect(wrapper.find('[data-testid="invoice-pay-security"]').exists()).toBe(true)
  })

  it('says online payment is unavailable and hides the security notice when no gateway exists', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice(InvoiceStatus.Unpaid))

    const wrapper = await mountPage(PayPage)

    expect(wrapper.find('[data-testid="invoice-pay-unavailable"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="invoice-pay-security"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-pay-options-error"]').exists()).toBe(false)
  })

  it('shows an error with a retry when the gateway list cannot be read, and recovers on retry', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice(InvoiceStatus.Unpaid))
    billingApi.fetchGatewayMethods.mockRejectedValueOnce({ data: { detail: 'Gateways down.' } })
    const wrapper = await mountPage(PayPage)

    expect(wrapper.find('[data-testid="invoice-pay-options-error"]').text()).toContain('Gateways down.')
    expect(wrapper.find('[data-testid="invoice-pay-unavailable"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-pay-security"]').exists()).toBe(false)

    billingApi.fetchGatewayMethods.mockResolvedValue([{ module: 'hostedbank', displayName: 'Bank', supportsStoredCredentials: false }])
    await wrapper.find('[data-testid="invoice-pay-options-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="invoice-pay-options-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-pay-hosted"]').exists()).toBe(true)
  })

  it('says an already paid invoice has been paid and offers no payment', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice(InvoiceStatus.Paid))

    const wrapper = await mountPage(PayPage)

    expect(wrapper.find('[data-testid="invoice-already-paid"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="invoice-pay-panel"]').exists()).toBe(false)
  })

  it('treats a partially refunded invoice as already paid and offers no payment', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice(InvoiceStatus.PartiallyRefunded))

    const wrapper = await mountPage(PayPage)

    expect(wrapper.find('[data-testid="invoice-already-paid"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="invoice-pay-panel"]').exists()).toBe(false)
  })

  it('says the invoice was not found when the read fails', async () => {
    billingApi.fetchInvoice.mockRejectedValue({ data: { detail: 'No such invoice.' } })

    const wrapper = await mountPage(PayPage)

    expect(wrapper.text()).toContain('client.invoices.notFound')
    expect(wrapper.find('[data-testid="invoice-pay-panel"]').exists()).toBe(false)
  })

  it('offers the gateway payment button when a gateway is enabled', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice(InvoiceStatus.Unpaid))
    billingApi.fetchGatewayMethods.mockResolvedValue([{ module: 'hostedbank', displayName: 'Bank', supportsStoredCredentials: false }])
    const wrapper = await mountPage(PayPage)

    expect(wrapper.find('[data-testid="invoice-pay-hosted"]').exists()).toBe(true)
  })

  it('shows the bank details of an invoice that waits for a transfer, with no payment choices or gateway notice', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice(InvoiceStatus.PaymentPending))
    billingApi.fetchGatewayMethods.mockResolvedValue([{ module: 'hostedbank', displayName: 'Bank', supportsStoredCredentials: false }])
    billingApi.fetchBankTransfer.mockResolvedValue({ invoiceId: 41, reference: 'INV-41', amountDue: 100, currency: 'USD', accountName: null, iban: 'AM00', swift: null, bankName: null, instructions: null })

    const wrapper = await mountPage(PayPage)

    expect(billingApi.fetchBankTransfer).toHaveBeenCalledWith('41')
    expect(wrapper.find('[data-testid="bank-transfer-card"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="bank-transfer-reference"]').text()).toBe('INV-41')
    expect(wrapper.find('[data-testid="invoice-pay-hosted"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-pay-security"]').exists()).toBe(false)
  })

  it('does not read bank details for an invoice that does not wait for a transfer', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice(InvoiceStatus.Unpaid))

    await mountPage(PayPage)

    expect(billingApi.fetchBankTransfer).not.toHaveBeenCalled()
  })

  it('links the breadcrumb back to the invoice', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice(InvoiceStatus.Unpaid))

    const wrapper = await mountPage(PayPage)

    expect(wrapper.find('nav a[href="/client/invoices/41"]').exists()).toBe(true)
  })
})
