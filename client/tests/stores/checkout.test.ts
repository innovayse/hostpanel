/**
 * Tests for `stores/checkout.ts`, through the real billing and auth API composables, the real auth, cart,
 * currency, catalogue and client stores and the transport against a stub server.
 *
 * Both checkout templates place their order through this store, so these assertions pin the
 * whole flow once: the request body, the guest sign-in, the three ways of settling an order and
 * where the payer is sent afterwards, plus how a refusal is kept as state. Nothing of our own is
 * replaced: the stub plays the BFF, the payer is the guest of an operator billing in dollars and
 * drams, and a signed-in client is one whose `authed` cookie is set.
 *
 * @module tests/stores/checkout.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { nextTick, ref } from 'vue'
import { requestsTo, routeTable } from '../helpers/stubRoutes'
import type { StubRouteTable } from '../helpers/stubRoutes'
import type { StubReply, StubServer } from '../helpers/stubServer'
import { startTransport } from '../helpers/stubTransport'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { useOrderPaymentToken } from '~/composables/useOrderPaymentToken'
import { useCartStore } from '~/stores/cart'
import { useCheckoutStore } from '~/stores/checkout'
import { useCurrencyStore } from '~/stores/currency'
import type { CartItem } from '~/types/cartitem'
import { CheckoutDestinationKind } from '~/types/checkoutdestinationkind'
import type { BillingCurrency } from '~/types/currency'
import { HttpMethod } from '~/types/httpmethod'
import type { PlaceOrderInput } from '~/types/placeorderinput'
import { PortalErrorCode } from '~/constants/portalErrorCode'
import { PaymentModule } from '~/types/paymentmodule'
import { PricingCycle } from '~/types/pricingcycle'
import { ProductType } from '~/types/producttype'
import { consumeCheckoutSave } from '~/utils/pendingCheckoutSave'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies the stub gives, by `METHOD /path`; the top-level setup fills the happy ones and each test overrides what it needs. */
let table: StubRouteTable

/** The hosted gateway module the tests pay through. */
const HOSTED = 'hosted_gateway'

/** The order creation. */
const CREATE_PATH = '/api/portal/order/create'

/** The hosted-gateway start of the placed order. */
const START_PATH = '/api/portal/order/42/gateway-payment/start'

/** The guest sign-in. */
const LOGIN_PATH = '/api/portal/auth/login'

/** The choice of the bank transfer of the placed invoice. */
const TRANSFER_PATH = '/api/portal/client/invoices/77/bank-transfer'

/** The identity read. */
const ME_ROUTE = `${HttpMethod.Get} /api/portal/client/me`

/** The catalogue read the price refresh makes. */
const PRODUCTS_ROUTE = `${HttpMethod.Get} /api/portal/public/products`

/** The base currency of the operator, whose money shape the totals are formatted in. */
const usd: BillingCurrency = { code: 'USD', numeric: '840', prefix: '$', suffix: '', decimals: 2, isBase: true }

/** A second currency a guest can switch to. */
const amd: BillingCurrency = { code: 'AMD', numeric: '051', prefix: '', suffix: ' AMD', decimals: 0, isBase: false }

/** A hosting item priced in dollars. */
const hosting: CartItem = { pid: 5, name: 'Pro', billingcycle: PricingCycle.Monthly, cycleLabel: 'Monthly', amount: 12, currency: 'USD' }

/** A guest's details. */
const guest = { firstname: 'Ada', lastname: 'L', email: 'ada@example.com', password: 'pw-123456', phonenumber: '+374' }

/** The order the API answers a placed order with. */
const placed = { orderId: 42, orderNumber: 'A 42', invoiceId: 77, paymentToken: 'tok', total: 12, currency: 'USD' }

/** The account the guest sign-in and the identity read answer with. */
const ada = makeClientUser({ email: 'ada@example.com' })

/** A refusal that carries the sentence of the gateway. */
const GATEWAY_DOWN: StubReply = { status: 400, body: { detail: 'The gateway is unavailable.' } }

/**
 * Makes the payer the guest of an operator billing in dollars and drams: the currency store holds the
 * list and the payer pays in the base currency until a test selects another.
 */
const seedCurrencies = (): void => {
  useCurrencyStore().currencies = [usd, amd]
}

/**
 * Sets the readable `authed` cookie the auth store reads, which is what makes a visitor a signed-in client.
 * Called before the stores are created.
 *
 * @param value - `'1'` for a signed-in client, null for a guest.
 */
const setAuthedCookie = (value: string | null): void => {
  vi.stubGlobal('useCookie', () => ref<string | null>(value))
}

/**
 * The JSON body of the order creation the stub received.
 *
 * @returns The body, as the store sent it.
 */
const createBody = (): Record<string, unknown> =>
  JSON.parse(requestsTo(stub.requests, CREATE_PATH)[0]?.body ?? '{}') as Record<string, unknown>

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

beforeEach(async () => {
  setActivePinia(createPinia())
  seedCurrencies()
  window.sessionStorage.clear()
  table = {
    [`${HttpMethod.Post} ${CREATE_PATH}`]: { body: placed },
    [`${HttpMethod.Post} ${LOGIN_PATH}`]: { body: ada },
    [`${HttpMethod.Post} ${START_PATH}`]: { body: { redirectUrl: 'https://bank.example/pay' } },
    [`${HttpMethod.Post} ${TRANSFER_PATH}`]: { body: { invoiceId: 77, reference: 'INV-77' } },
    [ME_ROUTE]: { body: ada }
  }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  vi.unstubAllGlobals()
  window.localStorage.clear()
  window.sessionStorage.clear()
  await stub.close()
})

describe('useCheckoutStore.loadPaymentMethods', () => {
  it('keeps the gateways and preselects the first one', async () => {
    table[`${HttpMethod.Get} /api/portal/order/payment-methods`] = { body: [{ module: 'card_gateway', displayName: 'Card' }, { module: PaymentModule.BankTransfer, displayName: 'Bank' }] }
    const store = useCheckoutStore()

    await store.loadPaymentMethods()

    expect(store.paymentMethods).toHaveLength(2)
    expect(store.selectedMethod).toBe('card_gateway')
    expect(store.methodsLoading).toBe(false)
  })

  it('does not override a method the payer already chose', async () => {
    table[`${HttpMethod.Get} /api/portal/order/payment-methods`] = { body: [{ module: 'card_gateway', displayName: 'Card' }] }
    const store = useCheckoutStore()
    store.selectedMethod = PaymentModule.BankTransfer

    await store.loadPaymentMethods()

    expect(store.selectedMethod).toBe(PaymentModule.BankTransfer)
  })

  it('keeps the API sentence and an empty list when the gateways cannot be read', async () => {
    table[`${HttpMethod.Get} /api/portal/order/payment-methods`] = { status: 400, body: { detail: 'Gateways unavailable.' } }
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
    const store = arrange(PaymentModule.BankTransfer, [hosting, { ...hosting, pid: 6, currency: 'AMD' }])

    await store.placeOrder(input())

    const body = createBody()
    expect((body.items as unknown[]).length).toBe(1)
    expect(body).toMatchObject({ paymentmethod: PaymentModule.BankTransfer, currency: 'USD', ...guest })
    expect(JSON.parse(requestsTo(stub.requests, LOGIN_PATH)[0]?.body ?? '{}')).toEqual({ email: 'ada@example.com', password: 'pw-123456' })
  })

  it('sends no guest details and no sign-in for a signed-in client', async () => {
    setAuthedCookie('1')
    const store = arrange(PaymentModule.BankTransfer)

    await store.placeOrder(input())

    expect(createBody().email).toBeUndefined()
    expect(requestsTo(stub.requests, LOGIN_PATH)).toHaveLength(0)
  })

  it('still reports the order as placed when the guest sign-in is refused', async () => {
    table[`${HttpMethod.Post} ${LOGIN_PATH}`] = { status: 401, body: { detail: 'Wrong email or password.' } }
    const store = arrange(PaymentModule.BankTransfer)

    const destination = await store.placeOrder(input())

    expect(destination).toMatchObject({ kind: CheckoutDestinationKind.Route })
    expect(store.error).toBe('')
  })

  it('hands a hosted gateway the localised return address, remembers the token and leaves the site', async () => {
    const store = arrange(HOSTED)

    const destination = await store.placeOrder(input())

    expect(useOrderPaymentToken().recall(42)).toBe('tok')
    expect(useOrderPaymentToken().recallInvoiceId(42)).toBe(77)
    const started = JSON.parse(requestsTo(stub.requests, START_PATH)[0]?.body ?? '{}') as Record<string, string>
    expect(started).toMatchObject({ module: HOSTED, paymentToken: 'tok' })
    expect(new URL(started.returnUrl ?? '').pathname).toBe('/en/payment/result')
    expect(new URL(started.returnUrl ?? '').search).toBe('?order=42')
    expect(destination).toEqual({ kind: CheckoutDestinationKind.External, target: 'https://bank.example/pay' })
    // Kept until the result page sees the payment confirmed.
    expect(useCartStore().items).toHaveLength(1)
    expect(store.submitting).toBe(true)
  })

  it('refuses a gateway address that is not secure and lets the payer try again', async () => {
    table[`${HttpMethod.Post} ${START_PATH}`] = { body: { redirectUrl: 'javascript:alert(1)' } }
    const store = arrange(HOSTED)

    const destination = await store.placeOrder(input())

    expect(destination).toBeNull()
    expect(store.error).toBe('checkout.errors.generic')
    expect(store.submitting).toBe(false)
    expect(useCartStore().items).toHaveLength(1)
  })

  it('places one order for two concurrent submits and one only', async () => {
    const store = arrange(PaymentModule.BankTransfer)

    const [first, second] = await Promise.all([store.placeOrder(input()), store.placeOrder(input())])

    expect(requestsTo(stub.requests, CREATE_PATH)).toHaveLength(1)
    expect([first, second].filter(destination => destination !== null)).toHaveLength(1)
  })

  it('places nothing for an empty cart', async () => {
    const store = arrange(PaymentModule.BankTransfer, [])

    expect(await store.placeOrder(input())).toBeNull()
    expect(requestsTo(stub.requests, CREATE_PATH)).toHaveLength(0)
    expect(store.submitting).toBe(false)
  })

  it('keeps the button disabled after a hand-off until the checkout page opens again', async () => {
    const store = arrange(PaymentModule.BankTransfer)
    await store.placeOrder(input())
    expect(store.submitting).toBe(true)

    store.resetFlow()

    expect(store.submitting).toBe(false)
  })

  it('sends a bank transfer to the invoice, or to the invoice list when there is none', async () => {
    const withInvoice = await arrange(PaymentModule.BankTransfer).placeOrder(input())
    expect(withInvoice).toEqual({ kind: CheckoutDestinationKind.Route, target: '/en/client/invoices/77/pay' })

    setActivePinia(createPinia())
    seedCurrencies()
    table[`${HttpMethod.Post} ${CREATE_PATH}`] = { body: { ...placed, invoiceId: 0 } }
    const withoutInvoice = await arrange(PaymentModule.BankTransfer).placeOrder(input())
    expect(withoutInvoice).toEqual({ kind: CheckoutDestinationKind.Route, target: '/en/client/invoices' })
  })

  it('moves the invoice to waiting for the transfer before sending the payer to it', async () => {
    const store = arrange(PaymentModule.BankTransfer)

    const destination = await store.placeOrder(input())

    expect(requestsTo(stub.requests, TRANSFER_PATH).map(request => request.method)).toEqual([HttpMethod.Post])
    expect(destination).toEqual({ kind: CheckoutDestinationKind.Route, target: '/en/client/invoices/77/pay' })
  })

  it('still sends the payer to the invoice when choosing the transfer fails, where the page offers it again', async () => {
    table[`${HttpMethod.Post} ${TRANSFER_PATH}`] = { status: 400, body: { detail: 'Refused.' } }
    const store = arrange(PaymentModule.BankTransfer)

    const destination = await store.placeOrder(input())

    expect(destination).toEqual({ kind: CheckoutDestinationKind.Route, target: '/en/client/invoices/77/pay' })
    expect(store.error).toBe('')
  })

  it('does not choose a transfer for an order that has no invoice', async () => {
    table[`${HttpMethod.Post} ${CREATE_PATH}`] = { body: { ...placed, invoiceId: 0 } }

    await arrange(PaymentModule.BankTransfer).placeOrder(input())

    expect(stub.requests.filter(request => request.url.includes('bank-transfer'))).toHaveLength(0)
  })

  it('sends a free order straight to the success page, whatever the method, without paying', async () => {
    table[`${HttpMethod.Post} ${CREATE_PATH}`] = { body: { ...placed, total: 0 } }
    const store = arrange(HOSTED)

    const destination = await store.placeOrder(input())

    expect(requestsTo(stub.requests, START_PATH)).toHaveLength(0)
    expect(destination).toEqual({
      kind: CheckoutDestinationKind.Route,
      target: '/en/client/order-success?order=A+42&invoice=77'
    })
    expect(useCartStore().items).toEqual([])
  })

  it('marks the success link digital when the order held digital goods only', async () => {
    table[`${HttpMethod.Post} ${CREATE_PATH}`] = { body: { ...placed, total: 0 } }
    const digital: CartItem = { ...hosting, productType: ProductType.Digital }
    const store = arrange(HOSTED, [digital])

    const destination = await store.placeOrder(input())

    expect(destination).toEqual({
      kind: CheckoutDestinationKind.Route,
      target: '/en/client/order-success?order=A+42&invoice=77&digital=1'
    })
  })

  it('does not mark the success link digital when the order also held a hosting line', async () => {
    table[`${HttpMethod.Post} ${CREATE_PATH}`] = { body: { ...placed, total: 0 } }
    const digital: CartItem = { ...hosting, pid: 99, productType: ProductType.Digital }
    const store = arrange(HOSTED, [digital, hosting])

    const destination = await store.placeOrder(input())

    expect(destination).toEqual({
      kind: CheckoutDestinationKind.Route,
      target: '/en/client/order-success?order=A+42&invoice=77'
    })
  })

  it('keeps the API sentence and the field messages when the order is refused, and keeps the cart', async () => {
    table[`${HttpMethod.Post} ${CREATE_PATH}`] = {
      status: 400,
      body: { detail: 'Fix the form.', type: 'VALIDATION_ERROR', errors: { email: ['Email is required.'] } }
    }
    const store = arrange(PaymentModule.BankTransfer)

    const destination = await store.placeOrder(input())

    expect(destination).toBeNull()
    expect(store.error).toBe('Email is required.')
    expect(store.fieldErrors).toEqual({ email: 'Email is required.' })
    expect(store.submitting).toBe(false)
    expect(useCartStore().items).toHaveLength(1)
  })

  it('shows the refusal sentence when starting the gateway payment fails, and the client\'s own sentence when it gave none', async () => {
    const store = arrange(HOSTED)

    table[`${HttpMethod.Post} ${START_PATH}`] = GATEWAY_DOWN
    await store.placeOrder(input())
    expect(store.error).toBe('The gateway is unavailable.')

    table[`${HttpMethod.Post} ${START_PATH}`] = { status: 502 }
    await store.placeOrder(input())
    expect(store.error).toBe('errors.noAnswer')
  })

  it('pays the same order again on a retry after a failed payment start', async () => {
    table[`${HttpMethod.Post} ${START_PATH}`] = GATEWAY_DOWN
    const store = arrange(HOSTED)

    await store.placeOrder(input())
    table[`${HttpMethod.Post} ${START_PATH}`] = { body: { redirectUrl: 'https://bank.example/pay' } }
    const destination = await store.placeOrder(input())

    expect(requestsTo(stub.requests, CREATE_PATH)).toHaveLength(1)
    expect(requestsTo(stub.requests, LOGIN_PATH)).toHaveLength(1)
    expect(destination).toMatchObject({ kind: CheckoutDestinationKind.External })
  })

  it('places a new order when the cart changed after a failed payment start', async () => {
    table[`${HttpMethod.Post} ${START_PATH}`] = GATEWAY_DOWN
    const store = arrange(HOSTED)

    await store.placeOrder(input())
    useCartStore().addItem({ ...hosting, pid: 6 })
    await nextTick()

    expect(store.pendingOrder).toBeNull()
    await store.placeOrder(input())
    expect(requestsTo(stub.requests, CREATE_PATH)).toHaveLength(2)
  })

  it('skips the payment and the total confirmation for an order the backend settled as free', async () => {
    table[`${HttpMethod.Post} ${CREATE_PATH}`] = { body: { ...placed, total: 0 } }
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
  beforeEach(() => {
    setAuthedCookie('1')
  })

  it('still sends the payer to the bank, saving nothing, when the account cannot be read', async () => {
    table[ME_ROUTE] = { status: 500 }
    const store = arrange(HOSTED)
    store.saveForRenewals = true

    const destination = await store.placeOrder(input())

    expect(destination).toEqual({ kind: CheckoutDestinationKind.External, target: 'https://bank.example/pay' })
    expect(store.error).toBe('')
    expect(consumeCheckoutSave(42, 'ada@example.com')).toBeNull()
  })

  it('is off by default', () => {
    const store = arrange(HOSTED)

    expect(store.saveForRenewals).toBe(false)
  })

  it('is offered to a signed-in client paying through a hosted gateway only', () => {
    const store = arrange(HOSTED)
    expect(store.canSaveForRenewals).toBe(true)

    store.selectedMethod = PaymentModule.BankTransfer
    expect(store.canSaveForRenewals).toBe(false)
  })

  it('is not offered to a guest', () => {
    setAuthedCookie(null)
    const store = arrange(HOSTED)

    expect(store.canSaveForRenewals).toBe(false)
  })

  it('keeps the choice and the module for the return page when the box was ticked, and still pays as before', async () => {
    const store = arrange(HOSTED)
    store.saveForRenewals = true

    const destination = await store.placeOrder(input())

    expect(destination).toEqual({ kind: CheckoutDestinationKind.External, target: 'https://bank.example/pay' })
    expect(requestsTo(stub.requests, START_PATH)).toHaveLength(1)
    expect(consumeCheckoutSave(42, 'ada@example.com')).toBe(HOSTED)
  })

  it('keeps the choice for the signed-in account only', async () => {
    const store = arrange(HOSTED)
    store.saveForRenewals = true

    await store.placeOrder(input())

    expect(consumeCheckoutSave(42, 'bob@example.com')).toBeNull()
  })

  it('keeps nothing when the account cannot be identified', async () => {
    table[ME_ROUTE] = { status: 404, body: { type: PortalErrorCode.ClientProfileNotFound } }
    const store = arrange(HOSTED)
    store.saveForRenewals = true

    await store.placeOrder(input())

    expect(consumeCheckoutSave(42, 'ada@example.com')).toBeNull()
  })

  it('keeps nothing when the box was left off', async () => {
    const store = arrange(HOSTED)

    await store.placeOrder(input())

    expect(consumeCheckoutSave(42, 'ada@example.com')).toBeNull()
  })

  it('keeps nothing for a bank transfer, whatever the box says', async () => {
    const store = arrange(PaymentModule.BankTransfer)
    store.saveForRenewals = true

    await store.placeOrder(input())

    expect(consumeCheckoutSave(42, 'ada@example.com')).toBeNull()
  })

  it('keeps nothing for a guest, whatever the box says', async () => {
    setAuthedCookie(null)
    // The guest stays a guest: a sign-in that succeeded would make them a client by the time the choice is read.
    table[`${HttpMethod.Post} ${LOGIN_PATH}`] = { status: 401, body: { detail: 'Wrong email or password.' } }
    const store = arrange(HOSTED)
    store.saveForRenewals = true

    await store.placeOrder(input())

    expect(consumeCheckoutSave(42, 'ada@example.com')).toBeNull()
  })

  it('keeps nothing when the gateway payment could not be started', async () => {
    table[`${HttpMethod.Post} ${START_PATH}`] = { status: 400, body: { detail: 'Gateway unavailable.' } }
    const store = arrange(HOSTED)
    store.saveForRenewals = true

    expect(await store.placeOrder(input())).toBeNull()

    expect(consumeCheckoutSave(42, 'ada@example.com')).toBeNull()
  })

  it('drops the choice of an earlier attempt when the same order is retried with the box off', async () => {
    const store = arrange(HOSTED)
    store.saveForRenewals = true
    await store.placeOrder(input())
    store.resetFlow()
    store.saveForRenewals = false

    await store.placeOrder(input())

    expect(consumeCheckoutSave(42, 'ada@example.com')).toBeNull()
  })
})

describe('useCheckoutStore billed total', () => {
  beforeEach(() => {
    setAuthedCookie('1')
  })

  it('pays without asking when the billed total is the one the cart showed', async () => {
    const confirmTotalChange = vi.fn(() => true)

    await arrange(PaymentModule.BankTransfer).placeOrder(input(confirmTotalChange))

    expect(confirmTotalChange).not.toHaveBeenCalled()
  })

  it('asks with both amounts when the billed total differs, and goes on when the payer agrees', async () => {
    table[`${HttpMethod.Post} ${CREATE_PATH}`] = { body: { ...placed, total: 15 } }
    const confirmTotalChange = vi.fn(() => true)

    const destination = await arrange(PaymentModule.BankTransfer).placeOrder(input(confirmTotalChange))

    expect(confirmTotalChange).toHaveBeenCalledWith('$12.00', '$15.00')
    expect(destination).toMatchObject({ kind: CheckoutDestinationKind.Route })
  })

  it('asks when the billed currency differs from the payer currency', async () => {
    table[`${HttpMethod.Post} ${CREATE_PATH}`] = { body: { ...placed, currency: 'EUR' } }
    const confirmTotalChange = vi.fn(() => false)

    await arrange(PaymentModule.BankTransfer).placeOrder(input(confirmTotalChange))

    expect(confirmTotalChange).toHaveBeenCalled()
  })

  it('takes no payment and keeps the cart when the payer declines the billed total, and asks again on a retry', async () => {
    table[`${HttpMethod.Post} ${CREATE_PATH}`] = { body: { ...placed, total: 15 } }
    const confirmTotalChange = vi.fn().mockReturnValueOnce(false).mockReturnValueOnce(true)
    const store = arrange(HOSTED)

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
    table[`${HttpMethod.Post} ${CREATE_PATH}`] = { body: { ...placed, total: 15 } }
    table[`${HttpMethod.Post} ${START_PATH}`] = { status: 400, body: { detail: 'Try again shortly.' } }
    const confirmTotalChange = vi.fn(() => true)
    const store = arrange(HOSTED)

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
  })

  it('reads nothing for an empty cart or an unknown payer currency', async () => {
    await useCheckoutStore().refreshCartPrices()
    useCurrencyStore().currencies = []
    await arrange('card_gateway').refreshCartPrices()

    expect(requestsTo(stub.requests, '/api/portal/public/products')).toHaveLength(0)
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

    store.selectedMethod = PaymentModule.BankTransfer
    expect(store.submittingLabel).toBe('checkout.processing')
    store.selectedMethod = HOSTED
    expect(store.submittingLabel).toBe('checkout.redirecting')
  })

  it('explains the bank hand-off in a separate hint, only while a hosted gateway order is in flight', () => {
    const store = useCheckoutStore()

    store.selectedMethod = HOSTED
    expect(store.submittingHint).toBe('')
    store.submitting = true
    expect(store.submittingHint).toBe('checkout.redirectingToBank')
    store.selectedMethod = PaymentModule.BankTransfer
    expect(store.submittingHint).toBe('')
    store.selectedMethod = ''
    expect(store.submittingHint).toBe('')
  })

  it('changes the guest currency only once the visitor accepts an emptied cart', () => {
    const store = arrange('card_gateway')
    const currencies = useCurrencyStore()

    expect(store.changeGuestCurrency('USD', () => true)).toBe(false)
    expect(store.changeGuestCurrency('AMD', () => false)).toBe(false)
    expect(currencies.selectedCode).toBeNull()
    expect(useCartStore().items).toHaveLength(1)

    expect(store.changeGuestCurrency('AMD', () => true)).toBe(true)
    expect(currencies.selectedCode).toBe('AMD')
    expect(useCartStore().items).toEqual([])
  })

  it('changes the currency without asking when the cart is empty', () => {
    const store = useCheckoutStore()
    const ask = vi.fn(() => false)

    expect(store.changeGuestCurrency('AMD', ask)).toBe(true)
    expect(ask).not.toHaveBeenCalled()
  })
})
