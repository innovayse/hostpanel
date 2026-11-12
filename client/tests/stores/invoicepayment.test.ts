/**
 * Tests for `stores/invoicepayment.ts`, through the real billing API composable and transport against a stub server.
 *
 * The store, not the payment page, owns the in-flight flag and the refusal
 * sentence, so these assertions pin the contract the page renders from. Nothing of our own is
 * replaced: the stub plays the BFF, and the file a download hands to the browser is read at the
 * browser's own primitives.
 *
 * @module tests/stores/invoicepayment.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { captureDownloads, PDF_REPLY } from '../helpers/captureDownloads'
import { requestsTo, routeTable } from '../helpers/stubRoutes'
import type { StubRouteTable } from '../helpers/stubRoutes'
import type { StubServer } from '../helpers/stubServer'
import { bodyOf } from '../helpers/requestBody'
import { startTransport } from '../helpers/stubTransport'
import { useInvoicePaymentStore } from '~/stores/invoicepayment'
import { GatewayPaymentState } from '~/types/gatewaypaymentstate'
import { HttpMethod } from '~/types/httpmethod'
import { InvoiceStatus } from '~/types/invoicestatus'
import { PaymentModule } from '~/types/paymentmodule'
import { PaymentTarget } from '~/types/paymenttarget'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies the stub gives, by `METHOD /path`; each test fills the ones it needs. */
let table: StubRouteTable

/** The read of the gateway list. */
const GATEWAYS_ROUTE = `${HttpMethod.Get} /api/portal/order/payment-methods`

/** The hosted-gateway start of invoice 7. */
const START_ROUTE = `${HttpMethod.Post} /api/portal/client/invoices/7/gateway-payment/start`

/** The read of invoice 7. */
const INVOICE_7_ROUTE = `${HttpMethod.Get} /api/portal/client/invoices/7`

/** The read of invoice 8. */
const INVOICE_8_ROUTE = `${HttpMethod.Get} /api/portal/client/invoices/8`

/** The read of invoice 41. */
const INVOICE_41_ROUTE = `${HttpMethod.Get} /api/portal/client/invoices/41`

/** The read of invoice 42. */
const INVOICE_42_ROUTE = `${HttpMethod.Get} /api/portal/client/invoices/42`

/** The read of the PDF of invoice 41. */
const PDF_ROUTE = `${HttpMethod.Get} /api/portal/client/invoices/41/pdf`

/** The path of the bank transfer of invoice 41. */
const BANK_TRANSFER_PATH = '/api/portal/client/invoices/41/bank-transfer'

/** The choice of the bank transfer of invoice 41. */
const SELECT_TRANSFER_ROUTE = `${HttpMethod.Post} ${BANK_TRANSFER_PATH}`

/** The read of the bank transfer details of invoice 41. */
const READ_TRANSFER_ROUTE = `${HttpMethod.Get} ${BANK_TRANSFER_PATH}`

/** The bank details the endpoints answer with. */
const details = { invoiceId: 41, reference: 'INV-41', amountDue: 50, currency: 'USD', accountName: 'Example LLC', iban: 'AM00', swift: null, bankName: null, instructions: null }

beforeEach(async () => {
  setActivePinia(createPinia())
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  vi.restoreAllMocks()
  await stub.close()
})

describe('useInvoicePaymentStore.startGatewayPayment', () => {
  it('returns the gateway address and stays in flight while the browser leaves', async () => {
    table[START_ROUTE] = { body: { redirectUrl: 'https://bank.example/pay' } }
    const store = useInvoicePaymentStore()

    const url = await store.startGatewayPayment('7', 'gw', 'https://app.example/result')

    expect(url).toBe('https://bank.example/pay')
    expect(stub.requests).toHaveLength(1)
    expect(`${stub.requests[0]?.method} ${stub.requests[0]?.url}`).toBe(START_ROUTE)
    expect(bodyOf(stub.requests[0])).toEqual({ module: 'gw', returnUrl: 'https://app.example/result' })
    expect(store.submitting).toBe(true)
  })

  it('refuses a gateway address the browser must not follow and releases the flag', async () => {
    table[START_ROUTE] = { body: { redirectUrl: 'javascript:alert(1)' } }
    const store = useInvoicePaymentStore()

    const url = await store.startGatewayPayment('7', 'gw', 'https://app.example/result')

    expect(url).toBeNull()
    expect(store.submitting).toBe(false)
    expect(store.error).not.toBe('')
  })

  it('returns null, keeps the sentence and releases the flag on failure', async () => {
    table[START_ROUTE] = { status: 500 }
    const store = useInvoicePaymentStore()

    const url = await store.startGatewayPayment('7', 'gw', 'https://app.example/result')

    expect(url).toBeNull()
    expect(store.submitting).toBe(false)
    expect(store.error).not.toBe('')
  })

  it('reset clears the flag and the sentence', async () => {
    table[START_ROUTE] = { status: 500 }
    const store = useInvoicePaymentStore()
    await store.startGatewayPayment('7', 'gw', 'https://app.example/result')

    store.reset()

    expect(store.error).toBe('')
    expect(store.submitting).toBe(false)
  })
})

describe('useInvoicePaymentStore reads', () => {
  it('keeps the invoice, and the API sentence with no invoice when the read is refused', async () => {
    table[INVOICE_7_ROUTE] = { body: { id: 7, total: '10.00' } }
    table[INVOICE_8_ROUTE] = { status: 404, body: { detail: 'No such invoice.' } }
    const store = useInvoicePaymentStore()

    expect(await store.loadInvoice('7')).toMatchObject({ id: 7 })
    expect(store.invoiceError).toBe('')

    expect(await store.loadInvoice('8')).toBeNull()
    expect(store.invoice).toBeNull()
    expect(store.invoiceError).toBe('No such invoice.')
    expect(store.invoiceLoading).toBe(false)
  })

  it('reads the gateways and picks the first plugin-backed gateway', async () => {
    table[GATEWAYS_ROUTE] = {
      body: [
        { module: PaymentModule.BankTransfer, displayName: 'Bank' },
        { module: 'hosted_gateway', displayName: 'Hosted gateway' },
        { module: 'card_gateway', displayName: 'Card' }
      ]
    }
    const store = useInvoicePaymentStore()

    await store.loadPaymentOptions()

    expect(store.gatewayMethods).toHaveLength(3)
    expect(store.hostedGatewayModule).toBe('hosted_gateway')
  })

  it('has no hosted gateway when only the bank transfer is offered', async () => {
    table[GATEWAYS_ROUTE] = { body: [{ module: PaymentModule.BankTransfer, displayName: 'Bank' }] }
    const store = useInvoicePaymentStore()

    await store.loadPaymentOptions()

    expect(store.hostedGatewayModule).toBeNull()
  })

  it('leaves the gateway list empty and keeps a sentence when its read fails, then clears it on a good read', async () => {
    table[GATEWAYS_ROUTE] = { status: 400, body: { detail: 'down' } }
    const store = useInvoicePaymentStore()

    await store.loadPaymentOptions()

    expect(store.gatewayMethods).toEqual([])
    expect(store.hostedGatewayModule).toBeNull()
    expect(store.optionsError).toBe('down')

    table[GATEWAYS_ROUTE] = { body: [] }
    await store.loadPaymentOptions()

    expect(store.optionsError).toBe('')
  })
})

describe('useInvoicePaymentStore.verifyGatewayPayment', () => {
  it.each([
    [GatewayPaymentState.Paid],
    [GatewayPaymentState.Pending],
    [GatewayPaymentState.Declined]
  ])('returns the backend state %s as it is', async (word) => {
    table[`${HttpMethod.Post} /api/portal/order/42/gateway-payment/complete`] = { body: { state: word } }

    const state = await useInvoicePaymentStore().verifyGatewayPayment(PaymentTarget.Order, '42', 'tok')

    expect(state).toBe(word)
    expect(stub.requests).toHaveLength(1)
    expect(bodyOf(stub.requests[0])).toEqual({ paymentToken: 'tok' })
  })

  it('keeps the order number the backend answered with', async () => {
    table[`${HttpMethod.Post} /api/portal/order/42/gateway-payment/complete`] = { body: { state: GatewayPaymentState.Paid, orderNumber: 'ORD-0042' } }
    const store = useInvoicePaymentStore()

    await store.verifyGatewayPayment(PaymentTarget.Order, '42', 'tok')

    expect(store.verifiedOrderNumber).toBe('ORD-0042')
  })

  it('reports Unknown, never Declined, when no answer could be had', async () => {
    table[`${HttpMethod.Post} /api/portal/client/invoices/7/gateway-payment/complete`] = { status: 504 }

    const state = await useInvoicePaymentStore().verifyGatewayPayment(PaymentTarget.Invoice, '7')

    expect(state).toBe(GatewayPaymentState.Unknown)
  })
})

describe('useInvoicePaymentStore.downloadInvoicePdf', () => {
  it('saves the PDF under the invoice number and releases the in-flight flag', async () => {
    table[PDF_ROUTE] = PDF_REPLY
    const downloads = captureDownloads()
    const store = useInvoicePaymentStore()

    const pending = store.downloadInvoicePdf('41')
    expect(store.pdfDownloading).toBe(true)
    await pending

    expect(stub.requests.map(request => `${request.method} ${request.url}`)).toEqual([PDF_ROUTE])
    expect(downloads).toHaveLength(1)
    expect(downloads[0]?.fileName).toBe('invoice-41.pdf')
    expect(await downloads[0]?.blob.text()).toBe('%PDF-')
    expect(store.pdfDownloading).toBe(false)
    expect(store.pdfError).toBe('')
  })

  it('keeps the API sentence when the download is refused, and clears it on the next try', async () => {
    table[PDF_ROUTE] = { status: 400, body: { detail: 'This invoice has too many lines to download as a PDF.' } }
    const downloads = captureDownloads()
    const store = useInvoicePaymentStore()

    await store.downloadInvoicePdf('41')

    expect(downloads).toHaveLength(0)
    expect(store.pdfError).toBe('This invoice has too many lines to download as a PDF.')
    expect(store.pdfDownloading).toBe(false)

    table[PDF_ROUTE] = PDF_REPLY
    await store.downloadInvoicePdf('41')

    expect(store.pdfError).toBe('')
  })
})

describe('useInvoicePaymentStore bank transfer', () => {
  it('offers the transfer only when the gateway list holds the bank transfer module', async () => {
    const store = useInvoicePaymentStore()
    expect(store.bankTransferOffered).toBe(false)

    table[GATEWAYS_ROUTE] = { body: [{ module: 'hosted', displayName: 'Card' }] }
    await store.loadPaymentOptions()
    expect(store.bankTransferOffered).toBe(false)

    table[GATEWAYS_ROUTE] = { body: [{ module: 'hosted', displayName: 'Card' }, { module: PaymentModule.BankTransfer, displayName: 'Bank' }] }
    await store.loadPaymentOptions()
    expect(store.bankTransferOffered).toBe(true)
  })

  it('choosing the transfer keeps the details, moves the invoice to waiting and releases the flag', async () => {
    table[INVOICE_41_ROUTE] = { body: { id: 41, status: InvoiceStatus.Unpaid } }
    table[SELECT_TRANSFER_ROUTE] = { body: details }
    const store = useInvoicePaymentStore()
    await store.loadInvoice('41')

    const chosen = await store.selectBankTransfer('41')

    expect(chosen).toBe(true)
    expect(requestsTo(stub.requests, BANK_TRANSFER_PATH).map(request => request.method)).toEqual([HttpMethod.Post])
    expect(store.bankTransfer).toEqual(details)
    expect(store.invoice?.status).toBe(InvoiceStatus.PaymentPending)
    expect(store.submitting).toBe(false)
    expect(store.error).toBe('')
  })

  it('keeps the API sentence when choosing the transfer is refused', async () => {
    table[INVOICE_41_ROUTE] = { body: { id: 41, status: InvoiceStatus.Unpaid } }
    table[SELECT_TRANSFER_ROUTE] = { status: 400, body: { detail: 'Bank transfer is not available right now.' } }
    const store = useInvoicePaymentStore()
    await store.loadInvoice('41')

    const chosen = await store.selectBankTransfer('41')

    expect(chosen).toBe(false)
    expect(store.error).toBe('Bank transfer is not available right now.')
    expect(store.bankTransfer).toBeNull()
    expect(store.invoice?.status).toBe(InvoiceStatus.Unpaid)
    expect(store.submitting).toBe(false)
  })

  it('reads the details only for an invoice that waits for a transfer', async () => {
    table[READ_TRANSFER_ROUTE] = { body: details }
    const store = useInvoicePaymentStore()

    table[INVOICE_41_ROUTE] = { body: { id: 41, status: InvoiceStatus.Unpaid } }
    await store.loadInvoice('41')
    await store.loadBankTransfer('41')
    expect(requestsTo(stub.requests, BANK_TRANSFER_PATH)).toHaveLength(0)
    expect(store.bankTransfer).toBeNull()

    table[INVOICE_41_ROUTE] = { body: { id: 41, status: InvoiceStatus.PaymentPending, isBankTransferPending: true } }
    await store.loadInvoice('41')
    await store.loadBankTransfer('41')
    expect(requestsTo(stub.requests, BANK_TRANSFER_PATH)).toHaveLength(1)
    expect(store.bankTransfer).toEqual(details)
  })

  it('does not read details for a pending invoice that is not a bank transfer', async () => {
    table[INVOICE_41_ROUTE] = { body: { id: 41, status: InvoiceStatus.PaymentPending, isBankTransferPending: false } }
    const store = useInvoicePaymentStore()
    await store.loadInvoice('41')

    await store.loadBankTransfer('41')

    expect(requestsTo(stub.requests, BANK_TRANSFER_PATH)).toHaveLength(0)
    expect(store.bankTransfer).toBeNull()
    expect(store.bankTransferError).toBe('')
  })

  it('keeps the sentence of a refused details read and no details', async () => {
    table[INVOICE_41_ROUTE] = { body: { id: 41, status: InvoiceStatus.PaymentPending, isBankTransferPending: true } }
    table[READ_TRANSFER_ROUTE] = { status: 404, body: { detail: 'No bank transfer is waiting on this invoice.' } }
    const store = useInvoicePaymentStore()
    await store.loadInvoice('41')

    await store.loadBankTransfer('41')

    expect(store.bankTransfer).toBeNull()
    expect(store.bankTransferError).toBe('No bank transfer is waiting on this invoice.')
  })

  it('forgets the details of the previous invoice when another one is read', async () => {
    table[INVOICE_41_ROUTE] = { body: { id: 41, status: InvoiceStatus.Unpaid } }
    table[INVOICE_42_ROUTE] = { body: { id: 42, status: InvoiceStatus.Unpaid } }
    table[SELECT_TRANSFER_ROUTE] = { body: details }
    const store = useInvoicePaymentStore()
    await store.loadInvoice('41')
    await store.selectBankTransfer('41')

    await store.loadInvoice('42')

    expect(store.bankTransfer).toBeNull()
  })
})
