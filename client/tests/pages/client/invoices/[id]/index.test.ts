/**
 * Tests for `pages/client/invoices/[id]/index.vue`: the header, the pay button of an owing invoice, the
 * totals, the PDF download and the not-found state.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import InvoicePage from '~/pages/client/invoices/[id]/index.vue'
import { InvoiceStatus } from '~/types/invoicestatus'
import { InvoiceTransactionType } from '~/types/invoicetransactiontype'

/** The mocked billing endpoints the store reaches. */
const billingApi = vi.hoisted(() => ({
  fetchInvoice: vi.fn(),
  fetchInvoicePdf: vi.fn(),
  fetchBankTransfer: vi.fn(),
  fetchCredit: vi.fn(),
  applyInvoiceCredit: vi.fn(),
}))

/** The mocked browser save. */
const saveBlob = vi.hoisted(() => vi.fn())

vi.mock('~/composables/apis/useBillingApi', () => ({ useBillingApi: () => billingApi }))
vi.mock('~/utils/saveBlob', () => ({ saveBlob }))

/**
 * Builds an invoice.
 *
 * @param overrides - Fields to set.
 * @returns The invoice.
 */
const invoice = (overrides: Record<string, unknown> = {}) => ({
  id: 41, status: InvoiceStatus.Unpaid, currency: 'USD', invoiceDate: '2026-01-01', dueDate: '2099-01-15', paidAt: null, paymentMethod: 'card',
  subTotal: 90, tax: 10, credit: 0, total: 100, amountDue: 100, isCreditTopUp: false, isBankTransferPending: false,
  items: [{ id: 1, description: 'Hosting', amount: 90, discountPercent: 0 }], transactions: [],
  ...overrides
})

/**
 * Builds a credit with an empty history.
 *
 * @param balance - The balance.
 * @returns The credit.
 */
const credit = (balance: number) => ({
  balance, currency: 'USD', topUpMinimum: 5, topUpMaximum: 1000,
  entries: { items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 0 }
})

describe('invoice page', () => {
  beforeEach(() => {
    billingApi.fetchInvoice.mockReset()
    billingApi.fetchInvoicePdf.mockReset()
    billingApi.fetchBankTransfer.mockReset()
    billingApi.fetchCredit.mockReset()
    billingApi.applyInvoiceCredit.mockReset()
    billingApi.fetchCredit.mockResolvedValue(credit(0))
    saveBlob.mockReset()
    stubClientRuntime({ params: { id: '41' } })
  })

  it('shows the bank details of an invoice that waits for a transfer and no pay button', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice({ status: InvoiceStatus.PaymentPending, isBankTransferPending: true }))
    billingApi.fetchBankTransfer.mockResolvedValue({ invoiceId: 41, reference: 'INV-41', amountDue: 100, currency: 'USD', accountName: null, iban: 'AM00', swift: null, bankName: null, instructions: null })

    const wrapper = await mountPage(InvoicePage)

    expect(billingApi.fetchBankTransfer).toHaveBeenCalledWith('41')
    expect(wrapper.find('[data-testid="bank-transfer-card"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="invoice-pay-now"]').exists()).toBe(false)
  })

  it('shows no bank details for an invoice that owes money and waits for nothing', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice())

    const wrapper = await mountPage(InvoicePage)

    expect(billingApi.fetchBankTransfer).not.toHaveBeenCalled()
    expect(wrapper.find('[data-testid="bank-transfer-card"]').exists()).toBe(false)
  })

  it('reads this invoice and shows its heading and line items', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice())

    const wrapper = await mountPage(InvoicePage)

    expect(billingApi.fetchInvoice).toHaveBeenCalledWith('41')
    expect(wrapper.find('[data-testid="invoice-heading"]').text()).toContain('client.invoices.heading')
    expect(wrapper.find('[data-testid="invoice-items"]').text()).toContain('Hosting')
  })

  it('notes the client group discount on a discounted line only', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice({ items: [{ id: 1, description: 'Hosting', amount: 90, discountPercent: 10 }] }))
    const discounted = await mountPage(InvoicePage)
    billingApi.fetchInvoice.mockResolvedValue(invoice())
    const plain = await mountPage(InvoicePage)

    expect(discounted.find('[data-testid="invoice-item-discount"]').exists()).toBe(true)
    expect(plain.find('[data-testid="invoice-item-discount"]').exists()).toBe(false)
  })

  it('shows the amounts in the invoice currency, with tax only when there is some', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice())
    const taxed = await mountPage(InvoicePage)
    billingApi.fetchInvoice.mockResolvedValue(invoice({ tax: 0, total: 90, amountDue: 90 }))
    const untaxed = await mountPage(InvoicePage)

    expect(taxed.find('[data-testid="invoice-items"]').text()).toContain('$100.00')
    expect(taxed.find('[data-testid="invoice-items"]').text()).toContain('client.invoices.tax')
    expect(untaxed.find('[data-testid="invoice-items"]').text()).not.toContain('client.invoices.tax')
  })

  it('offers payment for an owing invoice', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice())

    const wrapper = await mountPage(InvoicePage)

    expect(wrapper.find('[data-testid="invoice-pay-now"]').attributes('href')).toBe('/client/invoices/41/pay')
    expect(wrapper.find('[data-testid="invoice-unpaid-banner"]').exists()).toBe(true)
  })

  it('puts what is still owed on the pay button of a part-paid invoice, not the whole total', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice({ amountDue: 60, transactions: [{ id: 1, amount: 40, type: InvoiceTransactionType.Payment }] }))

    const wrapper = await mountPage(InvoicePage)

    expect(wrapper.find('[data-testid="invoice-pay-amount"]').text()).toContain('$60.00')
  })

  it('offers to pay with credit when the client has some, and reads the invoice again once it is applied', async () => {
    billingApi.fetchCredit.mockResolvedValue(credit(30))
    billingApi.applyInvoiceCredit.mockResolvedValue(undefined)
    billingApi.fetchInvoice.mockResolvedValueOnce(invoice()).mockResolvedValueOnce(invoice({ credit: 30, total: 70, amountDue: 70 }))
    const wrapper = await mountPage(InvoicePage)
    await flushPromises()

    await wrapper.get('[data-testid="apply-credit-amount"]').setValue('30')
    await wrapper.get('[data-testid="apply-credit-card"] form').trigger('submit')
    await flushPromises()

    expect(billingApi.applyInvoiceCredit).toHaveBeenCalledWith(41, 30)
    expect(billingApi.fetchInvoice).toHaveBeenCalledTimes(2)
    expect(wrapper.get('[data-testid="invoice-pay-amount"]').text()).toContain('$70.00')
  })

  it('offers no credit payment when the client has no credit', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice())

    const wrapper = await mountPage(InvoicePage)
    await flushPromises()

    expect(wrapper.find('[data-testid="apply-credit-card"]').exists()).toBe(false)
  })

  it('marks a credit top-up invoice and offers no credit for it', async () => {
    billingApi.fetchCredit.mockResolvedValue(credit(30))
    billingApi.fetchInvoice.mockResolvedValue(invoice({ isCreditTopUp: true }))

    const wrapper = await mountPage(InvoicePage)
    await flushPromises()

    expect(wrapper.find('[data-testid="invoice-top-up-badge"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="apply-credit-card"]').exists()).toBe(false)
  })

  it('offers no payment for a paid invoice', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice({ status: InvoiceStatus.Paid, paidAt: '2026-01-05T10:00:00Z' }))

    const wrapper = await mountPage(InvoicePage)

    expect(wrapper.find('[data-testid="invoice-pay-now"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-unpaid-banner"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('client.invoices.datePaid')
  })

  it('says the invoice was not found when the read fails', async () => {
    billingApi.fetchInvoice.mockRejectedValue({ data: { detail: 'No such invoice.' } })

    const wrapper = await mountPage(InvoicePage)

    expect(wrapper.find('[data-testid="ui-empty-state"]').text()).toContain('client.invoices.notFound')
  })

  it('downloads the invoice PDF and saves it under the invoice number', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice())
    const pdf = new Blob(['%PDF'], { type: 'application/pdf' })
    billingApi.fetchInvoicePdf.mockResolvedValue(pdf)

    const wrapper = await mountPage(InvoicePage)
    const button = wrapper.find('[data-testid="invoice-download"]')
    await button.trigger('click')
    await flushPromises()

    expect(button.text()).toContain('client.invoices.downloadPdf')
    expect(billingApi.fetchInvoicePdf).toHaveBeenCalledWith('41')
    expect(saveBlob).toHaveBeenCalledWith(pdf, 'invoice-41.pdf')
    expect(wrapper.find('[data-testid="invoice-download-error"]').exists()).toBe(false)
  })

  it('shows the API sentence in an alert when the download fails', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice())
    billingApi.fetchInvoicePdf.mockRejectedValue({ data: { detail: 'This invoice has too many lines to download as a PDF.' } })

    const wrapper = await mountPage(InvoicePage)
    await wrapper.find('[data-testid="invoice-download"]').trigger('click')
    await flushPromises()

    expect(saveBlob).not.toHaveBeenCalled()
    expect(wrapper.find('[data-testid="invoice-download-error"]').text())
      .toContain('This invoice has too many lines to download as a PDF.')
  })

  it('offers no PDF download for a draft', async () => {
    billingApi.fetchInvoice.mockResolvedValue(invoice({ status: InvoiceStatus.Draft }))

    const wrapper = await mountPage(InvoicePage)

    expect(wrapper.find('[data-testid="invoice-header"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="invoice-download"]').exists()).toBe(false)
  })
})
