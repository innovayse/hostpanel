/**
 * Tests for `stores/invoicepayment.ts`.
 *
 * The store, not the payment page, owns the in-flight flag and the refusal
 * sentence, so these assertions pin the contract the page renders from.
 *
 * @module tests/stores/invoicepayment.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { ref } from 'vue'
import { GatewayPaymentState } from '~/types/gatewaypaymentstate'
import { PaymentTarget } from '~/types/paymenttarget'

/** The mocked hosted-gateway start call, re-created for every test. */
const startGatewayMock = vi.fn()

/** The mocked invoice read. */
const fetchInvoiceMock = vi.fn()

/** The mocked gateway-list read. */
const fetchGatewayMethodsMock = vi.fn()

/** The mocked gateway settlement call. */
const completeGatewayMock = vi.fn()

/** The mocked PDF read. */
const fetchInvoicePdfMock = vi.fn()

/** The mocked bank transfer choice. */
const selectBankTransferMock = vi.fn()

/** The mocked bank transfer details read. */
const fetchBankTransferMock = vi.fn()

/** The mocked browser save, so no test touches the DOM. */
const saveBlobMock = vi.fn()

vi.stubGlobal('ref', ref)

vi.mock('~/composables/apis/useBillingApi', () => ({
  /**
   * Stands in for the real API composable so the store runs without transport.
   *
   * @returns Only the endpoints the tests reach for.
   */
  useBillingApi: () => ({
    startInvoiceGatewayPayment: startGatewayMock,
    fetchInvoice: fetchInvoiceMock,
    fetchGatewayMethods: fetchGatewayMethodsMock,
    completeGatewayPayment: completeGatewayMock,
    fetchInvoicePdf: fetchInvoicePdfMock,
    selectBankTransfer: selectBankTransferMock,
    fetchBankTransfer: fetchBankTransferMock
  })
}))

vi.mock('~/utils/saveBlob', () => ({ saveBlob: saveBlobMock }))

const { useInvoicePaymentStore } = await import('~/stores/invoicepayment')

describe('useInvoicePaymentStore.startGatewayPayment', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    startGatewayMock.mockReset()
  })

  it('returns the gateway address and stays in flight while the browser leaves', async () => {
    startGatewayMock.mockResolvedValue({ redirectUrl: 'https://bank.example/pay' })
    const store = useInvoicePaymentStore()

    const url = await store.startGatewayPayment('7', 'gw', 'https://app.example/result')

    expect(url).toBe('https://bank.example/pay')
    expect(startGatewayMock).toHaveBeenCalledWith('7', 'gw', 'https://app.example/result')
    expect(store.submitting).toBe(true)
  })

  it('refuses a gateway address the browser must not follow and releases the flag', async () => {
    startGatewayMock.mockResolvedValue({ redirectUrl: 'javascript:alert(1)' })
    const store = useInvoicePaymentStore()

    const url = await store.startGatewayPayment('7', 'gw', 'https://app.example/result')

    expect(url).toBeNull()
    expect(store.submitting).toBe(false)
    expect(store.error).not.toBe('')
  })

  it('returns null, keeps the sentence and releases the flag on failure', async () => {
    startGatewayMock.mockRejectedValue(new Error('down'))
    const store = useInvoicePaymentStore()

    const url = await store.startGatewayPayment('7', 'gw', 'https://app.example/result')

    expect(url).toBeNull()
    expect(store.submitting).toBe(false)
    expect(store.error).not.toBe('')
  })

  it('reset clears the flag and the sentence', async () => {
    startGatewayMock.mockRejectedValue(new Error('down'))
    const store = useInvoicePaymentStore()
    await store.startGatewayPayment('7', 'gw', 'https://app.example/result')

    store.reset()

    expect(store.error).toBe('')
    expect(store.submitting).toBe(false)
  })
})

describe('useInvoicePaymentStore reads', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    fetchInvoiceMock.mockReset()
    fetchGatewayMethodsMock.mockReset()
    completeGatewayMock.mockReset()
  })

  it('keeps the invoice, and the API sentence with no invoice when the read is refused', async () => {
    fetchInvoiceMock.mockResolvedValueOnce({ id: 7, total: '10.00' }).mockRejectedValueOnce({ data: { detail: 'No such invoice.' } })
    const store = useInvoicePaymentStore()

    expect(await store.loadInvoice('7')).toMatchObject({ id: 7 })
    expect(store.invoiceError).toBe('')

    expect(await store.loadInvoice('8')).toBeNull()
    expect(store.invoice).toBeNull()
    expect(store.invoiceError).toBe('No such invoice.')
    expect(store.invoiceLoading).toBe(false)
  })

  it('reads the gateways and picks the first plugin-backed gateway', async () => {
    fetchGatewayMethodsMock.mockResolvedValue([
      { module: 'bank_transfer', displayName: 'Bank' },
      { module: 'hosted_gateway', displayName: 'Hosted gateway' },
      { module: 'card_gateway', displayName: 'Card' }
    ])
    const store = useInvoicePaymentStore()

    await store.loadPaymentOptions()

    expect(store.gatewayMethods).toHaveLength(3)
    expect(store.hostedGatewayModule).toBe('hosted_gateway')
  })

  it('has no hosted gateway when only the bank transfer is offered', async () => {
    fetchGatewayMethodsMock.mockResolvedValue([{ module: 'bank_transfer', displayName: 'Bank' }])
    const store = useInvoicePaymentStore()

    await store.loadPaymentOptions()

    expect(store.hostedGatewayModule).toBeNull()
  })

  it('leaves the gateway list empty and keeps a sentence when its read fails, then clears it on a good read', async () => {
    fetchGatewayMethodsMock.mockRejectedValueOnce({ data: { detail: 'down' } })
    const store = useInvoicePaymentStore()

    await store.loadPaymentOptions()

    expect(store.gatewayMethods).toEqual([])
    expect(store.hostedGatewayModule).toBeNull()
    expect(store.optionsError).toBe('down')

    fetchGatewayMethodsMock.mockResolvedValue([])
    await store.loadPaymentOptions()

    expect(store.optionsError).toBe('')
  })
})

describe('useInvoicePaymentStore.verifyGatewayPayment', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    completeGatewayMock.mockReset()
  })

  it.each([
    [GatewayPaymentState.Paid],
    [GatewayPaymentState.Pending],
    [GatewayPaymentState.Declined]
  ])('returns the backend state %s as it is', async (word) => {
    completeGatewayMock.mockResolvedValue({ state: word })

    const state = await useInvoicePaymentStore().verifyGatewayPayment(PaymentTarget.Order, '42', 'tok')

    expect(state).toBe(word)
    expect(completeGatewayMock).toHaveBeenCalledWith('order', '42', 'tok')
  })

  it('keeps the order number the backend answered with', async () => {
    completeGatewayMock.mockResolvedValue({ state: GatewayPaymentState.Paid, orderNumber: 'ORD-0042' })
    const store = useInvoicePaymentStore()

    await store.verifyGatewayPayment(PaymentTarget.Order, '42', 'tok')

    expect(store.verifiedOrderNumber).toBe('ORD-0042')
  })

  it('reports Unknown, never Declined, when no answer could be had', async () => {
    completeGatewayMock.mockRejectedValue(new Error('timeout'))

    const state = await useInvoicePaymentStore().verifyGatewayPayment(PaymentTarget.Invoice, '7')

    expect(state).toBe(GatewayPaymentState.Unknown)
  })
})

describe('useInvoicePaymentStore.downloadInvoicePdf', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    fetchInvoicePdfMock.mockReset()
    saveBlobMock.mockReset()
  })

  it('saves the PDF under the invoice number and releases the in-flight flag', async () => {
    const pdf = new Blob(['%PDF'], { type: 'application/pdf' })
    fetchInvoicePdfMock.mockResolvedValue(pdf)
    const store = useInvoicePaymentStore()

    const pending = store.downloadInvoicePdf('41')
    expect(store.pdfDownloading).toBe(true)
    await pending

    expect(fetchInvoicePdfMock).toHaveBeenCalledWith('41')
    expect(saveBlobMock).toHaveBeenCalledWith(pdf, 'invoice-41.pdf')
    expect(store.pdfDownloading).toBe(false)
    expect(store.pdfError).toBe('')
  })

  it('keeps the API sentence when the download is refused, and clears it on the next try', async () => {
    fetchInvoicePdfMock.mockRejectedValueOnce({ data: { detail: 'This invoice has too many lines to download as a PDF.' } })
    const store = useInvoicePaymentStore()

    await store.downloadInvoicePdf('41')

    expect(saveBlobMock).not.toHaveBeenCalled()
    expect(store.pdfError).toBe('This invoice has too many lines to download as a PDF.')
    expect(store.pdfDownloading).toBe(false)

    fetchInvoicePdfMock.mockResolvedValueOnce(new Blob(['%PDF']))
    await store.downloadInvoicePdf('41')

    expect(store.pdfError).toBe('')
  })
})

describe('useInvoicePaymentStore.downloadInvoicePdf', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    fetchInvoicePdfMock.mockReset()
    saveBlobMock.mockReset()
  })

  it('saves the PDF under the invoice number and releases the in-flight flag', async () => {
    const pdf = new Blob(['%PDF'], { type: 'application/pdf' })
    fetchInvoicePdfMock.mockResolvedValue(pdf)
    const store = useInvoicePaymentStore()

    const pending = store.downloadInvoicePdf('41')
    expect(store.pdfDownloading).toBe(true)
    await pending

    expect(fetchInvoicePdfMock).toHaveBeenCalledWith('41')
    expect(saveBlobMock).toHaveBeenCalledWith(pdf, 'invoice-41.pdf')
    expect(store.pdfDownloading).toBe(false)
    expect(store.pdfError).toBe('')
  })

  it('keeps the API sentence when the download is refused, and clears it on the next try', async () => {
    fetchInvoicePdfMock.mockRejectedValueOnce({ data: { detail: 'This invoice has too many lines to download as a PDF.' } })
    const store = useInvoicePaymentStore()

    await store.downloadInvoicePdf('41')

    expect(saveBlobMock).not.toHaveBeenCalled()
    expect(store.pdfError).toBe('This invoice has too many lines to download as a PDF.')
    expect(store.pdfDownloading).toBe(false)

    fetchInvoicePdfMock.mockResolvedValueOnce(new Blob(['%PDF']))
    await store.downloadInvoicePdf('41')

    expect(store.pdfError).toBe('')
  })
})

/** The bank details the endpoints answer with. */
const details = { invoiceId: 41, reference: 'INV-41', amountDue: 50, currency: 'USD', accountName: 'Example LLC', iban: 'AM00', swift: null, bankName: null, instructions: null }

describe('useInvoicePaymentStore bank transfer', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    selectBankTransferMock.mockReset()
    fetchBankTransferMock.mockReset()
    fetchInvoiceMock.mockReset()
    fetchGatewayMethodsMock.mockReset()
  })

  it('offers the transfer only when the gateway list holds the bank transfer module', async () => {
    const store = useInvoicePaymentStore()
    expect(store.bankTransferOffered).toBe(false)

    fetchGatewayMethodsMock.mockResolvedValue([{ module: 'hosted', displayName: 'Card' }])
    await store.loadPaymentOptions()
    expect(store.bankTransferOffered).toBe(false)

    fetchGatewayMethodsMock.mockResolvedValue([{ module: 'hosted', displayName: 'Card' }, { module: 'bank_transfer', displayName: 'Bank' }])
    await store.loadPaymentOptions()
    expect(store.bankTransferOffered).toBe(true)
  })

  it('choosing the transfer keeps the details, moves the invoice to waiting and releases the flag', async () => {
    fetchInvoiceMock.mockResolvedValue({ id: 41, status: 'Unpaid' })
    selectBankTransferMock.mockResolvedValue(details)
    const store = useInvoicePaymentStore()
    await store.loadInvoice('41')

    const chosen = await store.selectBankTransfer('41')

    expect(chosen).toBe(true)
    expect(selectBankTransferMock).toHaveBeenCalledWith('41')
    expect(store.bankTransfer).toEqual(details)
    expect(store.invoice?.status).toBe('PaymentPending')
    expect(store.submitting).toBe(false)
    expect(store.error).toBe('')
  })

  it('keeps the API sentence when choosing the transfer is refused', async () => {
    fetchInvoiceMock.mockResolvedValue({ id: 41, status: 'Unpaid' })
    selectBankTransferMock.mockRejectedValue({ data: { detail: 'Bank transfer is not available right now.' } })
    const store = useInvoicePaymentStore()
    await store.loadInvoice('41')

    const chosen = await store.selectBankTransfer('41')

    expect(chosen).toBe(false)
    expect(store.error).toBe('Bank transfer is not available right now.')
    expect(store.bankTransfer).toBeNull()
    expect(store.invoice?.status).toBe('Unpaid')
    expect(store.submitting).toBe(false)
  })

  it('reads the details only for an invoice that waits for a transfer', async () => {
    fetchBankTransferMock.mockResolvedValue(details)
    const store = useInvoicePaymentStore()

    fetchInvoiceMock.mockResolvedValue({ id: 41, status: 'Unpaid' })
    await store.loadInvoice('41')
    await store.loadBankTransfer('41')
    expect(fetchBankTransferMock).not.toHaveBeenCalled()
    expect(store.bankTransfer).toBeNull()

    fetchInvoiceMock.mockResolvedValue({ id: 41, status: 'PaymentPending' })
    await store.loadInvoice('41')
    await store.loadBankTransfer('41')
    expect(fetchBankTransferMock).toHaveBeenCalledWith('41')
    expect(store.bankTransfer).toEqual(details)
  })

  it('keeps the sentence of a refused details read and no details', async () => {
    fetchInvoiceMock.mockResolvedValue({ id: 41, status: 'PaymentPending' })
    fetchBankTransferMock.mockRejectedValue({ data: { detail: 'No bank transfer is waiting on this invoice.' } })
    const store = useInvoicePaymentStore()
    await store.loadInvoice('41')

    await store.loadBankTransfer('41')

    expect(store.bankTransfer).toBeNull()
    expect(store.bankTransferError).toBe('No bank transfer is waiting on this invoice.')
  })

  it('forgets the details of the previous invoice when another one is read', async () => {
    fetchInvoiceMock.mockResolvedValue({ id: 41, status: 'Unpaid' })
    selectBankTransferMock.mockResolvedValue(details)
    const store = useInvoicePaymentStore()
    await store.loadInvoice('41')
    await store.selectBankTransfer('41')

    await store.loadInvoice('42')

    expect(store.bankTransfer).toBeNull()
  })
})
