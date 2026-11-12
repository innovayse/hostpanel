/**
 * Tests for `pages/client/invoices/[id]/index.vue` against the real invoice payment and credit
 * stores, the real API composable and transport, with a stub BFF answering on a real socket
 * (nothing of ours is mocked; the file a download hands to the browser is read at the browser's own
 * primitives): the header, the pay button of an owing invoice, the totals, the PDF download and the
 * not-found state.
 *
 * @module tests/pages/client/invoices/[id]/index.test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { captureDownloads, PDF_REPLY } from '~/tests/helpers/captureDownloads'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import { bodyOf } from '~/tests/helpers/requestBody'
import { settle } from '~/tests/helpers/settle'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import InvoicePage from '~/pages/client/invoices/[id]/index.vue'
import { InvoiceStatus } from '~/types/invoicestatus'
import { InvoiceTransactionType } from '~/types/invoicetransactiontype'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The BFF paths the page reads and writes for invoice 41. */
const PATHS = {
  invoice: '/api/portal/client/invoices/41',
  bankTransfer: '/api/portal/client/invoices/41/bank-transfer',
  pdf: '/api/portal/client/invoices/41/pdf',
  applyCredit: '/api/portal/client/invoices/41/credit',
  credit: '/api/portal/client/credit',
} as const

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

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

/**
 * How many times the credit was read; the card under the invoice reads it when it appears.
 *
 * @returns The count.
 */
const creditReads = (): number => requestsTo(stub.requests, PATHS.credit).length

beforeEach(async () => {
  stubClientRuntime({ params: { id: '41' } })
  table = {
    [`GET ${PATHS.credit}`]: { body: credit(0) },
  }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  vi.restoreAllMocks()
  await stub.close()
})

describe('invoice page', () => {
  it('shows the bank details of an invoice that waits for a transfer and no pay button', async () => {
    table[`GET ${PATHS.invoice}`] = { body: invoice({ status: InvoiceStatus.PaymentPending, isBankTransferPending: true }) }
    table[`GET ${PATHS.bankTransfer}`] = { body: { invoiceId: 41, reference: 'INV-41', amountDue: 100, currency: 'USD', accountName: null, iban: 'AM00', swift: null, bankName: null, instructions: null } }

    const wrapper = await mountPage(InvoicePage)
    await waitUntil(() => expect(wrapper.find('[data-testid="bank-transfer-card"]').exists()).toBe(true))

    expect(requestsTo(stub.requests, PATHS.bankTransfer).map(request => request.method)).toEqual(['GET'])
    expect(wrapper.find('[data-testid="invoice-pay-now"]').exists()).toBe(false)
  })

  it('shows no bank details for an invoice that owes money and waits for nothing', async () => {
    table[`GET ${PATHS.invoice}`] = { body: invoice() }

    const wrapper = await mountPage(InvoicePage)
    await waitUntil(() => expect(wrapper.find('[data-testid="invoice-pay-now"]').exists()).toBe(true))

    expect(requestsTo(stub.requests, PATHS.bankTransfer)).toHaveLength(0)
    expect(wrapper.find('[data-testid="bank-transfer-card"]').exists()).toBe(false)
  })

  it('reads this invoice and shows its heading and line items', async () => {
    table[`GET ${PATHS.invoice}`] = { body: invoice() }

    const wrapper = await mountPage(InvoicePage)
    await waitUntil(() => expect(wrapper.find('[data-testid="invoice-heading"]').exists()).toBe(true))

    expect(requestsTo(stub.requests, PATHS.invoice).map(request => request.method)).toEqual(['GET'])
    expect(wrapper.find('[data-testid="invoice-heading"]').text()).toContain('client.invoices.heading')
    expect(wrapper.find('[data-testid="invoice-items"]').text()).toContain('Hosting')
  })

  it('notes the client group discount on a discounted line only', async () => {
    billingApi.fetchInvoice.mockResolvedValue(
      invoice({ items: [{ id: 1, description: 'Hosting', amount: 90, discountPercent: 10 }] }),
    )
    const discounted = await mountPage(InvoicePage)
    billingApi.fetchInvoice.mockResolvedValue(invoice())
    const plain = await mountPage(InvoicePage)

    expect(discounted.find('[data-testid="invoice-item-discount"]').exists()).toBe(true)
    expect(plain.find('[data-testid="invoice-item-discount"]').exists()).toBe(false)
  })

  it('shows the amounts in the invoice currency, with tax only when there is some', async () => {
    table[`GET ${PATHS.invoice}`] = { body: invoice() }
    const taxed = await mountPage(InvoicePage)
    await waitUntil(() => expect(taxed.find('[data-testid="invoice-items"]').exists()).toBe(true))
    table[`GET ${PATHS.invoice}`] = { body: invoice({ tax: 0, total: 90, amountDue: 90 }) }
    const untaxed = await mountPage(InvoicePage)
    await waitUntil(() => expect(untaxed.find('[data-testid="invoice-items"]').exists()).toBe(true))

    expect(taxed.find('[data-testid="invoice-items"]').text()).toContain('$100.00')
    expect(taxed.find('[data-testid="invoice-items"]').text()).toContain('client.invoices.tax')
    expect(untaxed.find('[data-testid="invoice-items"]').text()).not.toContain('client.invoices.tax')
  })

  it('offers payment for an owing invoice', async () => {
    table[`GET ${PATHS.invoice}`] = { body: invoice() }

    const wrapper = await mountPage(InvoicePage)
    await waitUntil(() => expect(wrapper.find('[data-testid="invoice-pay-now"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="invoice-pay-now"]').attributes('href')).toBe('/client/invoices/41/pay')
    expect(wrapper.find('[data-testid="invoice-unpaid-banner"]').exists()).toBe(true)
  })

  it('puts what is still owed on the pay button of a part-paid invoice, not the whole total', async () => {
    table[`GET ${PATHS.invoice}`] = { body: invoice({ amountDue: 60, transactions: [{ id: 1, amount: 40, type: InvoiceTransactionType.Payment }] }) }

    const wrapper = await mountPage(InvoicePage)
    await waitUntil(() => expect(wrapper.find('[data-testid="invoice-pay-amount"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="invoice-pay-amount"]').text()).toContain('$60.00')
  })

  it('offers to pay with credit when the client has some, and reads the invoice again once it is applied', async () => {
    table[`GET ${PATHS.credit}`] = { body: credit(30) }
    table[`POST ${PATHS.applyCredit}`] = { body: {} }
    // The first read is the invoice as it was; every read after the credit was applied is the invoice as it is now.
    table[`GET ${PATHS.invoice}`] = () => ({
      body: requestsTo(stub.requests, PATHS.invoice).length <= 1 ? invoice() : invoice({ credit: 30, total: 70, amountDue: 70 })
    })
    const wrapper = await mountPage(InvoicePage)
    await waitUntil(() => expect(wrapper.find('[data-testid="apply-credit-amount"]').exists()).toBe(true))

    await wrapper.get('[data-testid="apply-credit-amount"]').setValue('30')
    await wrapper.get('[data-testid="apply-credit-card"] form').trigger('submit')
    await waitUntil(() => expect(wrapper.get('[data-testid="invoice-pay-amount"]').text()).toContain('$70.00'))

    expect(bodyOf(requestsTo(stub.requests, PATHS.applyCredit)[0])).toEqual({ amount: 30 })
    expect(requestsTo(stub.requests, PATHS.invoice)).toHaveLength(2)
  })

  it('offers no credit payment when the client has no credit', async () => {
    table[`GET ${PATHS.invoice}`] = { body: invoice() }

    const wrapper = await mountPage(InvoicePage)
    await waitUntil(() => expect(creditReads()).toBe(1))
    await settle()

    expect(wrapper.find('[data-testid="apply-credit-card"]').exists()).toBe(false)
  })

  it('marks a credit top-up invoice and offers no credit for it', async () => {
    table[`GET ${PATHS.credit}`] = { body: credit(30) }
    table[`GET ${PATHS.invoice}`] = { body: invoice({ isCreditTopUp: true }) }

    const wrapper = await mountPage(InvoicePage)
    await waitUntil(() => expect(creditReads()).toBe(1))
    await settle()

    expect(wrapper.find('[data-testid="invoice-top-up-badge"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="apply-credit-card"]').exists()).toBe(false)
  })

  it('offers no payment for a paid invoice', async () => {
    table[`GET ${PATHS.invoice}`] = { body: invoice({ status: InvoiceStatus.Paid, paidAt: '2026-01-05T10:00:00Z' }) }

    const wrapper = await mountPage(InvoicePage)
    await waitUntil(() => expect(wrapper.find('[data-testid="invoice-header"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="invoice-pay-now"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invoice-unpaid-banner"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('client.invoices.datePaid')
  })

  it('says the invoice was not found when the read fails', async () => {
    table[`GET ${PATHS.invoice}`] = { status: 404, body: { detail: 'No such invoice.' } }

    const wrapper = await mountPage(InvoicePage)
    await waitUntil(() => expect(wrapper.find('[data-testid="ui-empty-state"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="ui-empty-state"]').text()).toContain('client.invoices.notFound')
  })

  it('downloads the invoice PDF and saves it under the invoice number', async () => {
    table[`GET ${PATHS.invoice}`] = { body: invoice() }
    table[`GET ${PATHS.pdf}`] = PDF_REPLY
    const downloads = captureDownloads()

    const wrapper = await mountPage(InvoicePage)
    await waitUntil(() => expect(wrapper.find('[data-testid="invoice-download"]').exists()).toBe(true))
    const button = wrapper.find('[data-testid="invoice-download"]')
    await button.trigger('click')
    await waitUntil(() => expect(downloads).toHaveLength(1))
    await flushPromises()

    expect(button.text()).toContain('client.invoices.downloadPdf')
    expect(requestsTo(stub.requests, PATHS.pdf).map(request => request.method)).toEqual(['GET'])
    expect(downloads[0]?.fileName).toBe('invoice-41.pdf')
    expect(await downloads[0]?.blob.text()).toBe('%PDF-')
    expect(wrapper.find('[data-testid="invoice-download-error"]').exists()).toBe(false)
  })

  it('shows the API sentence in an alert when the download fails', async () => {
    table[`GET ${PATHS.invoice}`] = { body: invoice() }
    table[`GET ${PATHS.pdf}`] = { status: 400, body: { detail: 'This invoice has too many lines to download as a PDF.' } }
    const downloads = captureDownloads()

    const wrapper = await mountPage(InvoicePage)
    await waitUntil(() => expect(wrapper.find('[data-testid="invoice-download"]').exists()).toBe(true))
    await wrapper.find('[data-testid="invoice-download"]').trigger('click')
    await waitUntil(() => expect(wrapper.find('[data-testid="invoice-download-error"]').exists()).toBe(true))

    expect(downloads).toHaveLength(0)
    expect(wrapper.find('[data-testid="invoice-download-error"]').text())
      .toContain('This invoice has too many lines to download as a PDF.')
  })

  it('offers no PDF download for a draft', async () => {
    table[`GET ${PATHS.invoice}`] = { body: invoice({ status: InvoiceStatus.Draft }) }

    const wrapper = await mountPage(InvoicePage)
    await waitUntil(() => expect(wrapper.find('[data-testid="invoice-header"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="invoice-download"]').exists()).toBe(false)
  })
})
