/**
 * Tests for `stores/checkout.ts`, through the real billing API composable, auth, currency, catalogue and cart stores and the transport against a stub server.
 *
 * Both checkout templates place their order through this store, so these assertions pin the
 * whole flow once: the request body, the guest sign-in, the three ways of settling an order and
 * where the payer is sent afterwards, plus how a refusal is kept as state. Nothing of our own is
 * replaced: the stub plays the BFF, so "the API was called with X" is an assertion on the request that reached it.
 *
 * @module tests/stores/checkout.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { nextTick } from 'vue'
import { makeClientUser } from '../helpers/clientUser'
import { bodyOf } from '../helpers/requestBody'
import { refusal, requestsTo, routeTable } from '../helpers/stubRoutes'
import type { StubRouteTable } from '../helpers/stubRoutes'
import type { StubServer } from '../helpers/stubServer'
import { startTransport } from '../helpers/stubTransport'
import { useOrderPaymentToken } from '~/composables/useOrderPaymentToken'
import { useAuthStore } from '~/stores/auth'
import { useCartStore } from '~/stores/cart'
import { useCheckoutStore } from '~/stores/checkout'
import { useCurrencyStore } from '~/stores/currency'
import type { CartItem } from '~/types/cartitem'
import { CheckoutDestinationKind } from '~/types/checkoutdestinationkind'
import { ConsentTextVersion } from '~/types/consenttextversion'
import type { BillingCurrency } from '~/types/currency'
import type { PlaceOrderInput } from '~/types/placeorderinput'
import { PricingCycle } from '~/types/pricingcycle'
import { ProductType } from '~/types/producttype'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies the stub gives, by `METHOD /path`; each test fills the ones it needs. */
let table: StubRouteTable

/** The read of the gateways an order may be paid through. */
const METHODS_ROUTE = 'GET /api/portal/order/payment-methods'

/** The placing of an order. */
const CREATE_ROUTE = 'POST /api/portal/order/create'

/** The path of the start of a hosted-gateway payment for the placed order. */
const START_PATH = '/api/portal/order/42/gateway-payment/start'

/** The start of a hosted-gateway payment for the placed order. */
const START_ROUTE = `POST ${START_PATH}`

/** The choice of a bank transfer on the placed order's invoice. */
const TRANSFER_ROUTE = 'POST /api/portal/client/invoices/77/bank-transfer'

/** The guest's sign-in after the order. */
const LOGIN_ROUTE = 'POST /api/portal/auth/login'

/** The path of the guest's sign-in. */
const LOGIN_PATH = '/api/portal/auth/login'

/** The path of the placing of an order. */
const CREATE_PATH = '/api/portal/order/create'

/** The read of the catalogue the price refresh makes. */
const PRODUCTS_ROUTE = 'GET /api/portal/public/products'

/** The path of the catalogue read. */
const PRODUCTS_PATH = '/api/portal/public/products'

/** The currencies the operator bills in: dollars are the base, so they are the payer's. */
const CURRENCIES: BillingCurrency[] = [
  { code: 'USD', numeric: '840', prefix: '$', suffix: '', decimals: 2, isBase: true },
  { code: 'AMD', numeric: '051', prefix: '', suffix: ' ֏', decimals: 0, isBase: false }
]

/** A hosting item priced in dollars. */
const hosting: CartItem = { pid: 5, name: 'Pro', billingcycle: PricingCycle.Monthly, cycleLabel: 'Monthly', amount: 12, currency: 'USD' }

/** A guest's details. */
const guest = { firstname: 'Ada', lastname: 'L', email: 'ada@example.com', password: 'pw-123456', phonenumber: '+374' }

/** The order the API answers a placed order with. */
const placed = { orderId: 42, orderNumber: 'A 42', invoiceId: 77, paymentToken: 'tok', total: 12, currency: 'USD' }

/** The bank's address the payment start answers with. */
const BANK_URL = 'https://bank.example/pay'

/** The refusal the payment start answers with while the gateway is down. */
const GATEWAY_DOWN = refusal('The gateway is unavailable.')

/**
 * Loads the operator's currencies into the active store, as the app does once it has read them.
 *
 * @returns Nothing.
 */
const loadCurrencies = (): void => {
  useCurrencyStore().currencies = CURRENCIES
}

/**
 * Signs a client in on the real auth store.
 *
 * @returns Nothing.
 */
const signIn = (): void => {
  useAuthStore().user = makeClientUser()
}

/**
 * Builds the input a checkout page hands the store.
 *
 * @param confirmTotalChange - The answer to a changed total; agrees by default.
 * @returns The input.
 */
const input = (
  confirmTotalChange: PlaceOrderInput['confirmTotalChange'] = () => true
): PlaceOrderInput => ({
  guest,
  confirmTotalChange,
  localizePath: path => `/en${path}`
})

/**
 * Creates the store with a cart holding the given items and a chosen payment method.
 *
 * @param method - The gateway module to select.
 * @param items - The cart contents.
 * @returns The checkout store.
 */
const arrange = (method: string, items: CartItem[] = [hosting]) => {
  const cart = useCartStore()
  items.forEach(item => cart.addItem(item))
  const store = useCheckoutStore()
  store.selectedMethod = method
  return store
}

/**
 * The body of the payment start the stub received.
 *
 * @returns The parsed body.
 */
const startBody = (): Record<string, unknown> => bodyOf(requestsTo(stub.requests, START_PATH)[0]) as Record<string, unknown>

/**
 * The keys the browser holds in session storage.
 *
 * @returns The keys, in storage order.
 */
const sessionKeys = (): string[] =>
  Array.from({ length: window.sessionStorage.length }, (_, index) => window.sessionStorage.key(index) ?? '')

beforeEach(async () => {
  setActivePinia(createPinia())
  window.localStorage.clear()
  window.sessionStorage.clear()
  loadCurrencies()
  table = {
    [CREATE_ROUTE]: { body: placed },
    [LOGIN_ROUTE]: { body: { success: true } },
    [TRANSFER_ROUTE]: { body: { invoiceId: 77, reference: 'INV-77' } }
  }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  vi.restoreAllMocks()
  await stub.close()
})

describe('useCheckoutStore.loadPaymentMethods', () => {
  it('keeps the gateways and preselects the first one', async () => {
    table[METHODS_ROUTE] = { body: [{ module: 'card_gateway', displayName: 'Card', supportsStoredCredentials: false }, { module: 'bank_transfer', displayName: 'Bank', supportsStoredCredentials: false }] }
    const store = useCheckoutStore()

    await store.loadPaymentMethods()

    expect(store.paymentMethods).toHaveLength(2)
    expect(store.selectedMethod).toBe('card_gateway')
    expect(store.methodsLoading).toBe(false)
  })

  it('does not override a method the payer already chose', async () => {
    table[METHODS_ROUTE] = { body: [{ module: 'card_gateway', displayName: 'Card', supportsStoredCredentials: false }] }
    const store = useCheckoutStore()
    store.selectedMethod = 'bank_transfer'

    await store.loadPaymentMethods()

    expect(store.selectedMethod).toBe('bank_transfer')
  })

  it('keeps the API sentence and an empty list when the gateways cannot be read', async () => {
    table[METHODS_ROUTE] = refusal('Gateways unavailable.')
    const store = useCheckoutStore()

    await store.loadPaymentMethods()

    expect(store.paymentMethods).toEqual([])
    expect(store.error).toBe('Gateways unavailable.')
  })
})

describe('useCheckoutStore.placeOrder', () => {
  it('does nothing without a chosen method', async () => {
    const store = useCheckoutStore()

    expect(await store.placeOrder(input())).toBeNull()
    expect(requestsTo(stub.requests, CREATE_PATH)).toHaveLength(0)
  })

  it('sends the cart in the payer currency with the guest details and signs the guest in', async () => {
    const store = arrange('bank_transfer', [hosting, { ...hosting, pid: 6, currency: 'AMD' }])

    await store.placeOrder(input())

    const body = bodyOf(requestsTo(stub.requests, CREATE_PATH)[0]) as Record<string, unknown>
    expect((body.items as unknown[]).length).toBe(1)
    expect(body).toMatchObject({ paymentmethod: 'bank_transfer', currency: 'USD', ...guest })
    expect(bodyOf(requestsTo(stub.requests, LOGIN_PATH)[0])).toEqual({ email: 'ada@example.com', password: 'pw-123456' })
  })

  it('sends no guest details and no sign-in for a signed-in client', async () => {
    signIn()
    const store = arrange('bank_transfer')

    await store.placeOrder(input())

    const body = bodyOf(requestsTo(stub.requests, CREATE_PATH)[0]) as Record<string, unknown>
    expect(body.email).toBeUndefined()
    expect(requestsTo(stub.requests, LOGIN_PATH)).toHaveLength(0)
  })

  it('still reports the order as placed when the guest sign-in is refused', async () => {
    table[LOGIN_ROUTE] = { status: 401, body: { detail: 'no' } }
    const store = arrange('bank_transfer')

    const destination = await store.placeOrder(input())

    expect(requestsTo(stub.requests, LOGIN_PATH)).toHaveLength(1)
    expect(destination).toMatchObject({ kind: CheckoutDestinationKind.Route })
    expect(store.error).toBe('')
  })

  it('hands a hosted gateway the localised return address, remembers the token and leaves the site', async () => {
    table[START_ROUTE] = { body: { redirectUrl: BANK_URL } }
    const store = arrange('hosted_gateway')

    const destination = await store.placeOrder(input())

    expect(useOrderPaymentToken().recall(42)).toBe('tok')
    expect(useOrderPaymentToken().recallInvoiceId(42)).toBe(77)
    const returnUrl = startBody().returnUrl as string
    expect(new URL(returnUrl).pathname).toBe('/en/payment/result')
    expect(new URL(returnUrl).search).toBe('?order=42')
    expect(destination).toEqual({ kind: CheckoutDestinationKind.External, target: BANK_URL })
    // Kept until the result page sees the payment confirmed.
    expect(useCartStore().items).toHaveLength(1)
    expect(store.submitting).toBe(true)
  })

  it('refuses a gateway address that is not secure and lets the payer try again', async () => {
    table[START_ROUTE] = { body: { redirectUrl: 'javascript:alert(1)' } }
    const store = arrange('hosted_gateway')

    const destination = await store.placeOrder(input())

    expect(destination).toBeNull()
    expect(store.error).toBe('checkout.errors.generic')
    expect(store.submitting).toBe(false)
    expect(useCartStore().items).toHaveLength(1)
  })

  it('places one order for two concurrent submits and one only', async () => {
    const store = arrange('bank_transfer')

    const [first, second] = await Promise.all([store.placeOrder(input()), store.placeOrder(input())])

    expect(requestsTo(stub.requests, CREATE_PATH)).toHaveLength(1)
    expect([first, second].filter(destination => destination !== null)).toHaveLength(1)
  })

  it('places nothing for an empty cart', async () => {
    const store = arrange('bank_transfer', [])

    expect(await store.placeOrder(input())).toBeNull()
    expect(requestsTo(stub.requests, CREATE_PATH)).toHaveLength(0)
    expect(store.submitting).toBe(false)
  })

  it('keeps the button disabled after a hand-off until the checkout page opens again', async () => {
    const store = arrange('bank_transfer')
    await store.placeOrder(input())
    expect(store.submitting).toBe(true)

    store.resetFlow()

    expect(store.submitting).toBe(false)
  })

  it('sends a bank transfer to the invoice, or to the invoice list when there is none', async () => {
    const withInvoice = await arrange('bank_transfer').placeOrder(input())
    expect(withInvoice).toEqual({ kind: CheckoutDestinationKind.Route, target: '/en/client/invoices/77/pay' })

    setActivePinia(createPinia())
    loadCurrencies()
    table[CREATE_ROUTE] = { body: { ...placed, invoiceId: 0 } }
    const withoutInvoice = await arrange('bank_transfer').placeOrder(input())
    expect(withoutInvoice).toEqual({ kind: CheckoutDestinationKind.Route, target: '/en/client/invoices' })
  })

  it('moves the invoice to waiting for the transfer before sending the payer to it', async () => {
    const store = arrange('bank_transfer')

    const destination = await store.placeOrder(input())

    expect(requestsTo(stub.requests, '/api/portal/client/invoices/77/bank-transfer')).toHaveLength(1)
    expect(bodyOf(requestsTo(stub.requests, '/api/portal/client/invoices/77/bank-transfer')[0])).toEqual({})
    expect(destination).toEqual({ kind: CheckoutDestinationKind.Route, target: '/en/client/invoices/77/pay' })
  })

  it('still sends the payer to the invoice when choosing the transfer fails, where the page offers it again', async () => {
    table[TRANSFER_ROUTE] = refusal('refused')
    const store = arrange('bank_transfer')

    const destination = await store.placeOrder(input())

    expect(destination).toEqual({ kind: CheckoutDestinationKind.Route, target: '/en/client/invoices/77/pay' })
    expect(store.error).toBe('')
  })

  it('does not choose a transfer for an order that has no invoice', async () => {
    table[CREATE_ROUTE] = { body: { ...placed, invoiceId: 0 } }

    await arrange('bank_transfer').placeOrder(input())

    expect(stub.requests.filter(request => request.url.includes('bank-transfer'))).toHaveLength(0)
  })

  it('sends a free order straight to the success page, whatever the method, without paying', async () => {
    table[CREATE_ROUTE] = { body: { ...placed, total: 0 } }
    const store = arrange('hosted_gateway')

    const destination = await store.placeOrder(input())

    expect(requestsTo(stub.requests, START_PATH)).toHaveLength(0)
    expect(destination).toEqual({
      kind: CheckoutDestinationKind.Route,
      target: '/en/client/order-success?order=A+42&invoice=77'
    })
    expect(useCartStore().items).toEqual([])
  })

  it('marks the success link digital when the order held digital goods only', async () => {
    table[CREATE_ROUTE] = { body: { ...placed, total: 0 } }
    const digital: CartItem = { ...hosting, productType: ProductType.Digital }
    const store = arrange('hosted_gateway', [digital])

    const destination = await store.placeOrder(input())

    expect(destination).toEqual({
      kind: CheckoutDestinationKind.Route,
      target: '/en/client/order-success?order=A+42&invoice=77&digital=1'
    })
  })

  it('does not mark the success link digital when the order also held a hosting line', async () => {
    table[CREATE_ROUTE] = { body: { ...placed, total: 0 } }
    const digital: CartItem = { ...hosting, pid: 99, productType: ProductType.Digital }
    const store = arrange('hosted_gateway', [digital, hosting])

    const destination = await store.placeOrder(input())

    expect(destination).toEqual({
      kind: CheckoutDestinationKind.Route,
      target: '/en/client/order-success?order=A+42&invoice=77'
    })
  })

  it('keeps the API sentence and the field messages when the order is refused, and keeps the cart', async () => {
    table[CREATE_ROUTE] = {
      status: 400,
      body: { detail: 'Fix the form.', type: 'VALIDATION_ERROR', errors: { email: ['Email is required.'] } }
    }
    const store = arrange('bank_transfer')

    const destination = await store.placeOrder(input())

    expect(destination).toBeNull()
    expect(store.error).toBe('Email is required.')
    expect(store.fieldErrors).toEqual({ email: 'Email is required.' })
    expect(store.submitting).toBe(false)
    expect(useCartStore().items).toHaveLength(1)
  })

  it('shows the refusal sentence when starting the gateway payment fails, and a generic one when it gave none', async () => {
    const store = arrange('hosted_gateway')

    table[START_ROUTE] = GATEWAY_DOWN
    await store.placeOrder(input())
    expect(store.error).toBe('The gateway is unavailable.')

    // A reply that carries no address to send the payer to says nothing the payer could read.
    table[START_ROUTE] = { body: {} }
    await store.placeOrder(input())
    expect(store.error).toBe('checkout.errors.generic')
  })

  it('pays the same order again on a retry after a failed payment start', async () => {
    table[START_ROUTE] = GATEWAY_DOWN
    const store = arrange('hosted_gateway')

    await store.placeOrder(input())
    table[START_ROUTE] = { body: { redirectUrl: BANK_URL } }
    const destination = await store.placeOrder(input())

    expect(requestsTo(stub.requests, CREATE_PATH)).toHaveLength(1)
    expect(requestsTo(stub.requests, LOGIN_PATH)).toHaveLength(1)
    expect(destination).toMatchObject({ kind: CheckoutDestinationKind.External })
  })

  it('places a new order when the cart changed after a failed payment start', async () => {
    table[START_ROUTE] = GATEWAY_DOWN
    const store = arrange('hosted_gateway')

    await store.placeOrder(input())
    useCartStore().addItem({ ...hosting, pid: 6 })
    await nextTick()

    expect(store.pendingOrder).toBeNull()
    await store.placeOrder(input())
    expect(requestsTo(stub.requests, CREATE_PATH)).toHaveLength(2)
  })

  it('skips the payment and the total confirmation for an order the backend settled as free', async () => {
    table[CREATE_ROUTE] = { body: { ...placed, total: 0 } }
    const confirmTotalChange = vi.fn(() => false)
    const store = arrange('card_gateway')

    const destination = await store.placeOrder(input(confirmTotalChange))

    expect(confirmTotalChange).not.toHaveBeenCalled()
    expect(requestsTo(stub.requests, START_PATH)).toHaveLength(0)
    expect(destination).toEqual({
      kind: CheckoutDestinationKind.Route,
      target: '/en/client/order-success?order=A+42&invoice=77'
    })
    expect(useCartStore().items).toEqual([])
  })
})

describe('useCheckoutStore save for renewals', () => {
  /** A gateway that can keep the method it collects, and ones that cannot. */
  const gateways = [
    { module: 'hosted_gateway', displayName: 'Card', supportsStoredCredentials: true },
    { module: 'plain_gateway', displayName: 'Plain', supportsStoredCredentials: false },
    { module: 'bank_transfer', displayName: 'Transfer', supportsStoredCredentials: false }
  ]

  /**
   * Creates the store with the gateway list loaded and a method chosen.
   *
   * @param method - The gateway module to select.
   * @returns The checkout store.
   */
  const arrangeWithGateways = (method: string) => {
    const store = arrange(method)
    store.paymentMethods = gateways
    return store
  }

  beforeEach(() => {
    signIn()
    table[START_ROUTE] = { body: { redirectUrl: BANK_URL } }
  })

  it('is off by default', () => {
    const store = arrangeWithGateways('hosted_gateway')

    expect(store.saveForRenewals).toBe(false)
  })

  it('is offered to a signed-in client paying through a gateway that supports stored credentials only', () => {
    const store = arrangeWithGateways('hosted_gateway')
    expect(store.canSaveForRenewals).toBe(true)

    store.selectedMethod = 'plain_gateway'
    expect(store.canSaveForRenewals).toBe(false)

    store.selectedMethod = 'bank_transfer'
    expect(store.canSaveForRenewals).toBe(false)
  })

  it('is not offered to a guest', () => {
    useAuthStore().user = null
    const store = arrangeWithGateways('hosted_gateway')

    expect(store.canSaveForRenewals).toBe(false)
  })

  it('sends the choice and the consent version with the payment start when the box was ticked', async () => {
    const store = arrangeWithGateways('hosted_gateway')
    store.saveForRenewals = true

    const destination = await store.placeOrder(input())

    expect(destination).toEqual({ kind: CheckoutDestinationKind.External, target: BANK_URL })
    expect(requestsTo(stub.requests, START_PATH)).toHaveLength(1)
    expect(startBody()).toEqual({
      module: 'hosted_gateway',
      returnUrl: expect.any(String),
      paymentToken: 'tok',
      savePaymentMethod: true,
      consentTextVersion: ConsentTextVersion.CheckoutAgreement
    })
  })

  it('sends no save choice and no consent version when the box was left off', async () => {
    const store = arrangeWithGateways('hosted_gateway')

    await store.placeOrder(input())

    expect(startBody()).toEqual({ module: 'hosted_gateway', returnUrl: expect.any(String), paymentToken: 'tok', savePaymentMethod: false })
  })

  it('clears the choice when the method changes to one that cannot save, and does not send it', async () => {
    const store = arrangeWithGateways('hosted_gateway')
    store.saveForRenewals = true

    store.selectedMethod = 'plain_gateway'

    expect(store.saveForRenewals).toBe(false)
    await store.placeOrder(input())
    expect(startBody()).toEqual({ module: 'plain_gateway', returnUrl: expect.any(String), paymentToken: 'tok', savePaymentMethod: false })
  })

  it('keeps the choice while the method changes to another one that can save', () => {
    const store = arrangeWithGateways('hosted_gateway')
    store.paymentMethods = [...gateways, { module: 'other_gateway', displayName: 'Other', supportsStoredCredentials: true }]
    store.saveForRenewals = true

    store.selectedMethod = 'other_gateway'

    expect(store.saveForRenewals).toBe(true)
  })

  it('remembers the choice nowhere for the return page', async () => {
    const store = arrangeWithGateways('hosted_gateway')
    store.saveForRenewals = true

    await store.placeOrder(input())

    // The only thing kept for the return page is the order's payment token, never the choice.
    expect(sessionKeys()).toEqual(['order-payment-token:42'])
    expect(window.sessionStorage.getItem('order-payment-token:42')).not.toContain('save')
  })
})

describe('useCheckoutStore billed total', () => {
  beforeEach(() => {
    signIn()
  })

  it('pays without asking when the billed total is the one the cart showed', async () => {
    const confirmTotalChange = vi.fn(() => true)

    await arrange('bank_transfer').placeOrder(input(confirmTotalChange))

    expect(confirmTotalChange).not.toHaveBeenCalled()
  })

  it('asks with both amounts when the billed total differs, and goes on when the payer agrees', async () => {
    table[CREATE_ROUTE] = { body: { ...placed, total: 15 } }
    const confirmTotalChange = vi.fn(() => true)

    const destination = await arrange('bank_transfer').placeOrder(input(confirmTotalChange))

    expect(confirmTotalChange).toHaveBeenCalledWith('$12.00', '$15.00')
    expect(destination).toMatchObject({ kind: CheckoutDestinationKind.Route })
  })

  it('asks when the billed currency differs from the payer currency', async () => {
    table[CREATE_ROUTE] = { body: { ...placed, currency: 'EUR' } }
    const confirmTotalChange = vi.fn(() => false)

    await arrange('bank_transfer').placeOrder(input(confirmTotalChange))

    expect(confirmTotalChange).toHaveBeenCalled()
  })

  it('takes no payment and keeps the cart when the payer declines the billed total, and asks again on a retry', async () => {
    table[CREATE_ROUTE] = { body: { ...placed, total: 15 } }
    table[START_ROUTE] = { body: { redirectUrl: BANK_URL } }
    const confirmTotalChange = vi.fn().mockReturnValueOnce(false).mockReturnValueOnce(true)
    const store = arrange('hosted_gateway')

    expect(await store.placeOrder(input(confirmTotalChange))).toBeNull()
    expect(store.error).toBe('checkout.errors.totalNotAccepted')
    expect(store.submitting).toBe(false)
    expect(requestsTo(stub.requests, START_PATH)).toHaveLength(0)
    expect(useCartStore().items).toHaveLength(1)

    await store.placeOrder(input(confirmTotalChange))

    expect(confirmTotalChange).toHaveBeenCalledTimes(2)
    expect(requestsTo(stub.requests, CREATE_PATH)).toHaveLength(1)
    expect(requestsTo(stub.requests, START_PATH)).toHaveLength(1)
  })

  it('does not ask again for an order whose changed total the payer already accepted', async () => {
    table[CREATE_ROUTE] = { body: { ...placed, total: 15 } }
    table[START_ROUTE] = refusal('Try again shortly.')
    const confirmTotalChange = vi.fn(() => true)
    const store = arrange('hosted_gateway')

    await store.placeOrder(input(confirmTotalChange))
    await store.placeOrder(input(confirmTotalChange))

    expect(confirmTotalChange).toHaveBeenCalledTimes(1)
  })
})

describe('useCheckoutStore.refreshCartPrices', () => {
  it('re-prices the cart from the catalogue', async () => {
    table[PRODUCTS_ROUTE] = { body: [{ id: 5, groupId: 1, name: 'Pro', pricing: { monthly: 14, annual: 140 } }] }
    const store = arrange('card_gateway')

    await store.refreshCartPrices()

    expect(useCartStore().items[0]?.amount).toBe(14)
    expect(requestsTo(stub.requests, PRODUCTS_PATH)).toHaveLength(1)
  })

  it('reads nothing for an empty cart or an unknown payer currency', async () => {
    await useCheckoutStore().refreshCartPrices()
    useCurrencyStore().currencies = []
    await arrange('card_gateway').refreshCartPrices()

    expect(requestsTo(stub.requests, PRODUCTS_PATH)).toHaveLength(0)
  })
})

describe('useCheckoutStore cart helpers', () => {
  it('totals only items in the payer currency and flags the others', () => {
    const store = arrange('card_gateway', [hosting, { ...hosting, pid: 6, currency: 'AMD', amount: 5000 }])

    expect(store.totalLabel).toBe('$12.00')
    expect(store.staleItems).toHaveLength(1)
    expect(store.itemPrice(hosting)).toBe('$12.00')
  })

  it('formats an empty cart as zero', () => {
    expect(useCheckoutStore().totalLabel).toBe('$0.00')
  })

  it('labels the submit button for a hosted gateway differently from bank transfer', () => {
    const store = useCheckoutStore()

    store.selectedMethod = 'bank_transfer'
    expect(store.submittingLabel).toBe('checkout.processing')
    store.selectedMethod = 'hosted_gateway'
    expect(store.submittingLabel).toBe('checkout.redirecting')
  })

  it('explains the bank hand-off in a separate hint, only while a hosted gateway order is in flight', () => {
    const store = useCheckoutStore()

    store.selectedMethod = 'hosted_gateway'
    expect(store.submittingHint).toBe('')
    store.submitting = true
    expect(store.submittingHint).toBe('checkout.redirectingToBank')
    store.selectedMethod = 'bank_transfer'
    expect(store.submittingHint).toBe('')
    store.selectedMethod = ''
    expect(store.submittingHint).toBe('')
  })

  it('changes the guest currency only once the visitor accepts an emptied cart', () => {
    const store = arrange('card_gateway')
    const currency = useCurrencyStore()

    expect(store.changeGuestCurrency('USD', () => true)).toBe(false)
    expect(store.changeGuestCurrency('AMD', () => false)).toBe(false)
    expect(currency.selectedCode).toBeNull()
    expect(useCartStore().items).toHaveLength(1)

    expect(store.changeGuestCurrency('AMD', () => true)).toBe(true)
    expect(currency.selectedCode).toBe('AMD')
    expect(useCartStore().items).toEqual([])
  })

  it('changes the currency without asking when the cart is empty', () => {
    const store = useCheckoutStore()
    const ask = vi.fn(() => false)

    expect(store.changeGuestCurrency('AMD', ask)).toBe(true)
    expect(ask).not.toHaveBeenCalled()
  })
})
