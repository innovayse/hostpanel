/**
 * Tests for `stores/checkout.ts`.
 *
 * Both checkout templates place their order through this store, so these assertions pin the
 * whole flow once: the request body, the guest sign-in, the three ways of settling an order and
 * where the payer is sent afterwards, plus how a refusal is kept as state.
 *
 * @module tests/stores/checkout.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { nextTick } from 'vue'
import type { CartItem } from '~/types/cartitem'
import type { PlaceOrderInput } from '~/types/placeorderinput'
import { CartItemType } from '~/types/cartitemtype'
import { DomainOrderAction } from '~/types/domainorderaction'
import { PricingCycle } from '~/types/pricingcycle'
import { ProductType } from '~/types/producttype'

/** The mocked order endpoints, re-created for every test. */
const billing = {
  fetchGatewayMethods: vi.fn(),
  createOrder: vi.fn(),
  createOrderPaymentIntent: vi.fn(),
  confirmOrderPayment: vi.fn(),
  startOrderGatewayPayment: vi.fn()
}

/** The auth store stand-in: who is signed in and the sign-in call a guest order makes. */
const auth = { isLoggedIn: false, login: vi.fn() }

/** The payer the currency store stand-in reports. */
const payer = { code: 'USD' as string | null }

/** Records the payment tokens the hosted-gateway path stores before leaving for the bank. */
const rememberToken = vi.fn()

/** The chosen-currency calls the currency store stand-in receives. */
const selectCurrency = vi.fn()

/** The catalogue read the price refresh makes. */
const loadProducts = vi.fn()

vi.mock('~/stores/catalog', () => ({
  /**
   * Stands in for the catalogue store.
   *
   * @returns The catalogue read the checkout store uses to re-price the cart.
   */
  useCatalogStore: () => ({ loadProducts })
}))

vi.mock('~/composables/apis/useBillingApi', () => ({
  /**
   * Stands in for the real API composable so the store runs without transport.
   *
   * @returns The mocked endpoints.
   */
  useBillingApi: () => billing
}))

vi.mock('~/composables/useOrderPaymentToken', () => ({
  /**
   * Stands in for the token storage, which is a no-op outside a browser.
   *
   * @returns The remember function only.
   */
  useOrderPaymentToken: () => ({ remember: rememberToken })
}))

vi.mock('~/stores/auth', () => ({
  /**
   * Stands in for the auth store.
   *
   * @returns The shared stand-in.
   */
  useAuthStore: () => auth
}))

vi.mock('~/stores/currency', () => ({
  /**
   * Stands in for the currency store.
   *
   * @returns A payer code, a money shape and the select call.
   */
  useCurrencyStore: () => ({
    get payerCode() {
      return payer.code
    },
    moneyCurrencyFor: () => ({ code: 'USD', prefix: '$', suffix: '', decimals: 2 }),
    select: selectCurrency
  })
}))

const { useCheckoutStore } = await import('~/stores/checkout')
const { useCartStore } = await import('~/stores/cart')
const { CheckoutDestinationKind } = await import('~/types/checkoutdestinationkind')

/** A hosting item priced in dollars. */
const hosting: CartItem = { pid: 5, name: 'Pro', billingcycle: PricingCycle.Monthly, cycleLabel: 'Monthly', amount: 12, currency: 'USD' }

/** A domain item priced in dollars. */
const domainItem: CartItem = {
  pid: 9, name: 'a.com', billingcycle: PricingCycle.Annual, cycleLabel: '1 Year', amount: 10, currency: 'USD',
  domain: 'a.com', itemType: CartItemType.Domain, domainAction: DomainOrderAction.Register, years: 1
}

/** A guest's details. */
const guest = { firstname: 'Ada', lastname: 'L', email: 'ada@example.com', password: 'pw-123456', phonenumber: '+374' }

/** The order the API answers a placed order with. */
const placed = { orderId: 42, orderNumber: 'A 42', invoiceId: 77, paymentToken: 'tok', total: 12, currency: 'USD' }

/**
 * Builds the input a checkout page hands the store.
 *
 * @param confirmCard - The card confirmation to use; succeeds by default.
 * @param confirmTotalChange - The answer to a changed total; agrees by default.
 * @returns The input.
 */
const input = (
  confirmCard: PlaceOrderInput['confirmCard'] = async () => ({ id: 'pi_1' }),
  confirmTotalChange: PlaceOrderInput['confirmTotalChange'] = () => true
): PlaceOrderInput => ({
  guest,
  confirmCard,
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

describe('useCheckoutStore.loadPaymentMethods', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    Object.values(billing).forEach(fn => fn.mockReset())
    auth.isLoggedIn = false
    payer.code = 'USD'
  })

  it('keeps the gateways and preselects the first one', async () => {
    billing.fetchGatewayMethods.mockResolvedValue([{ module: 'stripe', displayName: 'Card' }, { module: 'bank_transfer', displayName: 'Bank' }])
    const store = useCheckoutStore()

    await store.loadPaymentMethods()

    expect(store.paymentMethods).toHaveLength(2)
    expect(store.selectedMethod).toBe('stripe')
    expect(store.methodsLoading).toBe(false)
  })

  it('does not override a method the payer already chose', async () => {
    billing.fetchGatewayMethods.mockResolvedValue([{ module: 'stripe', displayName: 'Card' }])
    const store = useCheckoutStore()
    store.selectedMethod = 'bank_transfer'

    await store.loadPaymentMethods()

    expect(store.selectedMethod).toBe('bank_transfer')
  })

  it('keeps the API sentence and an empty list when the gateways cannot be read', async () => {
    billing.fetchGatewayMethods.mockRejectedValue({ data: { detail: 'Gateways unavailable.' } })
    const store = useCheckoutStore()

    await store.loadPaymentMethods()

    expect(store.paymentMethods).toEqual([])
    expect(store.error).toBe('Gateways unavailable.')
  })
})

describe('useCheckoutStore.placeOrder', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    Object.values(billing).forEach(fn => fn.mockReset())
    auth.isLoggedIn = false
    auth.login = vi.fn().mockResolvedValue({})
    rememberToken.mockReset()
    payer.code = 'USD'
    billing.createOrder.mockResolvedValue(placed)
  })

  it('does nothing without a chosen method', async () => {
    const store = useCheckoutStore()

    expect(await store.placeOrder(input())).toBeNull()
    expect(billing.createOrder).not.toHaveBeenCalled()
  })

  it('sends the cart in the payer currency with the guest details and signs the guest in', async () => {
    const store = arrange('bank_transfer', [hosting, { ...hosting, pid: 6, currency: 'AMD' }])

    await store.placeOrder(input())

    const body = billing.createOrder.mock.calls[0]?.[0] as Record<string, unknown>
    expect((body.items as unknown[]).length).toBe(1)
    expect(body).toMatchObject({ paymentmethod: 'bank_transfer', currency: 'USD', ...guest })
    expect(auth.login).toHaveBeenCalledWith('ada@example.com', 'pw-123456')
  })

  it('sends no guest details and no sign-in for a signed-in client', async () => {
    auth.isLoggedIn = true
    const store = arrange('bank_transfer')

    await store.placeOrder(input())

    const body = billing.createOrder.mock.calls[0]?.[0] as Record<string, unknown>
    expect(body.email).toBeUndefined()
    expect(auth.login).not.toHaveBeenCalled()
  })

  it('still reports the order as placed when the guest sign-in is refused', async () => {
    auth.login = vi.fn().mockRejectedValue(new Error('no'))
    const store = arrange('bank_transfer')

    const destination = await store.placeOrder(input())

    expect(destination).toMatchObject({ kind: CheckoutDestinationKind.Route })
    expect(store.error).toBe('')
  })

  it('settles by card: intent, provider confirmation, backend confirmation, then the success page', async () => {
    billing.createOrderPaymentIntent.mockResolvedValue({ clientSecret: 'sec' })
    const confirmCard = vi.fn().mockResolvedValue({ id: 'pi_9' })
    const store = arrange('stripe', [domainItem])

    const destination = await store.placeOrder(input(confirmCard))

    expect(billing.createOrderPaymentIntent).toHaveBeenCalledWith(42, 'tok', false)
    expect(confirmCard).toHaveBeenCalledWith('sec')
    expect(billing.confirmOrderPayment).toHaveBeenCalledWith(42, { paymentIntentId: 'pi_9' }, 'tok')
    expect(destination).toEqual({
      kind: CheckoutDestinationKind.Route,
      target: '/en/client/order-success?order=A+42&invoice=77&domain=a.com&action=register'
    })
    expect(useCartStore().items).toEqual([])
    // The page is leaving: the button stays disabled so a second click cannot place another order.
    expect(store.submitting).toBe(true)
  })

  it('asks to keep the card for later charges only when the payer chose it', async () => {
    billing.createOrderPaymentIntent.mockResolvedValue({ clientSecret: 'sec' })
    const confirmCard = vi.fn().mockResolvedValue({ id: 'pi_9' })
    const store = arrange('stripe', [domainItem])

    await store.placeOrder({ ...input(confirmCard), saveCard: true })

    expect(billing.createOrderPaymentIntent).toHaveBeenCalledWith(42, 'tok', true)
  })

  it('hands a hosted gateway the localised return address, remembers the token and leaves the site', async () => {
    billing.startOrderGatewayPayment.mockResolvedValue({ redirectUrl: 'https://bank.example/pay' })
    const store = arrange('inecobank')

    const destination = await store.placeOrder(input())

    expect(rememberToken).toHaveBeenCalledWith(42, 'tok', 77)
    const returnUrl = billing.startOrderGatewayPayment.mock.calls[0]?.[2] as string
    expect(new URL(returnUrl).pathname).toBe('/en/payment/result')
    expect(new URL(returnUrl).search).toBe('?order=42')
    expect(destination).toEqual({ kind: CheckoutDestinationKind.External, target: 'https://bank.example/pay' })
    // Kept until the result page sees the payment confirmed.
    expect(useCartStore().items).toHaveLength(1)
    expect(store.submitting).toBe(true)
  })

  it('refuses a gateway address that is not secure and lets the payer try again', async () => {
    billing.startOrderGatewayPayment.mockResolvedValue({ redirectUrl: 'javascript:alert(1)' })
    const store = arrange('inecobank')

    const destination = await store.placeOrder(input())

    expect(destination).toBeNull()
    expect(store.error).toBe('checkout.errors.generic')
    expect(store.submitting).toBe(false)
    expect(useCartStore().items).toHaveLength(1)
  })

  it('places one order for two concurrent submits and one only', async () => {
    billing.createOrder.mockImplementation(async () => {
      await Promise.resolve()
      return placed
    })
    const store = arrange('bank_transfer')

    const [first, second] = await Promise.all([store.placeOrder(input()), store.placeOrder(input())])

    expect(billing.createOrder).toHaveBeenCalledTimes(1)
    expect([first, second].filter(destination => destination !== null)).toHaveLength(1)
  })

  it('places nothing for an empty cart', async () => {
    const store = arrange('bank_transfer', [])

    expect(await store.placeOrder(input())).toBeNull()
    expect(billing.createOrder).not.toHaveBeenCalled()
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
    billing.createOrder.mockResolvedValue({ ...placed, invoiceId: 0 })
    const withoutInvoice = await arrange('bank_transfer').placeOrder(input())
    expect(withoutInvoice).toEqual({ kind: CheckoutDestinationKind.Route, target: '/en/client/invoices' })
  })

  it('sends a free order straight to the success page, whatever the method, without paying', async () => {
    billing.createOrder.mockResolvedValue({ ...placed, total: 0 })
    const store = arrange('inecobank')

    const destination = await store.placeOrder(input())

    expect(billing.startOrderGatewayPayment).not.toHaveBeenCalled()
    expect(billing.createOrderPaymentIntent).not.toHaveBeenCalled()
    expect(destination).toEqual({
      kind: CheckoutDestinationKind.Route,
      target: '/en/client/order-success?order=A+42&invoice=77'
    })
    expect(useCartStore().items).toEqual([])
  })

  it('marks the success link digital when the order held digital goods only', async () => {
    billing.createOrder.mockResolvedValue({ ...placed, total: 0 })
    const digital: CartItem = { ...hosting, productType: ProductType.Digital }
    const store = arrange('inecobank', [digital])

    const destination = await store.placeOrder(input())

    expect(destination).toEqual({
      kind: CheckoutDestinationKind.Route,
      target: '/en/client/order-success?order=A+42&invoice=77&digital=1'
    })
  })

  it('does not mark the success link digital when the order also held a hosting line', async () => {
    billing.createOrder.mockResolvedValue({ ...placed, total: 0 })
    const digital: CartItem = { ...hosting, pid: 99, productType: ProductType.Digital }
    const store = arrange('inecobank', [digital, hosting])

    const destination = await store.placeOrder(input())

    expect(destination).toEqual({
      kind: CheckoutDestinationKind.Route,
      target: '/en/client/order-success?order=A+42&invoice=77'
    })
  })

  it('keeps the API sentence and the field messages when the order is refused, and keeps the cart', async () => {
    billing.createOrder.mockRejectedValue({
      data: { detail: 'Fix the form.', type: 'VALIDATION_ERROR', errors: { email: ['Email is required.'] } }
    })
    const store = arrange('bank_transfer')

    const destination = await store.placeOrder(input())

    expect(destination).toBeNull()
    expect(store.error).toBe('Email is required.')
    expect(store.fieldErrors).toEqual({ email: 'Email is required.' })
    expect(store.submitting).toBe(false)
    expect(useCartStore().items).toHaveLength(1)
  })

  it('shows the payment provider sentence when the card is declined, and a generic one when it gave none', async () => {
    billing.createOrderPaymentIntent.mockResolvedValue({ clientSecret: 'sec' })
    const store = arrange('stripe')

    await store.placeOrder(input(async () => {
      throw new Error('Your card was declined.')
    }))
    expect(store.error).toBe('Your card was declined.')

    await store.placeOrder(input(async () => {
      throw new Error('')
    }))
    expect(store.error).toBe('checkout.errors.generic')
  })

  it('pays the same order again on a retry after a failed payment', async () => {
    billing.createOrderPaymentIntent.mockResolvedValue({ clientSecret: 'sec' })
    const store = arrange('stripe')

    await store.placeOrder(input(async () => {
      throw new Error('Your card was declined.')
    }))
    const destination = await store.placeOrder(input())

    expect(billing.createOrder).toHaveBeenCalledTimes(1)
    expect(auth.login).toHaveBeenCalledTimes(1)
    expect(billing.confirmOrderPayment).toHaveBeenCalledWith(42, { paymentIntentId: 'pi_1' }, 'tok')
    expect(destination).toMatchObject({ kind: CheckoutDestinationKind.Route })
  })

  it('repeats only the backend confirmation when it failed after the card was charged', async () => {
    billing.createOrderPaymentIntent.mockResolvedValue({ clientSecret: 'sec' })
    billing.confirmOrderPayment.mockRejectedValueOnce({ data: { detail: 'Try again shortly.' } })
    const confirmCard = vi.fn().mockResolvedValue({ id: 'pi_9' })
    const store = arrange('stripe')

    expect(await store.placeOrder(input(confirmCard))).toBeNull()
    expect(store.pendingPaymentIntentId).toBe('pi_9')
    const destination = await store.placeOrder(input(confirmCard))

    expect(billing.createOrderPaymentIntent).toHaveBeenCalledTimes(1)
    expect(confirmCard).toHaveBeenCalledTimes(1)
    expect(billing.confirmOrderPayment).toHaveBeenCalledTimes(2)
    expect(billing.confirmOrderPayment).toHaveBeenLastCalledWith(42, { paymentIntentId: 'pi_9' }, 'tok')
    expect(destination).toMatchObject({ kind: CheckoutDestinationKind.Route })
  })

  it('forgets the charged card payment when the cart changes', async () => {
    billing.createOrderPaymentIntent.mockResolvedValue({ clientSecret: 'sec' })
    billing.confirmOrderPayment.mockRejectedValueOnce({ data: { detail: 'Try again shortly.' } })
    const store = arrange('stripe')
    await store.placeOrder(input())

    useCartStore().addItem({ ...hosting, pid: 6 })
    await nextTick()

    expect(store.pendingPaymentIntentId).toBeNull()
  })

  it('places a new order when the cart changed after a failed payment', async () => {
    billing.createOrderPaymentIntent.mockResolvedValue({ clientSecret: 'sec' })
    const store = arrange('stripe')

    await store.placeOrder(input(async () => {
      throw new Error('Your card was declined.')
    }))
    useCartStore().addItem({ ...hosting, pid: 6 })
    await nextTick()

    expect(store.pendingOrder).toBeNull()
    await store.placeOrder(input())
    expect(billing.createOrder).toHaveBeenCalledTimes(2)
  })

  it('skips the card and the total confirmation for an order the backend settled as free', async () => {
    billing.createOrder.mockResolvedValue({ ...placed, total: 0 })
    const confirmTotalChange = vi.fn(() => false)
    const store = arrange('stripe')

    const destination = await store.placeOrder(input(undefined, confirmTotalChange))

    expect(confirmTotalChange).not.toHaveBeenCalled()
    expect(billing.createOrderPaymentIntent).not.toHaveBeenCalled()
    expect(billing.startOrderGatewayPayment).not.toHaveBeenCalled()
    expect(destination).toEqual({
      kind: CheckoutDestinationKind.Route,
      target: '/en/client/order-success?order=A+42&invoice=77'
    })
    expect(useCartStore().items).toEqual([])
  })
})

describe('useCheckoutStore billed total', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    Object.values(billing).forEach(fn => fn.mockReset())
    auth.isLoggedIn = true
    payer.code = 'USD'
    billing.createOrder.mockResolvedValue(placed)
  })

  it('pays without asking when the billed total is the one the cart showed', async () => {
    const confirmTotalChange = vi.fn(() => true)

    await arrange('bank_transfer').placeOrder(input(undefined, confirmTotalChange))

    expect(confirmTotalChange).not.toHaveBeenCalled()
  })

  it('asks with both amounts when the billed total differs, and goes on when the payer agrees', async () => {
    billing.createOrder.mockResolvedValue({ ...placed, total: 15 })
    const confirmTotalChange = vi.fn(() => true)

    const destination = await arrange('bank_transfer').placeOrder(input(undefined, confirmTotalChange))

    expect(confirmTotalChange).toHaveBeenCalledWith('$12.00', '$15.00')
    expect(destination).toMatchObject({ kind: CheckoutDestinationKind.Route })
  })

  it('asks when the billed currency differs from the payer currency', async () => {
    billing.createOrder.mockResolvedValue({ ...placed, currency: 'EUR' })
    const confirmTotalChange = vi.fn(() => false)

    await arrange('bank_transfer').placeOrder(input(undefined, confirmTotalChange))

    expect(confirmTotalChange).toHaveBeenCalled()
  })

  it('takes no payment and keeps the cart when the payer declines the billed total, and asks again on a retry', async () => {
    billing.createOrder.mockResolvedValue({ ...placed, total: 15 })
    billing.createOrderPaymentIntent.mockResolvedValue({ clientSecret: 'sec' })
    const confirmTotalChange = vi.fn().mockReturnValueOnce(false).mockReturnValueOnce(true)
    const store = arrange('stripe')

    expect(await store.placeOrder(input(undefined, confirmTotalChange))).toBeNull()
    expect(store.error).toBe('checkout.errors.totalNotAccepted')
    expect(store.submitting).toBe(false)
    expect(billing.createOrderPaymentIntent).not.toHaveBeenCalled()
    expect(useCartStore().items).toHaveLength(1)

    await store.placeOrder(input(undefined, confirmTotalChange))

    expect(confirmTotalChange).toHaveBeenCalledTimes(2)
    expect(billing.createOrder).toHaveBeenCalledTimes(1)
    expect(billing.confirmOrderPayment).toHaveBeenCalled()
  })

  it('does not ask again for an order whose changed total the payer already accepted', async () => {
    billing.createOrder.mockResolvedValue({ ...placed, total: 15 })
    billing.createOrderPaymentIntent.mockResolvedValue({ clientSecret: 'sec' })
    billing.confirmOrderPayment.mockRejectedValueOnce({ data: { detail: 'Try again shortly.' } })
    const confirmTotalChange = vi.fn(() => true)
    const store = arrange('stripe')

    await store.placeOrder(input(undefined, confirmTotalChange))
    await store.placeOrder(input(undefined, confirmTotalChange))

    expect(confirmTotalChange).toHaveBeenCalledTimes(1)
  })
})

describe('useCheckoutStore.refreshCartPrices', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    loadProducts.mockReset()
    payer.code = 'USD'
  })

  it('re-prices the cart from the catalogue', async () => {
    loadProducts.mockResolvedValue([{ id: 5, groupId: 1, name: 'Pro', pricing: { monthly: 14, annual: 140 } }])
    const store = arrange('stripe')

    await store.refreshCartPrices()

    expect(useCartStore().items[0]?.amount).toBe(14)
  })

  it('reads nothing for an empty cart or an unknown payer currency', async () => {
    await useCheckoutStore().refreshCartPrices()
    payer.code = null
    await arrange('stripe').refreshCartPrices()

    expect(loadProducts).not.toHaveBeenCalled()
  })
})

describe('useCheckoutStore cart helpers', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    selectCurrency.mockReset()
    payer.code = 'USD'
  })

  it('totals only items in the payer currency and flags the others', () => {
    const store = arrange('stripe', [hosting, { ...hosting, pid: 6, currency: 'AMD', amount: 5000 }])

    expect(store.totalLabel).toBe('$12.00')
    expect(store.staleItems).toHaveLength(1)
    expect(store.itemPrice(hosting)).toBe('$12.00')
  })

  it('formats an empty cart as zero', () => {
    expect(useCheckoutStore().totalLabel).toBe('$0.00')
  })

  it('labels the submit button for a hosted gateway differently from card and bank transfer', () => {
    const store = useCheckoutStore()

    store.selectedMethod = 'stripe'
    expect(store.submittingLabel).toBe('checkout.processing')
    store.selectedMethod = 'inecobank'
    expect(store.submittingLabel).toBe('checkout.redirecting')
  })

  it('explains the bank hand-off in a separate hint, only while a hosted gateway order is in flight', () => {
    const store = useCheckoutStore()

    store.selectedMethod = 'inecobank'
    expect(store.submittingHint).toBe('')
    store.submitting = true
    expect(store.submittingHint).toBe('checkout.redirectingToBank')
    store.selectedMethod = 'stripe'
    expect(store.submittingHint).toBe('')
    store.selectedMethod = ''
    expect(store.submittingHint).toBe('')
  })

  it('changes the guest currency only once the visitor accepts an emptied cart', () => {
    const store = arrange('stripe')

    expect(store.changeGuestCurrency('USD', () => true)).toBe(false)
    expect(store.changeGuestCurrency('AMD', () => false)).toBe(false)
    expect(selectCurrency).not.toHaveBeenCalled()
    expect(useCartStore().items).toHaveLength(1)

    expect(store.changeGuestCurrency('AMD', () => true)).toBe(true)
    expect(selectCurrency).toHaveBeenCalledWith('AMD')
    expect(useCartStore().items).toEqual([])
  })

  it('changes the currency without asking when the cart is empty', () => {
    const store = useCheckoutStore()
    const ask = vi.fn(() => false)

    expect(store.changeGuestCurrency('AMD', ask)).toBe(true)
    expect(ask).not.toHaveBeenCalled()
  })
})
