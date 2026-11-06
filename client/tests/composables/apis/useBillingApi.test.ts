/**
 * Tests for `composables/apis/useBillingApi.ts`.
 *
 * Each endpoint function is called for real through `apiFetch` against a stub server, and the
 * test asserts on the path, verb, query and body that arrived and on how the reply was typed.
 *
 * @module tests/composables/apis/useBillingApi.test
 */

import { afterEach, describe, expect, it } from 'vitest'
import { expectLastRequest, startTransport } from '../../helpers/stubTransport'
import type { ExpectedRequest } from '../../helpers/stubTransport'
import type { StubServer } from '../../helpers/stubServer'
import { useBillingApi } from '~/composables/apis/useBillingApi'
import { PaymentTarget } from '~/types/paymenttarget'
import { HttpMethod } from '~/types/httpmethod'

/** The stub started by the current test. */
let stub: StubServer | undefined

afterEach(async () => {
  await stub?.close()
  stub = undefined
})

/** The api surface under test. */
const api = useBillingApi()

/** Every endpoint function with the request it must send. */
const rows: ExpectedRequest[] = [
  { name: 'fetchInvoice reads one invoice', call: () => api.fetchInvoice('7'), method: HttpMethod.Get, url: '/api/portal/client/invoices/7' },
  { name: 'fetchInvoicePdf reads the invoice PDF', call: () => api.fetchInvoicePdf('7'), method: HttpMethod.Get, url: '/api/portal/client/invoices/7/pdf' },
  { name: 'fetchInvoicePdf encodes the id into one path segment', call: () => api.fetchInvoicePdf('7/../x'), method: HttpMethod.Get, url: '/api/portal/client/invoices/7%2F..%2Fx/pdf' },
  { name: 'fetchGatewayMethods reads the gateways under the order surface', call: () => api.fetchGatewayMethods(), method: HttpMethod.Get, url: '/api/portal/order/payment-methods' },
  { name: 'createOrder posts the order', call: () => api.createOrder({ items: [], paymentmethod: 'card_gateway' }), method: HttpMethod.Post, url: '/api/portal/order/create', body: { items: [], paymentmethod: 'card_gateway' } },
  { name: 'startOrderGatewayPayment posts module, return address and token', call: () => api.startOrderGatewayPayment(42, 'hosted_gateway', 'https://x/return', 'tok'), method: HttpMethod.Post, url: '/api/portal/order/42/gateway-payment/start', body: { module: 'hosted_gateway', returnUrl: 'https://x/return', paymentToken: 'tok' } },
  { name: 'startInvoiceGatewayPayment posts module and return address', call: () => api.startInvoiceGatewayPayment(7, 'hosted_gateway', 'https://x/return'), method: HttpMethod.Post, url: '/api/portal/client/invoices/7/gateway-payment/start', body: { module: 'hosted_gateway', returnUrl: 'https://x/return' } },
  { name: 'selectBankTransfer posts to the invoice bank transfer route', call: () => api.selectBankTransfer(7), method: HttpMethod.Post, url: '/api/portal/client/invoices/7/bank-transfer', body: {} },
  { name: 'fetchBankTransfer reads the invoice bank transfer route', call: () => api.fetchBankTransfer(7), method: HttpMethod.Get, url: '/api/portal/client/invoices/7/bank-transfer' },
  { name: 'fetchCredit reads the credit page', call: () => api.fetchCredit(2, 10), method: HttpMethod.Get, url: '/api/portal/client/credit?page=2&pageSize=10' },
  { name: 'createCreditTopUp posts the amount to the top-up route', call: () => api.createCreditTopUp(40), method: HttpMethod.Post, url: '/api/portal/client/credit/top-ups', body: { amount: 40 } },
  { name: 'applyInvoiceCredit posts the amount to the invoice credit route', call: () => api.applyInvoiceCredit(7, 12.5), method: HttpMethod.Post, url: '/api/portal/client/invoices/7/credit', body: { amount: 12.5 } },
  { name: 'completeGatewayPayment for an order sends the token', call: () => api.completeGatewayPayment(PaymentTarget.Order, 42, 'tok'), method: HttpMethod.Post, url: '/api/portal/order/42/gateway-payment/complete', body: { paymentToken: 'tok' } },
  { name: 'completeGatewayPayment for an invoice sends an empty body', call: () => api.completeGatewayPayment(PaymentTarget.Invoice, 7), method: HttpMethod.Post, url: '/api/portal/client/invoices/7/gateway-payment/complete', body: {} },
]

describe('useBillingApi', () => {
  it.each(rows)('$name', async (row) => {
    stub = await startTransport()

    await row.call()

    expectLastRequest(stub, row)
  })

  it('returns the order ids the endpoint answers with', async () => {
    stub = await startTransport(() => ({ body: { orderId: 42, invoiceId: 77, paymentToken: 'tok' } }))

    const placed = await api.createOrder({})

    expect(placed).toEqual({ orderId: 42, invoiceId: 77, paymentToken: 'tok' })
  })

  it('returns the PDF as a blob', async () => {
    stub = await startTransport(() => ({ body: '%PDF-1.4 test', headers: { 'content-type': 'application/pdf' } }))

    const pdf = await api.fetchInvoicePdf('41')

    expect(pdf).toBeInstanceOf(Blob)
    expect(await pdf.text()).toBe('%PDF-1.4 test')
  })
})
