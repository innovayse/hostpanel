/**
 * Unit tests for the payment result page against the real invoice payment, cart, auth and stored
 * method stores, API composables, order payment token and transport, with a stub BFF answering on a
 * real socket (nothing of ours is mocked): it verifies a hosted-gateway payment from the address and
 * shows the paid, pending, declined, missing or unknown outcome, never asserting money moved unless
 * the bank said so.
 *
 * @module tests/pages/payment/result.test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import type { Pinia } from 'pinia'
import { useOrderPaymentToken } from '~/composables/useOrderPaymentToken'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import ResultPage from '~/pages/payment/result.vue'
import { useAuthStore } from '~/stores/auth'
import { useCartStore } from '~/stores/cart'
import { ConsentTextVersion } from '~/types/consenttextversion'
import { GatewayPaymentState } from '~/types/gatewaypaymentstate'
import { PricingCycle } from '~/types/pricingcycle'
import { SaveMethodMode } from '~/types/savemethodmode'
import { consumeCheckoutSave, rememberCheckoutSave } from '~/utils/pendingCheckoutSave'
import { ProductType } from '~/types/producttype'

/** The path that settles the payment of order 5. */
const ORDER_COMPLETE = '/api/portal/order/5/gateway-payment/complete'

/** The path that settles the payment of invoice 9. */
const INVOICE_COMPLETE = '/api/portal/client/invoices/9/gateway-payment/complete'

/** The path that opens a stored-method save session. */
const SESSIONS_PATH = '/api/portal/client/stored-payment-methods/sessions'

/** The reply of the session endpoint that sends the payer to the gateway. */
const SESSION_REDIRECT = { body: { sessionId: 's1', mode: SaveMethodMode.Redirect, redirectUrl: 'https://gateway.example/save' } }

/** The reply of a backend that cannot be reached. */
const UNREACHABLE = { status: 503, body: { detail: 'Unavailable.' } }

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test. */
let table: StubRouteTable

/**
 * What the stored-method start writes the redirect to; the browser's location is the one stand-in,
 * so a handed-on redirect is read from it instead of the test page navigating away.
 */
let browserLocation: { origin: string, href: string }

/**
 * Answers the order's settle call with a state.
 *
 * @param state The state the bank reported.
 * @param orderNumber The order number the backend answers with, when it does.
 */
const orderIs = (state: GatewayPaymentState, orderNumber?: string): void => {
  table[`POST ${ORDER_COMPLETE}`] = { body: { state, orderNumber } }
}

/**
 * Answers the invoice's settle call with a state.
 *
 * @param state The state the bank reported.
 */
const invoiceIs = (state: GatewayPaymentState): void => {
  table[`POST ${INVOICE_COMPLETE}`] = { body: { state } }
}

/**
 * The requests that tried to open a stored-method save session.
 *
 * @returns The requests, oldest first.
 */
const sessionRequests = () => requestsTo(stub.requests, SESSIONS_PATH)

/**
 * Mounts the page for one address query and waits until the check has finished.
 *
 * @param query The address query string.
 * @param pinia A Pinia the test filled before mounting; a fresh one when omitted.
 * @returns The wrapper.
 */
const mountResult = async (query: Record<string, string | string[]> = {}, pinia?: Pinia) => {
  stubPublicPageRuntime({ query })
  const wrapper = await mountPage(ResultPage, { pinia })
  await vi.waitFor(() => expect(wrapper.find('[data-testid="payment-verifying"]').exists()).toBe(false))
  await flushPromises()
  return wrapper
}

/** The account that made the checkout choice. */
const ADA = 'ada@example.com'

/**
 * Builds a Pinia in which the given account is already loaded as the signed-in one.
 *
 * @param email The signed-in account's email.
 * @returns The Pinia to mount the page with.
 */
const signedInAs = (email: string): Pinia => {
  const pinia = createPinia()
  setActivePinia(pinia)
  useAuthStore().user = makeClientUser({ email })
  return pinia
}

describe('payment result page', () => {
  beforeEach(async () => {
    window.sessionStorage.clear()
    vi.stubGlobal('useOrderPaymentToken', useOrderPaymentToken)
    browserLocation = { origin: 'https://panel.test', href: '' }
    vi.stubGlobal('location', browserLocation)
    table = {}
    stub = await startTransport(routeTable(table))
    useOrderPaymentToken().remember('5', 'token-1', 77)
  })

  afterEach(async () => {
    await stub.close()
  })

  it('says there is nothing to check, and does not call the backend, when the address names no payment', async () => {
    const wrapper = await mountResult()

    expect(wrapper.find('[data-testid="payment-title"]').text()).toBe('paymentResult.missingTitle')
    expect(wrapper.find('[data-testid="payment-invoices"]').attributes('href')).toBe('/en/client/invoices')
    expect(stub.requests).toHaveLength(0)
  })

  it('verifies an order with its payment token and offers the order page once paid', async () => {
    orderIs(GatewayPaymentState.Paid, 'INV-77')

    const wrapper = await mountResult({ order: '5' })

    const [sent] = requestsTo(stub.requests, ORDER_COMPLETE)
    expect(bodyOf(sent)).toEqual({ paymentToken: 'token-1' })
    expect(wrapper.find('[data-testid="payment-title"]').text()).toBe('paymentResult.successTitle')
    expect(wrapper.find('[data-testid="payment-continue"]').attributes('href')).toBe('/en/client/order-success?order=INV-77')
    expect(useOrderPaymentToken().recall('5')).toBeUndefined()
  })

  it('verifies an invoice without a token and offers the invoice once paid', async () => {
    invoiceIs(GatewayPaymentState.Paid)

    const wrapper = await mountResult({ invoice: '9' })

    const [sent] = requestsTo(stub.requests, INVOICE_COMPLETE)
    expect(bodyOf(sent)).toEqual({})
    expect(wrapper.find('[data-testid="payment-continue"]').attributes('href')).toBe('/en/client/invoices/9')
  })

  it('keeps the token and lets the payer check again while the bank has not settled', async () => {
    orderIs(GatewayPaymentState.Pending)
    const wrapper = await mountResult({ order: '5' })
    expect(wrapper.find('[data-testid="payment-title"]').text()).toBe('paymentResult.pendingTitle')
    expect(useOrderPaymentToken().recall('5')).toBe('token-1')

    orderIs(GatewayPaymentState.Paid)
    await wrapper.find('[data-testid="payment-retry"]').trigger('click')
    await vi.waitFor(() => expect(wrapper.find('[data-testid="payment-title"]').text()).toBe('paymentResult.successTitle'))

    expect(requestsTo(stub.requests, ORDER_COMPLETE)).toHaveLength(2)
  })

  it('sends a declined payer to pay the same invoice again, never to a fresh cart', async () => {
    orderIs(GatewayPaymentState.Declined)
    invoiceIs(GatewayPaymentState.Declined)

    const order = await mountResult({ order: '5' })
    expect(order.find('[data-testid="payment-try-again"]').attributes('href')).toBe('/en/client/invoices/77/pay')

    const invoice = await mountResult({ invoice: '9' })
    expect(invoice.find('[data-testid="payment-try-again"]').attributes('href')).toBe('/en/client/invoices/9/pay')
  })

  it('sends a declined order payer to the invoice list when the billed invoice is not remembered', async () => {
    orderIs(GatewayPaymentState.Declined)
    useOrderPaymentToken().forget('5')

    const order = await mountResult({ order: '5' })

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

    orderIs(GatewayPaymentState.Declined)
    const declinedPinia = filledPinia()
    await mountResult({ order: '5' }, declinedPinia)
    expect(useCartStore(declinedPinia).items).toHaveLength(1)

    orderIs(GatewayPaymentState.Paid, 'ORD-5')
    const paidPinia = filledPinia()
    await mountResult({ order: '5' }, paidPinia)
    expect(useCartStore(paidPinia).items).toEqual([])
  })

  it('offers the order page of digital goods, which asks for no hosting setup, when the paid cart held only those', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    useCartStore().items = [{ pid: 1, name: 'Credits', billingcycle: PricingCycle.Monthly, cycleLabel: 'Monthly', amount: 5, currency: 'USD', productType: ProductType.Digital }]
    orderIs(GatewayPaymentState.Paid, 'ORD-5')

    const wrapper = await mountResult({ order: '5' }, pinia)

    expect(wrapper.find('[data-testid="payment-continue"]').attributes('href')).toBe('/en/client/order-success?order=ORD-5&digital=1')
  })

  it('treats a repeated or malformed id as missing and does not call the backend', async () => {
    /** Queries whose order or invoice id is repeated or malformed. */
    const queries: Array<Record<string, string | string[]>> = [{ order: ['1', '2'] }, { invoice: ['3', '4'] }, { order: '../x' }, { order: '5', invoice: ['9', '10'] }]
    for (const query of queries) {
      const wrapper = await mountResult(query)

      expect(wrapper.find('[data-testid="payment-title"]').text()).toBe('paymentResult.missingTitle')
    }
    expect(stub.requests).toHaveLength(0)
  })

  it('does not assert an outcome when the backend cannot be reached, and keeps the token', async () => {
    rememberCheckoutSave('5', 'card_gateway', ADA)
    table[`POST ${ORDER_COMPLETE}`] = UNREACHABLE

    const wrapper = await mountResult({ order: '5' }, signedInAs(ADA))

    expect(wrapper.find('[data-testid="payment-title"]').text()).toBe('paymentResult.unknownTitle')
    expect(wrapper.find('[data-testid="payment-status"]').exists()).toBe(true)
    expect(useOrderPaymentToken().recall('5')).toBe('token-1')
    // Nothing was confirmed or declined, so the renewal-save choice is neither acted on nor dropped.
    expect(sessionRequests()).toHaveLength(0)
    expect(consumeCheckoutSave('5', ADA)).toBe('card_gateway')
  })

  describe('save for automatic renewals', () => {
    it('starts the stored-method session once the payment is confirmed and the payer asked for it', async () => {
      rememberCheckoutSave('5', 'card_gateway', ADA)
      table[`POST ${SESSIONS_PATH}`] = SESSION_REDIRECT
      orderIs(GatewayPaymentState.Paid, 'ORD-5')

      const wrapper = await mountResult({ order: '5' }, signedInAs(ADA))
      await vi.waitFor(() => expect(sessionRequests()).toHaveLength(1))
      await flushPromises()

      expect(bodyOf(sessionRequests()[0])).toMatchObject({ gatewayModuleId: 'card_gateway', consentAccepted: true, consentTextVersion: ConsentTextVersion.CheckoutAgreement })
      expect(wrapper.find('[data-testid="payment-save-starting"]').exists()).toBe(true)
      expect(wrapper.find('[data-testid="payment-save-failed"]').exists()).toBe(false)
      await vi.waitFor(() => expect(browserLocation.href).toBe('https://gateway.example/save'))
    })

    it('starts nothing, and drops the choice, when another account opens the page on the same browser', async () => {
      rememberCheckoutSave('5', 'card_gateway', ADA)
      orderIs(GatewayPaymentState.Paid, 'ORD-5')

      const wrapper = await mountResult({ order: '5' }, signedInAs('bob@example.com'))

      expect(sessionRequests()).toHaveLength(0)
      expect(wrapper.find('[data-testid="payment-save-starting"]').exists()).toBe(false)
      expect(consumeCheckoutSave('5', ADA)).toBeNull()
    })

    it('starts nothing when the payer did not ask for it', async () => {
      orderIs(GatewayPaymentState.Paid, 'ORD-5')

      await mountResult({ order: '5' }, signedInAs(ADA))

      expect(sessionRequests()).toHaveLength(0)
    })

    it('starts one session only, even when the paid page is opened again', async () => {
      rememberCheckoutSave('5', 'card_gateway', ADA)
      table[`POST ${SESSIONS_PATH}`] = SESSION_REDIRECT
      orderIs(GatewayPaymentState.Paid, 'ORD-5')

      await mountResult({ order: '5' }, signedInAs(ADA))
      await vi.waitFor(() => expect(sessionRequests()).toHaveLength(1))
      await mountResult({ order: '5' }, signedInAs(ADA))

      expect(sessionRequests()).toHaveLength(1)
    })

    it('does not start on a declined payment, and drops the choice', async () => {
      rememberCheckoutSave('5', 'card_gateway', ADA)
      orderIs(GatewayPaymentState.Declined)

      await mountResult({ order: '5' }, signedInAs(ADA))

      expect(sessionRequests()).toHaveLength(0)
      expect(consumeCheckoutSave('5', ADA)).toBeNull()
    })

    it('does not start on a pending payment, and keeps the choice for the next check', async () => {
      rememberCheckoutSave('5', 'card_gateway', ADA)
      orderIs(GatewayPaymentState.Pending)

      await mountResult({ order: '5' }, signedInAs(ADA))

      expect(sessionRequests()).toHaveLength(0)
      expect(consumeCheckoutSave('5', ADA)).toBe('card_gateway')
    })

    it('does not start when the outcome could not be confirmed', async () => {
      rememberCheckoutSave('5', 'card_gateway', ADA)
      table[`POST ${ORDER_COMPLETE}`] = UNREACHABLE

      await mountResult({ order: '5' }, signedInAs(ADA))

      expect(sessionRequests()).toHaveLength(0)
    })

    it('starts nothing for an invoice payment', async () => {
      rememberCheckoutSave('9', 'card_gateway', ADA)
      invoiceIs(GatewayPaymentState.Paid)

      await mountResult({ invoice: '9' }, signedInAs(ADA))

      expect(sessionRequests()).toHaveLength(0)
    })

    it('keeps the paid result and shows a notice with a link to the saved methods when the session cannot be started', async () => {
      rememberCheckoutSave('5', 'card_gateway', ADA)
      table[`POST ${SESSIONS_PATH}`] = { status: 400, body: { detail: 'Not supported.' } }
      orderIs(GatewayPaymentState.Paid, 'ORD-5')

      const wrapper = await mountResult({ order: '5' }, signedInAs(ADA))
      await vi.waitFor(() => expect(wrapper.find('[data-testid="payment-save-failed"]').exists()).toBe(true))

      expect(wrapper.find('[data-testid="payment-title"]').text()).toBe('paymentResult.successTitle')
      expect(wrapper.find('[data-testid="payment-continue"]').exists()).toBe(true)
      expect(wrapper.find('[data-testid="payment-save-failed-link"]').attributes('href')).toBe('/en/client/account?tab=payment')
      expect(wrapper.find('[data-testid="payment-save-starting"]').exists()).toBe(false)
    })
  })
})
