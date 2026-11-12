/**
 * Tests for `composables/useCheckoutPage.ts` against the real cart, currency, checkout, auth, client
 * and catalogue stores, the real API composables and the real transport, with a stub BFF answering on
 * a real socket: the guest form, the signed-in line, the currency options and the place-order flow
 * that both checkout templates share. Nothing of ours is replaced.
 *
 * @module tests/composables/useCheckoutPage.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import { createPinia, setActivePinia } from 'pinia'
import type { Pinia } from 'pinia'
import { mountAsyncPage } from '~/tests/helpers/mountWithKit'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { stubLocalePath } from '~/tests/helpers/runtimeStubs'
import { settle } from '~/tests/helpers/settle'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { useCheckoutPage } from '~/composables/useCheckoutPage'
import { useAuthStore } from '~/stores/auth'
import { useCartStore } from '~/stores/cart'
import { useCheckoutStore } from '~/stores/checkout'
import { useClientStore } from '~/stores/client'
import { useCurrencyStore } from '~/stores/currency'
import type { CartItem } from '~/types/cartitem'
import type { ClientUser } from '~/types/clientuser'
import { PaymentModule } from '~/types/paymentmodule'
import { Permission } from '~/types/permission'
import { PricingCycle } from '~/types/pricingcycle'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The BFF paths the checkout page reads and writes. */
const PATHS = {
  currencies: '/api/portal/public/billing-currencies',
  products: '/api/portal/public/products',
  methods: '/api/portal/order/payment-methods',
  create: '/api/portal/order/create',
  gatewayStart: '/api/portal/order/42/gateway-payment/start',
  bankTransfer: '/api/portal/client/invoices/77/bank-transfer',
} as const

/** The hosted gateway module the stub offers. */
const HOSTED_GATEWAY = 'card_gateway'

/** Where the payer lands after a bank-transfer order: the invoice's payment page. */
const INVOICE_PAYMENT_PATH = '/client/invoices/77/pay'

/** localStorage key the cart persists under, which the page restores when it opens. */
const CART_STORAGE_KEY = 'innovayse_cart'

/** A hosting line priced in dollars. */
const HOSTING: CartItem = { pid: 5, name: 'Pro', billingcycle: PricingCycle.Monthly, cycleLabel: 'Monthly', amount: 12, currency: 'USD' }

/** The result of the page composable, as a template receives it. */
type CheckoutPage = Awaited<ReturnType<typeof useCheckoutPage>>

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/** The Pinia every store of the current test lives in. */
let pinia: Pinia

/**
 * Builds the order the BFF answers a placed order with.
 *
 * @param total - What the backend billed.
 * @returns The reply.
 */
const placedOrder = (total: number): { body: Record<string, unknown> } => ({
  body: { orderId: 42, orderNumber: 'A 42', invoiceId: 77, paymentToken: 'tok', total, currency: 'USD' },
})

/**
 * Makes the stub offer these gateways, the first one being preselected.
 *
 * @param modules - The gateway modules, in the order they are offered.
 */
const offer = (modules: string[]): void => {
  table[`GET ${PATHS.methods}`] = { body: modules.map(module => ({ module, displayName: module })) }
}

/**
 * Signs a person in: the auth store holds the account, the client store the same record whose
 * permissions decide what they may do.
 *
 * @param user - The account.
 */
const signIn = (user: ClientUser): void => {
  useAuthStore().user = user
  useClientStore().user = user
}

/**
 * Reads the configured currencies into the currency store, as an earlier page of the visit would have.
 *
 * @returns Resolves once they are loaded.
 */
const preloadCurrencies = async (): Promise<void> => {
  await useCurrencyStore().load()
}

/**
 * Runs the composable the way a page does: inside a component's setup, under `Suspense`, so the
 * lifecycle hook it registers has an instance to belong to and fires on mount. The page's reads go
 * over a real socket, so this waits until the composable has settled and the catalogue read the
 * mount starts has been answered. That read happens only when, at mount, there is a cart and a known
 * payer currency; a visit that loads the currencies itself makes none.
 *
 * @returns What the composable returned.
 */
const runInPage = async (): Promise<CheckoutPage> => {
  /** The composable's result, captured from inside the host's setup. */
  let page: CheckoutPage | undefined
  const Host = defineComponent({
    async setup() {
      page = await useCheckoutPage()
      return () => h('div')
    },
  })
  // Decided before the mount, from what the page starts with: reading the stores afterwards would race the
  // currency reply and the cart restore, and wait for a catalogue read the page never makes.
  const readsCatalogue = useCurrencyStore().payerCode !== null
    && (!useCartStore().isEmpty || window.localStorage.getItem(CART_STORAGE_KEY) !== null)
  await mountAsyncPage(Host, { pinia })
  await waitUntil(() => expect(page).toBeDefined())
  if (readsCatalogue) {
    await waitUntil(() => expect(requestsTo(stub.requests, PATHS.products)).toHaveLength(1))
  }
  await settle(5)
  if (!page) {
    throw new Error('the composable did not settle')
  }
  return page
}

beforeEach(async () => {
  pinia = createPinia()
  setActivePinia(pinia)
  window.localStorage.clear()
  window.sessionStorage.clear()
  vi.stubGlobal('useHead', vi.fn())
  stubLocalePath()
  table = {
    [`GET ${PATHS.currencies}`]: { body: [
      { code: 'USD', numeric: '840', prefix: '$', suffix: '', decimals: 2, isBase: true },
      { code: 'EUR', numeric: '978', prefix: '', suffix: ' EUR', decimals: 2, isBase: false },
    ] },
    [`GET ${PATHS.products}`]: { body: [] },
    [`POST ${PATHS.create}`]: placedOrder(12),
    [`POST ${PATHS.bankTransfer}`]: { body: { invoiceId: 77, reference: 'INV-77' } },
  }
  offer([HOSTED_GATEWAY, PaymentModule.BankTransfer])
  stub = await startTransport(routeTable(table))
  vi.mocked(navigateTo).mockClear()
})

afterEach(async () => {
  await stub.close()
  vi.unstubAllGlobals()
})

describe('useCheckoutPage', () => {
  it('offers the currencies as select options and the signed-in e-mail', async () => {
    signIn(makeClientUser({ email: 'ann@acme.test' }))
    window.localStorage.setItem(CART_STORAGE_KEY, JSON.stringify([HOSTING]))

    const page = await runInPage()

    await waitUntil(() => expect(page.currencyOptions.value).toEqual([{ value: 'USD', label: 'USD' }, { value: 'EUR', label: 'EUR' }]))
    expect(page.userEmail.value).toBe('ann@acme.test')
    expect(page.cart.items).toEqual([HOSTING])
    expect(requestsTo(stub.requests, PATHS.currencies)).toHaveLength(1)
  })

  it('releases the submit button and re-prices the cart from the catalogue when the page opens', async () => {
    await preloadCurrencies()
    useCartStore().items = [{ ...HOSTING }]
    useCheckoutStore().submitting = true
    table[`GET ${PATHS.products}`] = { body: [{ id: 5, groupId: 1, name: 'Pro', pricing: { monthly: 15 } }] }

    const page = await runInPage()

    expect(page.submitting.value).toBe(false)
    await waitUntil(() => expect(page.cart.items[0]?.amount).toBe(15))
  })

  it('hands the templates the renewal-save choice, off by default, and whether to offer it', async () => {
    signIn(makeClientUser())

    const page = await runInPage()

    expect(page.saveForRenewals.value).toBe(false)
    expect(page.canSaveForRenewals.value).toBe(true)
  })

  it('asks the visitor before the store changes the currency', async () => {
    await preloadCurrencies()
    useCartStore().items = [{ ...HOSTING }]
    const confirmSpy = vi.fn(() => false)
    vi.stubGlobal('confirm', confirmSpy)
    const page = await runInPage()

    page.changeGuestCurrency('EUR')

    expect(confirmSpy).toHaveBeenCalledWith('cart.currency.changeConfirm')
    expect(page.currencyStore.selectedCode).toBeNull()
    expect(page.cart.items).toHaveLength(1)

    confirmSpy.mockReturnValue(true)
    page.changeGuestCurrency('EUR')

    expect(page.currencyStore.selectedCode).toBe('EUR')
    expect(page.cart.items).toHaveLength(0)
  })

  it('sends the guest form with the order and goes to the route the store names', async () => {
    await preloadCurrencies()
    offer([PaymentModule.BankTransfer])
    useCartStore().items = [{ ...HOSTING }]
    const page = await runInPage()
    page.form.email = 'guest@acme.test'

    await page.submitOrder()

    const [sent] = requestsTo(stub.requests, PATHS.create)
    expect(JSON.parse(sent?.body ?? '')).toEqual(expect.objectContaining({ email: 'guest@acme.test' }))
    expect(navigateTo).toHaveBeenCalledWith(INVOICE_PAYMENT_PATH)
  })

  it('offers the store a confirmation for a total that changed, and asks the visitor with both amounts', async () => {
    await preloadCurrencies()
    offer([PaymentModule.BankTransfer])
    useCartStore().items = [{ ...HOSTING }]
    table[`POST ${PATHS.create}`] = placedOrder(14)
    const confirmSpy = vi.fn(() => true)
    vi.stubGlobal('confirm', confirmSpy)
    const page = await runInPage()

    await page.submitOrder()

    expect(confirmSpy).toHaveBeenCalledWith('checkout.totalChangedConfirm')
    expect(navigateTo).toHaveBeenCalledWith(INVOICE_PAYMENT_PATH)
  })

  it('does not follow an external destination that is not a secure address', async () => {
    await preloadCurrencies()
    useCartStore().items = [{ ...HOSTING }]
    table[`POST ${PATHS.gatewayStart}`] = { body: { redirectUrl: 'javascript:alert(1)' } }
    const before = window.location.href
    const page = await runInPage()

    await page.submitOrder()

    expect(requestsTo(stub.requests, PATHS.gatewayStart)).toHaveLength(1)
    expect(window.location.href).toBe(before)
    expect(navigateTo).not.toHaveBeenCalled()
  })

  it('stays put when the store refuses the order', async () => {
    await preloadCurrencies()
    useCartStore().items = [{ ...HOSTING }]
    table[`POST ${PATHS.create}`] = { status: 400, body: { type: 'ORDER_REFUSED', detail: 'Refused.' } }
    const page = await runInPage()

    await page.submitOrder()

    expect(page.orderError.value).toBe('Refused.')
    expect(navigateTo).not.toHaveBeenCalled()
  })
})

describe('useCheckoutPage permissions', () => {
  beforeEach(async () => {
    await preloadCurrencies()
    offer([PaymentModule.BankTransfer])
    useCartStore().items = [{ ...HOSTING }]
  })

  it('lets a person who may place orders order', async () => {
    signIn(makeClientUser({ permissions: Permission.PlaceNewOrders }))
    const page = await runInPage()

    await page.submitOrder()

    expect(page.mayPlaceOrders.value).toBe(true)
    expect(requestsTo(stub.requests, PATHS.create)).toHaveLength(1)
  })

  it('sends nothing when a member of the account lacks the permission to place orders', async () => {
    signIn(makeClientUser({ permissions: Permission.ViewPayInvoices }))
    const page = await runInPage()

    await page.submitOrder()

    expect(page.mayPlaceOrders.value).toBe(false)
    expect(requestsTo(stub.requests, PATHS.create)).toHaveLength(0)
    expect(navigateTo).not.toHaveBeenCalled()
  })

  it('lets a signed-in first-time buyer, who acts for no account, order', async () => {
    signIn(makeClientUser({ permissions: 0, actsForAccount: false }))
    const page = await runInPage()

    await page.submitOrder()

    expect(page.mayPlaceOrders.value).toBe(true)
    expect(requestsTo(stub.requests, PATHS.create)).toHaveLength(1)
  })
})
