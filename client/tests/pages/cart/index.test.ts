/**
 * Unit tests for the cart page against the real cart and currency stores, API composable and
 * transport, with a stub BFF answering on a real socket (nothing of ours is mocked): it shows the
 * visitor's lines in the currency each was added in, totals those in the payer's currency, flags the
 * others, and removes or clears lines.
 *
 * @module tests/pages/cart/index.test
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { nextTick } from 'vue'
import { mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { USD_CURRENCY } from '~/tests/helpers/payerCatalogue'
import { until } from '~/tests/helpers/settle'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import CartPage from '~/pages/cart/index.vue'
import CartHero from '~/components/cart/CartHero.vue'
import CartContent from '~/components/cart/CartContent.vue'
import { useCartStore } from '~/stores/cart'
import { useCurrencyStore } from '~/stores/currency'
import type { BillingCurrency } from '~/types/currency'
import { PricingCycle } from '~/types/pricingcycle'

/** The currencies the stub BFF answers with. */
const currencies: BillingCurrency[] = [
  USD_CURRENCY,
  { code: 'AMD', numeric: '051', prefix: '', suffix: ' AMD', decimals: 0, isBase: false },
]

/** The stub playing the BFF. */
let stub: StubServer

/**
 * Mounts the page and waits until its currencies have been read from the stub.
 *
 * @returns The wrapper.
 */
const mountCart = async () => {
  stubPublicPageRuntime()
  const wrapper = await mountPage(CartPage, { components: { CartHero, CartContent } })
  await until(() => useCurrencyStore().loaded, 'the billing currencies to be read')
  await flushPromises()
  return wrapper
}

/**
 * Puts a line in the cart and waits for the page to render it.
 *
 * @param pid The product id.
 * @param name The line name.
 * @param amount The price.
 * @param currency The currency the price is in.
 */
const addLine = async (pid: number, name: string, amount: number, currency: string): Promise<void> => {
  useCartStore().addItem({ pid, name, billingcycle: PricingCycle.Monthly, cycleLabel: 'Monthly', amount, currency })
  await nextTick()
}

describe('cart page', () => {
  beforeEach(async () => {
    window.localStorage.clear()
    stub = await startTransport(routeTable({ [`GET ${CatalogPath.BillingCurrencies}`]: { body: currencies } }))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('shows the empty notice before anything is added', async () => {
    const wrapper = await mountCart()

    expect(wrapper.find('[data-testid="cart-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="cart-item"]').exists()).toBe(false)
  })

  it('lists a line and totals it in the payer currency', async () => {
    const wrapper = await mountCart()

    await addLine(1, 'Starter', 5, 'USD')

    expect(wrapper.findAll('[data-testid="cart-item"]')).toHaveLength(1)
    expect(wrapper.find('[data-testid="cart-item-name"]').text()).toBe('Starter')
    expect(wrapper.find('[data-testid="cart-total"]').text()).toContain('5.00')
    expect(wrapper.find('[data-testid="cart-empty"]').exists()).toBe(false)
  })

  it('formats a line in the currency it was added in and warns when it is not the payer currency', async () => {
    const wrapper = await mountCart()

    await addLine(2, 'Business', 2000, 'AMD')

    expect(wrapper.find('[data-testid="cart-item"]').text()).toContain('AMD')
    expect(wrapper.find('[data-testid="cart-stale"]').exists()).toBe(true)
  })

  it('removes the line the visitor removes', async () => {
    const wrapper = await mountCart()
    await addLine(1, 'Starter', 5, 'USD')
    await addLine(3, 'Pro', 9, 'USD')

    await wrapper.findAll('[data-testid="cart-item-remove"]')[0]?.trigger('click')

    expect(useCartStore().items.map((item) => item.pid)).toEqual([3])
  })

  it('empties the cart from Clear cart', async () => {
    const wrapper = await mountCart()
    await addLine(1, 'Starter', 5, 'USD')

    await wrapper.find('[data-testid="cart-clear"]').trigger('click')

    expect(useCartStore().items).toEqual([])
    expect(wrapper.find('[data-testid="cart-empty"]').exists()).toBe(true)
  })

  it('leads to checkout', async () => {
    const wrapper = await mountCart()
    await addLine(1, 'Starter', 5, 'USD')

    expect(wrapper.find('[data-testid="cart-checkout"]').attributes('href')).toBe('/en/checkout')
  })
})
