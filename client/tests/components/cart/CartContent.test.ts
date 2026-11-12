/**
 * Unit tests for CartContent: the empty notice, the lines with their prices, the total, the stale
 * currency warning and the remove, clear and checkout actions.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import CartContent from '~/components/cart/CartContent.vue'
import CheckoutBundleSummary from '~/components/checkout/BundleSummary.vue'
import type { CartItem } from '~/types/cartitem'
import { CartItemType } from '~/types/cartitemtype'
import { PricingCycle } from '~/types/pricingcycle'

/** Two cart lines. */
const items: CartItem[] = [
  { pid: 1, name: 'Starter', billingcycle: PricingCycle.Monthly, cycleLabel: 'Monthly', amount: 5, currency: 'USD' },
  { pid: 2, name: 'example.com', billingcycle: PricingCycle.Annual, cycleLabel: '1 Year', amount: 12, currency: 'USD', itemType: CartItemType.Domain, domain: 'example.com' },
]

/**
 * Mounts the content.
 *
 * @param props Props that differ from the defaults.
 * @returns The wrapper.
 */
const mountContent = (props: Record<string, unknown> = {}) =>
  mountWithKit(CartContent, {
    props: { items, staleCount: 0, totalLabel: '$17.00', priceOf: (item: CartItem) => `$${item.amount}.00`, ...props },
    stubs: { TransitionGroup: false },
    components: { CheckoutBundleSummary },
  })

describe('CartContent', () => {
  beforeEach(() => {
    stubPublicPageRuntime()
  })

  it('drops the glow wash and the gradient checkout button under the classic template', () => {
    const wrapper = mountContent()

    expect(wrapper.find('[data-testid="cart-checkout"]').classes()).toContain('[.tpl-classic_&]:bg-none')
    expect(wrapper.find('[data-testid="cart-content"] [data-testid="ui-decor"]').classes()).toContain('[.tpl-classic_&]:hidden')
  })

  it('shows the empty notice with a link to the hosting plans when there are no lines', () => {
    const wrapper = mountContent({ items: [], totalLabel: '' })

    expect(wrapper.find('[data-testid="cart-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="cart-browse"]').attributes('href')).toBe('/en/hosting')
    expect(wrapper.find('[data-testid="cart-item"]').exists()).toBe(false)
  })

  it('lists every line with its name and its price in the currency it was added in', () => {
    const wrapper = mountContent()

    const rows = wrapper.findAll('[data-testid="cart-item"]')
    expect(rows).toHaveLength(2)
    expect(rows[0]?.find('[data-testid="cart-item-name"]').text()).toBe('Starter')
    expect(rows[0]?.text()).toContain('$5.00')
    expect(rows[1]?.text()).toContain('$12.00')
  })

  it('shows the total and the way to checkout', () => {
    const wrapper = mountContent()

    expect(wrapper.find('[data-testid="cart-total"]').text()).toBe('$17.00')
    expect(wrapper.find('[data-testid="cart-checkout"]').attributes('href')).toBe('/en/checkout')
  })

  it('warns only when some lines are priced in another currency', () => {
    expect(mountContent().find('[data-testid="cart-stale"]').exists()).toBe(false)
    expect(mountContent({ staleCount: 1 }).find('[data-testid="cart-stale"]').text()).toContain('cart.staleCurrencyNotice')
  })

  it('reports the line the visitor removes', async () => {
    const wrapper = mountContent()

    await wrapper.findAll('[data-testid="cart-item-remove"]')[1]?.trigger('click')

    expect(wrapper.emitted('remove')?.[0]).toEqual([items[1]])
  })

  it('shows the bundle line only for a cart filled from a bundle with a discount', () => {
    expect(mountContent().find('[data-testid="bundle-summary-discount"]').exists()).toBe(false)

    const wrapper = mountContent({ bundleName: 'Starter pack', bundleOffer: '10% off', bundleDiscount: '$1.70' })

    expect(wrapper.find('[data-testid="bundle-summary-amount"]').text()).toBe('-$1.70')
  })

  it('reports Clear cart', async () => {
    const wrapper = mountContent()

    await wrapper.find('[data-testid="cart-clear"]').trigger('click')

    expect(wrapper.emitted('clear')).toHaveLength(1)
  })
})
