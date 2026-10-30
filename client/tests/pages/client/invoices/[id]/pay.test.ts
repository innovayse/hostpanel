/**
 * Tests for `pages/client/invoices/[id]/pay.vue`: the layout, the already-paid and not-found
 * states and the saved-card payment.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import PayPage from '~/pages/client/invoices/[id]/pay.vue'
import { InvoiceStatus } from '~/types/invoicestatus'
import { PaymentMethodType } from '~/types/payment'

/** The mocked billing endpoints the store reaches. */
const billingApi = vi.hoisted(() => ({
  fetchInvoice: vi.fn(), fetchPaymentMethods: vi.fn(), fetchGatewayMethods: vi.fn(), payInvoice: vi.fn()
}))

vi.mock('~/composables/apis/useBillingApi', () => ({ useBillingApi: () => billingApi }))

/**
 * Builds an invoice.
 *
 * @param status - Invoice status.
 * @returns The invoice.
 */
const invoice = (status: InvoiceStatus) => ({
  id: 41, status, currency: 'USD', subTotal: 100, credit: 0, total: 100, amountDue: 100, items: [{ id: 1, description: 'Hosting', amount: 100 }], transactions: []
})

/** A saved card. */
const card = { id: 'pm_1', type: PaymentMethodType.Card, description: 'Visa', gatewayName: 'stripe', cardType: 'visa', cardLastFour: '4242' }

describe('invoice payment page', () => {
  beforeEach(() => {
    Object.values(billingApi).forEach((fn) => fn.mockReset())
    billingApi.fetchPaymentMethods.mockResolvedValue([card])
    billingApi.fetchGatewayMethods.mockResolvedValue([])
    stubClientRuntime({ params: { id: '41' } })
  })

  it('shows the payment panel beside the invoice summary', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice(InvoiceStatus.Unpaid))

    const wrapper = await mountPage(PayPage)

    expect(wrapper.find('[data-testid="invoice-pay-panel"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="pay-summary"]').text()).toContain('Hosting')
    expect(wrapper.find('[data-testid="invoice-pay-security"]').exists()).toBe(true)
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

  it('pays with the preselected saved card', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice(InvoiceStatus.Unpaid))
    billingApi.payInvoice.mockResolvedValue({})
    const wrapper = await mountPage(PayPage)

    await wrapper.find('[data-testid="invoice-pay-submit"]').trigger('click')

    await vi.waitFor(() => expect(wrapper.find('[data-testid="invoice-pay-success"]').exists()).toBe(true))
    expect(billingApi.payInvoice).toHaveBeenCalledWith('41', 'pm_1', expect.any(String))
  })

  it('links the breadcrumb back to the invoice', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice(InvoiceStatus.Unpaid))

    const wrapper = await mountPage(PayPage)

    expect(wrapper.find('nav a[href="/client/invoices/41"]').exists()).toBe(true)
  })
})
