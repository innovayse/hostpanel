/**
 * Tests for the payment result page against the real invoice payment store, billing API composable,
 * order payment token memory and transport, with a stub BFF answering on a real socket (nothing of
 * ours is mocked; the token lives in the browser's own session storage): it verifies a
 * hosted-gateway payment from the address and shows the paid, pending, declined, missing or unknown
 * outcome, never asserting money moved unless the bank said so.
 *
 * @module tests/pages/payment/result.test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { Pinia } from 'pinia'
import { invoicePaymentCompletePath, orderPaymentCompletePath } from '~/tests/helpers/billingPaths'
import { mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { makeBackendUnreachable } from '~/tests/helpers/unreachableTransport'
import { waitUntil } from '~/tests/helpers/waitUntil'
import ResultPage from '~/pages/payment/result.vue'
import { useOrderPaymentToken } from '~/composables/useOrderPaymentToken'
import { useCartStore } from '~/stores/cart'
import { GatewayPaymentState } from '~/types/gatewaypaymentstate'
import { HttpMethod } from '~/types/httpmethod'
import { PricingCycle } from '~/types/pricingcycle'
import { ProductType } from '~/types/producttype'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/** The order the payer is sent back for, and the invoice it was billed on. */
const ORDER = { id: 5, invoiceId: 77, token: 'token-1' } as const

/**
 * The key of the stub reply to an order's payment check.
 *
 * @param orderId - The order id.
 * @returns The route table key.
 */
const orderCheck = (orderId: number): string => `${HttpMethod.Post} ${orderPaymentCompletePath(orderId)}`

/**
 * The key of the stub reply to an invoice's payment check.
 *
 * @param invoiceId - The invoice id.
 * @returns The route table key.
 */
const invoiceCheck = (invoiceId: number): string => `${HttpMethod.Post} ${invoicePaymentCompletePath(invoiceId)}`

/**
 * Puts the payment token of the order in the browser, as checkout does before it leaves for the bank.
 *
 * @returns Nothing.
 */
const rememberOrder = (): void => {
  useOrderPaymentToken().remember(ORDER.id, ORDER.token, ORDER.invoiceId)
}

/** The heading each outcome of the check renders. */
enum ResultTitle {
  /** The payment is confirmed. */
  Success = 'paymentResult.successTitle',
  /** The bank has not settled yet. */
  Pending = 'paymentResult.pendingTitle',
  /** The bank declined the payment. */
  Declined = 'paymentResult.declinedTitle',
  /** The address names no payment to check. */
  Missing = 'paymentResult.missingTitle',
  /** The backend could not give an answer. */
  Unknown = 'paymentResult.unknownTitle'
}

/**
 * Mounts the page for one address query and waits until the expected outcome is rendered.
 *
 * @param title The heading of the outcome the check must end in.
 * @param query The address query string.
 * @param pinia A Pinia the test filled before mounting; a fresh one when omitted.
 * @returns The wrapper.
 */
const mountResult = async (title: ResultTitle, query: Record<string, string | string[]> = {}, pinia?: Pinia) => {
  stubPublicPageRuntime({ query })
  const wrapper = await mountPage(ResultPage, { pinia })
  await waitUntil(() => expect(wrapper.find('[data-testid="payment-title"]').text()).toBe(title))
  return wrapper
}

/**
 * How many payment checks reached the stub, for either target.
 *
 * @returns The count.
 */
const checks = (): number => stub.requests.filter(request => request.url.endsWith('/gateway-payment/complete')).length

describe('payment result page', () => {
  beforeEach(async () => {
    window.sessionStorage.clear()
    vi.stubGlobal('useOrderPaymentToken', useOrderPaymentToken)
    rememberOrder()
    table = {}
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('says there is nothing to check, and does not call the backend, when the address names no payment', async () => {
    const wrapper = await mountResult(ResultTitle.Missing)

    expect(wrapper.find('[data-testid="payment-title"]').text()).toBe(ResultTitle.Missing)
    expect(wrapper.find('[data-testid="payment-invoices"]').attributes('href')).toBe('/en/client/invoices')
    expect(checks()).toBe(0)
  })

  it('verifies an order with its payment token and offers the order page once paid', async () => {
    table[orderCheck(ORDER.id)] = { body: { state: GatewayPaymentState.Paid, orderNumber: 'INV-77' } }

    const wrapper = await mountResult(ResultTitle.Success, { order: '5' })

    expect(bodyOf(requestsTo(stub.requests, orderPaymentCompletePath(ORDER.id))[0])).toEqual({ paymentToken: ORDER.token })
    expect(wrapper.find('[data-testid="payment-title"]').text()).toBe(ResultTitle.Success)
    expect(wrapper.find('[data-testid="payment-continue"]').attributes('href')).toBe('/en/client/order-success?order=INV-77')
    expect(useOrderPaymentToken().recall(ORDER.id)).toBeUndefined()
  })

  it('verifies an invoice without a token and offers the invoice once paid', async () => {
    table[invoiceCheck(9)] = { body: { state: GatewayPaymentState.Paid } }

    const wrapper = await mountResult(ResultTitle.Success, { invoice: '9' })

    expect(bodyOf(requestsTo(stub.requests, invoicePaymentCompletePath(9))[0])).toEqual({})
    expect(wrapper.find('[data-testid="payment-continue"]').attributes('href')).toBe('/en/client/invoices/9')
  })

  it('keeps the token and lets the payer check again while the bank has not settled', async () => {
    table[orderCheck(ORDER.id)] = { body: { state: GatewayPaymentState.Pending } }
    const wrapper = await mountResult(ResultTitle.Pending, { order: '5' })
    expect(wrapper.find('[data-testid="payment-title"]').text()).toBe(ResultTitle.Pending)
    expect(useOrderPaymentToken().recall(ORDER.id)).toBe(ORDER.token)

    table[orderCheck(ORDER.id)] = { body: { state: GatewayPaymentState.Paid } }
    await wrapper.find('[data-testid="payment-retry"]').trigger('click')
    await waitUntil(() => expect(wrapper.find('[data-testid="payment-title"]').text()).toBe(ResultTitle.Success))

    expect(requestsTo(stub.requests, orderPaymentCompletePath(ORDER.id))).toHaveLength(2)
  })

  it('sends a declined payer to pay the same invoice again, never to a fresh cart', async () => {
    table[orderCheck(ORDER.id)] = { body: { state: GatewayPaymentState.Declined } }
    table[invoiceCheck(9)] = { body: { state: GatewayPaymentState.Declined } }

    const order = await mountResult(ResultTitle.Declined, { order: '5' })
    expect(order.find('[data-testid="payment-try-again"]').attributes('href')).toBe('/en/client/invoices/77/pay')

    const invoice = await mountResult(ResultTitle.Declined, { invoice: '9' })
    expect(invoice.find('[data-testid="payment-try-again"]').attributes('href')).toBe('/en/client/invoices/9/pay')
  })

  it('sends a declined order payer to the invoice list when the billed invoice is not remembered', async () => {
    table[orderCheck(ORDER.id)] = { body: { state: GatewayPaymentState.Declined } }
    useOrderPaymentToken().forget(ORDER.id)

    const order = await mountResult(ResultTitle.Declined, { order: '5' })

    expect(order.find('[data-testid="payment-try-again"]').attributes('href')).toBe('/en/client/invoices')
  })

  it('empties the cart only once an order payment is confirmed', async () => {
    const cartItem = { pid: 1, name: 'Plan', billingcycle: PricingCycle.Monthly, cycleLabel: 'Monthly', amount: 5, currency: 'USD' }

    /**
     * Builds a Pinia whose cart already holds one line.
     *
     * @returns The active Pinia.
     */
    const filledPinia = (): Pinia => {
      const pinia = createPinia()
      setActivePinia(pinia)
      useCartStore().items = [cartItem]
      return pinia
    }

    table[orderCheck(ORDER.id)] = { body: { state: GatewayPaymentState.Declined } }
    const declinedPinia = filledPinia()
    await mountResult(ResultTitle.Declined, { order: '5' }, declinedPinia)
    expect(useCartStore(declinedPinia).items).toHaveLength(1)

    table[orderCheck(ORDER.id)] = { body: { state: GatewayPaymentState.Paid, orderNumber: 'ORD-5' } }
    rememberOrder()
    const paidPinia = filledPinia()
    await mountResult(ResultTitle.Success, { order: '5' }, paidPinia)
    expect(useCartStore(paidPinia).items).toEqual([])
  })

  it('offers the order page of digital goods, which asks for no hosting setup, when the paid cart held only those', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    useCartStore().items = [{ pid: 1, name: 'Credits', billingcycle: PricingCycle.Monthly, cycleLabel: 'Monthly', amount: 5, currency: 'USD', productType: ProductType.Digital }]
    table[orderCheck(ORDER.id)] = { body: { state: GatewayPaymentState.Paid, orderNumber: 'ORD-5' } }

    const wrapper = await mountResult(ResultTitle.Success, { order: '5' }, pinia)

    expect(wrapper.find('[data-testid="payment-continue"]').attributes('href')).toBe('/en/client/order-success?order=ORD-5&digital=1')
  })

  it('treats a repeated or malformed id as missing and does not call the backend', async () => {
    /** Queries whose order or invoice id is repeated or malformed. */
    const queries: Array<Record<string, string | string[]>> = [{ order: ['1', '2'] }, { invoice: ['3', '4'] }, { order: '../x' }, { order: '5', invoice: ['9', '10'] }]
    for (const query of queries) {
      const wrapper = await mountResult(ResultTitle.Missing, query)

      expect(wrapper.find('[data-testid="payment-title"]').text()).toBe(ResultTitle.Missing)
    }
    expect(checks()).toBe(0)
  })

  it('does not assert an outcome when the backend cannot be reached, and keeps the token', async () => {
    await makeBackendUnreachable()

    const wrapper = await mountResult(ResultTitle.Unknown, { order: '5' })

    expect(wrapper.find('[data-testid="payment-title"]').text()).toBe(ResultTitle.Unknown)
    expect(wrapper.find('[data-testid="payment-status"]').exists()).toBe(true)
    expect(useOrderPaymentToken().recall(ORDER.id)).toBe(ORDER.token)
  })
})
