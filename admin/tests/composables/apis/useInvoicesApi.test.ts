/**
 * Tests for the invoices API composable: paths, verbs, query strings and bodies through the
 * real `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useInvoicesApi } from '@/composables/apis/useInvoicesApi'
import { InvoiceRefundType } from '@/types/invoice'
import { ApiError } from '@/composables/useApi'
import { json, problem, stubFetch, callOf } from '../../helpers/fetch'

/** The composable under test. */
type InvoicesApi = ReturnType<typeof useInvoicesApi>

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useInvoicesApi', () => {
  it('list sends page, size and only the filters that are set', async () => {
    const mock = stubFetch(json({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 }))

    await useInvoicesApi().list(2, 25, { status: 'Paid', from: '', to: '2026-01-31' })
    expect(callOf(mock)).toMatchObject({ url: '/api/billing?page=2&pageSize=25&status=Paid&to=2026-01-31', method: 'GET' })
  })

  it('listByClient reads the client-scoped path', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))

    await useInvoicesApi().listByClient(7, 1, 10)
    expect(callOf(mock).url).toBe('/api/billing/client/7?page=1&pageSize=10')
  })

  it('get reads one invoice', async () => {
    const mock = stubFetch(json({ id: 5 }))

    expect(await useInvoicesApi().get(5)).toEqual({ id: 5 })
    expect(callOf(mock).url).toBe('/api/billing/5')
  })

  it('create POSTs the payload and returns the id', async () => {
    const mock = stubFetch(json({ id: 9 }))
    const payload = { clientId: 1, dueDate: '2026-02-01', items: [{ description: 'a', unitPrice: 2, quantity: 1 }], isDraft: true }

    expect(await useInvoicesApi().create(payload)).toEqual({ id: 9 })
    expect(callOf(mock)).toMatchObject({ url: '/api/billing', method: 'POST', body: payload })
  })

  it.each([
    ['updateItems', (a: InvoicesApi) => a.updateItems(5, []), '/api/billing/5/items', 'PUT', { items: [] }],
    ['updateNotes', (a: InvoicesApi) => a.updateNotes(5, 'n'), '/api/billing/5/notes', 'PUT', { notes: 'n' }],
    ['publish', (a: InvoicesApi) => a.publish(5), '/api/billing/5/publish', 'POST', undefined],
    ['cancel', (a: InvoicesApi) => a.cancel(5), '/api/billing/5/cancel', 'POST', undefined],
    ['applyCredit', (a: InvoicesApi) => a.applyCredit(5, 3), '/api/billing/5/credit', 'POST', { amount: 3 }],
    ['removeCredit', (a: InvoicesApi) => a.removeCredit(5, 3), '/api/billing/5/credit/remove', 'POST', { amount: 3 }],
    ['remove', (a: InvoicesApi) => a.remove(5), '/api/billing/5', 'DELETE', undefined],
    ['bulk', (a: InvoicesApi) => a.bulk([1, 2], 'Delete'), '/api/billing/bulk', 'POST', { invoiceIds: [1, 2], action: 'Delete' }],
  ])('%s sends the expected verb, path and body', async (_name, act, url, method, body) => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await act(useInvoicesApi())
    expect(callOf(mock)).toMatchObject({ url, method, body })
  })

  it('updateOptions, addPayment and refund send their payloads unchanged', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useInvoicesApi()
    const options = { invoiceDate: 'a', dueDate: 'b', paymentMethod: 'Manual', taxRate: 5 }
    const payment = { date: 'a', gateway: 'g', transactionId: 't', amount: 1 }
    const refund = { amount: 1, refundType: InvoiceRefundType.Gateway }

    await api.updateOptions(5, options)
    await api.addPayment(5, payment)
    await api.refund(5, refund)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/billing/5/options', method: 'PUT', body: options })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/billing/5/payment', method: 'POST', body: payment })
    expect(callOf(mock, 2)).toMatchObject({ url: '/api/billing/5/refund', method: 'POST', body: refund })
  })

  it('duplicate POSTs and returns the new id', async () => {
    const mock = stubFetch(json({ id: 11 }))

    expect(await useInvoicesApi().duplicate(5)).toEqual({ id: 11 })
    expect(callOf(mock)).toMatchObject({ url: '/api/billing/5/duplicate', method: 'POST' })
  })

  it('pdf downloads the invoice PDF with the file name the API gives', async () => {
    const mock = stubFetch(
      new Response('%PDF', {
        status: 200,
        headers: { 'Content-Type': 'application/pdf', 'Content-Disposition': 'attachment; filename="invoice-5.pdf"' },
      }),
    )

    const file = await useInvoicesApi().pdf(5)

    expect(callOf(mock)).toMatchObject({ url: '/api/billing/5/pdf', method: 'GET' })
    expect(file.fileName).toBe('invoice-5.pdf')
    expect(file.blob.size).toBe(4)
  })

  it('pdf rejects with an ApiError carrying the refusal body', async () => {
    stubFetch(problem('The invoice has too many lines for a PDF.', 409))

    await expect(useInvoicesApi().pdf(5)).rejects.toMatchObject({
      status: 409,
      body: { detail: 'The invoice has too many lines for a PDF.' },
    })
  })

  it('rejects with an ApiError carrying the refusal body', async () => {
    stubFetch(problem('Invoice is locked.', 409))

    await expect(useInvoicesApi().cancel(5)).rejects.toBeInstanceOf(ApiError)
  })
})
