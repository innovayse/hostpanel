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
import { PaymentMethodType } from '~/types/payment'
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
  { name: 'fetchPaymentMethods reads the saved methods', call: () => api.fetchPaymentMethods(), method: HttpMethod.Get, url: '/api/portal/client/payment-methods' },
  { name: 'fetchGatewayMethods reads the gateways under the order surface', call: () => api.fetchGatewayMethods(), method: HttpMethod.Get, url: '/api/portal/order/payment-methods' },
  { name: 'payInvoice posts the saved method id and the attempt', call: () => api.payInvoice('7', 'pm_1', 'attempt-1'), method: HttpMethod.Post, url: '/api/portal/client/invoices/7/pay', body: { paymentMethodId: 'pm_1', attemptId: 'attempt-1' } },
  { name: 'setDefaultPaymentMethod puts the default flag', call: () => api.setDefaultPaymentMethod('pm_1'), method: HttpMethod.Put, url: '/api/portal/client/payment-methods/pm_1', body: { setAsDefault: true } },
  { name: 'removePaymentMethod deletes by id', call: () => api.removePaymentMethod('pm_1'), method: HttpMethod.Delete, url: '/api/portal/client/payment-methods/pm_1' },
  { name: 'createOrder posts the order', call: () => api.createOrder({ items: [], paymentmethod: 'stripe' }), method: HttpMethod.Post, url: '/api/portal/order/create', body: { items: [], paymentmethod: 'stripe' } },
  { name: 'createOrderPaymentIntent posts the payment token and does not save the card by default', call: () => api.createOrderPaymentIntent(42, 'tok'), method: HttpMethod.Post, url: '/api/portal/order/42/create-payment-intent', body: { paymentToken: 'tok', saveCard: false } },
  { name: 'createOrderPaymentIntent posts the choice to save the card', call: () => api.createOrderPaymentIntent(42, 'tok', true), method: HttpMethod.Post, url: '/api/portal/order/42/create-payment-intent', body: { paymentToken: 'tok', saveCard: true } },
  { name: 'createSetupIntent posts the agreement', call: () => api.createSetupIntent(true), method: HttpMethod.Post, url: '/api/portal/client/payment-methods/setup-intent', body: { consentAccepted: true } },
  { name: 'confirmSetupIntent posts the setup intent id', call: () => api.confirmSetupIntent('seti_1'), method: HttpMethod.Post, url: '/api/portal/client/payment-methods/confirm-setup', body: { setupIntentId: 'seti_1' } },
  { name: 'turnOffAutoCharge deletes the account agreement', call: () => api.turnOffAutoCharge(), method: HttpMethod.Delete, url: '/api/portal/client/payment-methods/auto-charge' },
  { name: 'startOrderGatewayPayment posts module, return address and token', call: () => api.startOrderGatewayPayment(42, 'inecobank', 'https://x/return', 'tok'), method: HttpMethod.Post, url: '/api/portal/order/42/gateway-payment/start', body: { module: 'inecobank', returnUrl: 'https://x/return', paymentToken: 'tok' } },
  { name: 'startInvoiceGatewayPayment posts module and return address', call: () => api.startInvoiceGatewayPayment(7, 'inecobank', 'https://x/return'), method: HttpMethod.Post, url: '/api/portal/client/invoices/7/gateway-payment/start', body: { module: 'inecobank', returnUrl: 'https://x/return' } },
  { name: 'completeGatewayPayment for an order sends the token', call: () => api.completeGatewayPayment(PaymentTarget.Order, 42, 'tok'), method: HttpMethod.Post, url: '/api/portal/order/42/gateway-payment/complete', body: { paymentToken: 'tok' } },
  { name: 'completeGatewayPayment for an invoice sends an empty body', call: () => api.completeGatewayPayment(PaymentTarget.Invoice, 7), method: HttpMethod.Post, url: '/api/portal/client/invoices/7/gateway-payment/complete', body: {} },
  { name: 'confirmOrderPayment merges the token into the body', call: () => api.confirmOrderPayment(42, { paymentIntentId: 'pi_1' }, 'tok'), method: HttpMethod.Post, url: '/api/portal/order/42/confirm-payment', body: { paymentIntentId: 'pi_1', paymentToken: 'tok' } },
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

  it('maps the camelCase saved methods to the client shape', async () => {
    stub = await startTransport(() => ({
      body: [{ id: 'pm_1', type: PaymentMethodType.Card, brand: 'mastercard', last4: '4444', expiryMonth: 9, expiryYear: 2029, isDefault: true }]
    }))

    const [method] = await api.fetchPaymentMethods()

    expect(method).toMatchObject({ id: 'pm_1', description: 'Mastercard •••• 4444', cardExpiry: '09/29', isDefault: true })
  })

  it('returns the PDF as a blob', async () => {
    stub = await startTransport(() => ({ body: '%PDF-1.4 test', headers: { 'content-type': 'application/pdf' } }))

    const pdf = await api.fetchInvoicePdf('41')

    expect(pdf).toBeInstanceOf(Blob)
    expect(await pdf.text()).toBe('%PDF-1.4 test')
  })
})
