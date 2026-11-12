/**
 * Unit tests for CartDrawer: closed and empty states, the listed lines and total, the notice for
 * lines left in another currency, removing, clearing and the way on to checkout.
 *
 * The real cart and currency stores, API composable and transport run against a stub server
 * playing the BFF.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import CartDrawer from '~/components/layout/CartDrawer.vue'
import CheckoutBundleSummary from '~/components/checkout/BundleSummary.vue'
import { useCartStore } from '~/stores/cart'
import { useCurrencyStore } from '~/stores/currency'
import { BundleDiscountType } from '~/types/bundlediscounttype'
import type { CartItem } from '~/types/cartitem'
import type { BillingCurrency } from '~/types/currency'
import { PricingCycle } from '~/types/pricingcycle'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** Path of the endpoint that lists the enabled billing currencies. */
const CURRENCIES_PATH = '/api/portal/public/billing-currencies'

/** The stub playing the BFF. */
let stub: StubServer

/** The currencies the stub answers with. */
const currencies: BillingCurrency[] = [
  { code: 'USD', numeric: '840', prefix: '$', suffix: '', decimals: 2, isBase: true },
  { code: 'AMD', numeric: '051', prefix: '', suffix: ' AMD', decimals: 0, isBase: false },
]

/** Router stand-in shared by the component and the assertions. */
const router = { push: vi.fn(), replace: vi.fn(), back: vi.fn() }

/**
 * Builds a hosting cart line.
 *
 * @param pid Product id.
 * @param amount Price of the line.
 * @param currency Currency the line is priced in.
 * @returns The line.
 */
const line = (pid: number, amount: number, currency = 'USD'): CartItem => ({
  pid,
  name: `Plan ${pid}`,
  billingcycle: PricingCycle.Monthly,
  cycleLabel: 'Monthly',
  amount,
  currency,
})

/**
 * Mounts the drawer open, with the given lines in the cart, and waits for the currencies.
 *
 * @param items Lines to put in the cart.
 * @returns The wrapper.
 */
const mountOpen = async (items: CartItem[] = []) => {
  const wrapper = mountWithKit(CartDrawer, { components: { CheckoutBundleSummary } })
  const cart = useCartStore()
  cart.items = items
  cart.isOpen = true
  await waitUntil(() => expect(requestsTo(stub.requests, CURRENCIES_PATH)).toHaveLength(1))
  await waitUntil(() => expect(useCurrencyStore().loaded).toBe(true))
  await nextTick()
  return wrapper
}

describe('CartDrawer', () => {
  beforeEach(async () => {
    stubPublicPageRuntime()
    vi.stubGlobal('useRouter', () => router)
    router.push.mockClear()
    window.localStorage.clear()
    stub = await startTransport(routeTable({ [`GET ${CURRENCIES_PATH}`]: { body: currencies } }))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('renders nothing while the cart is closed', () => {
    const wrapper = mountWithKit(CartDrawer)

    expect(wrapper.find('[data-testid="ui-drawer"]').exists()).toBe(false)
  })

  it('shows the empty state with a way to the hosting plans', async () => {
    const wrapper = await mountOpen()

    expect(wrapper.find('[data-testid="cart-drawer-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="cart-drawer-checkout"]').exists()).toBe(false)

    await wrapper.find('[data-testid="cart-drawer-browse"]').trigger('click')

    expect(router.push).toHaveBeenCalledWith('/en/hosting')
    expect(useCartStore().isOpen).toBe(false)
  })

  it('lists the lines in the payer currency with their prices and the total', async () => {
    const wrapper = await mountOpen([line(1, 5), line(2, 7.5)])

    expect(wrapper.findAll('[data-testid="cart-drawer-item"]')).toHaveLength(2)
    const prices = wrapper.findAll('[data-testid="cart-drawer-item-price"]').map((price) => price.text())
    expect(prices[0]).toContain('5.00')
    expect(prices[1]).toContain('7.50')
    expect(wrapper.find('[data-testid="cart-drawer-total"]').text()).toContain('12.50')
    expect(wrapper.find('[data-testid="cart-drawer-count"]').text()).toContain('2')
  })

  it('takes the bundle discount off the total and names it', async () => {
    const wrapper = await mountOpen([{ ...line(1, 50), bundleId: 7 }, { ...line(2, 50), bundleId: 7 }])
    const cart = useCartStore()
    cart.bundle = { id: 7, name: 'Starter pack', discountType: BundleDiscountType.Percentage, discountAmount: 10, discountCurrency: null }
    await nextTick()

    expect(wrapper.find('[data-testid="bundle-summary-amount"]').text()).toBe('-$10.00')
    expect(wrapper.find('[data-testid="cart-drawer-total"]').text()).toContain('90.00')
  })

  it('leaves a line priced in another currency out of the list and the total, with a notice', async () => {
    const wrapper = await mountOpen([line(1, 5), line(2, 2000, 'AMD')])

    expect(wrapper.findAll('[data-testid="cart-drawer-item"]')).toHaveLength(1)
    expect(wrapper.find('[data-testid="cart-drawer-stale"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="cart-drawer-total"]').text()).toContain('5.00')
  })

  it('removes a line from its remove button', async () => {
    const wrapper = await mountOpen([line(1, 5), line(2, 7)])

    await wrapper.get('[data-testid="cart-drawer-remove"]').trigger('click')

    expect(useCartStore().items.map((item) => item.pid)).toEqual([2])
  })

  it('empties the cart from the clear button', async () => {
    const wrapper = await mountOpen([line(1, 5), line(2, 7)])

    await wrapper.find('[data-testid="cart-drawer-clear"]').trigger('click')

    expect(useCartStore().items).toEqual([])
  })

  it('closes and goes to the checkout', async () => {
    const wrapper = await mountOpen([line(1, 5)])

    await wrapper.find('[data-testid="cart-drawer-checkout"]').trigger('click')

    expect(router.push).toHaveBeenCalledWith('/en/checkout')
    expect(useCartStore().isOpen).toBe(false)
  })

  it('closes from the drawer close button', async () => {
    const wrapper = await mountOpen([line(1, 5)])

    await wrapper.find('[data-testid="ui-drawer-close"]').trigger('click')

    expect(useCartStore().isOpen).toBe(false)
  })
})
