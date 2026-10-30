/**
 * Tests for `composables/useCheckoutPage.ts` with the cart, currency, checkout and auth stores
 * replaced by minimal fakes: the guest form, the signed-in line, the currency options and the
 * place-order flow that both checkout templates share.
 *
 * @module tests/composables/useCheckoutPage.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { Suspense, defineComponent, h, reactive, ref } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { CheckoutDestinationKind } from '~/types/checkoutdestinationkind'
import { Permission } from '~/composables/usePermissions'
import { useCheckoutPage } from '~/composables/useCheckoutPage'

/** The fake stores and their spies, shared with the module mocks below. */
const fakes = vi.hoisted(() => ({
  cart: null as null | Record<string, unknown>,
  currency: null as null | Record<string, unknown>,
  checkout: null as null | Record<string, unknown>,
  auth: null as null | Record<string, unknown>,
  /** The permissions the signed-in person holds; `null` stands for a person who acts for no account. */
  held: null as number | null,
}))

vi.mock('~/stores/cart', () => ({ useCartStore: () => fakes.cart }))
vi.mock('~/stores/currency', () => ({ useCurrencyStore: () => fakes.currency }))
vi.mock('~/stores/checkout', () => ({ useCheckoutStore: () => fakes.checkout }))
vi.mock('~/stores/auth', () => ({ useAuthStore: () => fakes.auth }))
// The client store's own decision (`mayPerform`) is covered by its tests; here only the wiring is.
vi.mock('~/stores/client', () => ({
  useClientStore: () => ({ mayDo: (flag: number) => fakes.held === null || (fakes.held & flag) === flag }),
}))

/**
 * Builds the four fake stores.
 *
 * @param destination What the checkout store's order action answers with.
 * @returns The spies a test asserts on.
 */
const buildStores = (destination: { kind: CheckoutDestinationKind, target: string } | null) => {
  const placeOrder = vi.fn(async () => destination)
  const changeGuestCurrency = vi.fn()
  const resetFlow = vi.fn()
  const refreshCartPrices = vi.fn(async () => undefined)
  fakes.cart = reactive({ init: vi.fn() })
  fakes.currency = reactive({ currencies: [{ code: 'USD' }, { code: 'EUR' }], init: vi.fn(), load: vi.fn() })
  fakes.checkout = {
    staleItems: ref([]),
    totalLabel: ref(''),
    submittingLabel: ref(''),
    submittingHint: ref(''),
    paymentMethods: ref([]),
    selectedMethod: ref(''),
    submitting: ref(false),
    error: ref(''),
    itemPrice: () => '',
    changeGuestCurrency,
    resetFlow,
    refreshCartPrices,
    loadPaymentMethods: vi.fn(async () => undefined),
    placeOrder,
  }
  fakes.auth = { user: ref({ email: 'ann@acme.test' }), isLoggedIn: ref(true), fetchUser: vi.fn(), logout: vi.fn() }
  return { placeOrder, changeGuestCurrency, resetFlow, refreshCartPrices }
}

/**
 * Runs the composable the way a page does: inside a component's setup, under `Suspense`, so the
 * lifecycle hook it registers has an instance to belong to and fires on mount.
 *
 * @returns What the composable returned.
 */
const runInPage = async () => {
  /** The composable's result, captured from inside the host's setup. */
  let page: Awaited<ReturnType<typeof useCheckoutPage>> | undefined
  const Host = defineComponent({
    async setup() {
      page = await useCheckoutPage()
      return () => h('div')
    },
  })
  mount(defineComponent({ render: () => h(Suspense, null, { default: () => h(Host) }) }))
  await flushPromises()
  if (!page) {
    throw new Error('the composable did not settle')
  }
  return page
}

describe('useCheckoutPage', () => {
  beforeEach(() => {
    fakes.held = Permission.All
    vi.stubGlobal('useHead', vi.fn())
    vi.stubGlobal('useLocalePath', () => (path: string) => path)
  })

  it('offers the currencies as select options and the signed-in e-mail', async () => {
    buildStores(null)
    const page = await runInPage()

    expect(page.currencyOptions.value).toEqual([{ value: 'USD', label: 'USD' }, { value: 'EUR', label: 'EUR' }])
    expect(page.userEmail.value).toBe('ann@acme.test')
    expect(fakes.cart?.init).toHaveBeenCalled()
    expect(fakes.currency?.load).toHaveBeenCalled()
  })

  it('releases the submit button and re-prices the cart from the catalogue when the page opens', async () => {
    const { resetFlow, refreshCartPrices } = buildStores(null)
    await runInPage()

    expect(resetFlow).toHaveBeenCalled()
    expect(refreshCartPrices).toHaveBeenCalled()
  })

  it('asks the visitor before the store changes the currency', async () => {
    const { changeGuestCurrency } = buildStores(null)
    const page = await runInPage()

    page.changeGuestCurrency('EUR')

    expect(changeGuestCurrency).toHaveBeenCalledWith('EUR', expect.any(Function))
  })

  it('sends the guest form with the order and goes to the route the store names', async () => {
    const { placeOrder } = buildStores({ kind: CheckoutDestinationKind.Route, target: '/client/order-success' })
    const page = await runInPage()
    page.form.email = 'guest@acme.test'

    await page.submitOrder()

    expect(placeOrder).toHaveBeenCalledWith(expect.objectContaining({ guest: expect.objectContaining({ email: 'guest@acme.test' }) }))
    expect(navigateTo).toHaveBeenCalledWith('/client/order-success')
  })

  it('offers the store a confirmation for a total that changed, and asks the visitor with both amounts', async () => {
    const { placeOrder } = buildStores({ kind: CheckoutDestinationKind.Route, target: '/client/order-success' })
    const confirmSpy = vi.fn(() => true)
    vi.stubGlobal('confirm', confirmSpy)
    const page = await runInPage()

    await page.submitOrder()
    /** The arguments the page passed to placeOrder, holding the total change confirmation. */
    const input = (placeOrder.mock.calls[0] as unknown as [{ confirmTotalChange: (shown: string, charged: string) => boolean }])[0]

    expect(input.confirmTotalChange('$10.00', '$12.00')).toBe(true)
    expect(confirmSpy).toHaveBeenCalledWith('checkout.totalChangedConfirm')
  })

  it('does not follow an external destination that is not a secure address', async () => {
    buildStores({ kind: CheckoutDestinationKind.External, target: 'javascript:alert(1)' })
    vi.mocked(navigateTo).mockClear()
    const before = window.location.href
    const page = await runInPage()

    await page.submitOrder()

    expect(window.location.href).toBe(before)
    expect(navigateTo).not.toHaveBeenCalled()
  })

  it('stays put when the store refuses the order', async () => {
    buildStores(null)
    vi.mocked(navigateTo).mockClear()
    const page = await runInPage()

    await page.submitOrder()

    expect(navigateTo).not.toHaveBeenCalled()
  })
})

describe('useCheckoutPage permissions', () => {
  beforeEach(() => {
    vi.stubGlobal('useHead', vi.fn())
    vi.stubGlobal('useLocalePath', () => (path: string) => path)
    vi.mocked(navigateTo).mockClear()
  })

  it('lets a person who may place orders order', async () => {
    fakes.held = Permission.PlaceNewOrders
    const { placeOrder } = buildStores({ kind: CheckoutDestinationKind.Route, target: '/client/order-success' })
    const page = await runInPage()

    await page.submitOrder()

    expect(page.mayPlaceOrders.value).toBe(true)
    expect(placeOrder).toHaveBeenCalled()
  })

  it('sends nothing when a member of the account lacks the permission to place orders', async () => {
    fakes.held = Permission.ViewPayInvoices
    const { placeOrder } = buildStores({ kind: CheckoutDestinationKind.Route, target: '/client/order-success' })
    const page = await runInPage()

    await page.submitOrder()

    expect(page.mayPlaceOrders.value).toBe(false)
    expect(placeOrder).not.toHaveBeenCalled()
    expect(navigateTo).not.toHaveBeenCalled()
  })

  it('lets a signed-in first-time buyer, who acts for no account, order', async () => {
    fakes.held = null
    const { placeOrder } = buildStores({ kind: CheckoutDestinationKind.Route, target: '/client/order-success' })
    const page = await runInPage()

    await page.submitOrder()

    expect(page.mayPlaceOrders.value).toBe(true)
    expect(placeOrder).toHaveBeenCalled()
  })
})
