/**
 * Tests for `stores/cart.ts`'s currency-aware total and stale-item exclusion.
 *
 * Multi-currency pricing means a cart can hold items priced in different currencies: the
 * payer added something, then changed currency. The total must never silently sum across
 * currencies (that would add dollars to drams), and a stale item must be excluded rather than
 * dropped, so the summary can tell the payer to remove and re-add it.
 *
 * @module tests/stores/cart.test
 */

import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useCartStore } from '~/stores/cart'
import type { CartItem } from '~/types/cartitem'
import { CartItemType } from '~/types/cartitemtype'
import { DomainOrderAction } from '~/types/domainorderaction'
import type { PortalProduct } from '~/types/portalproduct'
import { PricingCycle } from '~/types/pricingcycle'
import { ProductType } from '~/types/producttype'

/**
 * Builds a minimal hosting cart item priced in the given currency.
 *
 * @param overrides Fields to set; `pid`, `amount` and `currency` are required.
 * @returns The cart item.
 */
const hostingItem = (overrides: Partial<CartItem> & { pid: number; amount: number; currency: string }): CartItem => {
  return {
    name: `Plan ${overrides.pid}`,
    billingcycle: PricingCycle.Monthly,
    cycleLabel: 'Monthly',
    ...overrides,
  }
}

describe('useCartStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('sums only items priced in the payer currency', () => {
    const cart = useCartStore()
    cart.items = [
      hostingItem({ pid: 1, amount: 10, currency: 'USD' }),
      hostingItem({ pid: 2, amount: 5, currency: 'USD' }),
      hostingItem({ pid: 3, amount: 1200, currency: 'AMD' }),
    ]

    expect(cart.total('USD')).toBe(15)
  })

  it('excludes items in a different currency from itemsInCurrency', () => {
    const cart = useCartStore()
    cart.items = [
      hostingItem({ pid: 1, amount: 10, currency: 'USD' }),
      hostingItem({ pid: 3, amount: 1200, currency: 'AMD' }),
    ]

    expect(cart.itemsInCurrency('USD').map(i => i.pid)).toEqual([1])
  })

  it('reports stale items separately rather than dropping them', () => {
    const cart = useCartStore()
    cart.items = [
      hostingItem({ pid: 1, amount: 10, currency: 'USD' }),
      hostingItem({ pid: 3, amount: 1200, currency: 'AMD' }),
    ]

    expect(cart.staleItems('USD').map(i => i.pid)).toEqual([3])
    // The stale item is still in the cart: removal is a user action, not automatic.
    expect(cart.items).toHaveLength(2)
  })

  it('treats every item as current when the payer currency is not yet known', () => {
    const cart = useCartStore()
    cart.items = [
      hostingItem({ pid: 1, amount: 10, currency: 'USD' }),
      hostingItem({ pid: 3, amount: 1200, currency: 'AMD' }),
    ]

    expect(cart.total(null)).toBe(1210)
    expect(cart.staleItems(null)).toEqual([])
  })

  describe('removeItem', () => {
    it('removes a hosting line by product and billing cycle, keeping the same plan on the other cycle', () => {
      const cart = useCartStore()
      cart.items = [
        hostingItem({ pid: 1, amount: 10, currency: 'USD', billingcycle: PricingCycle.Monthly }),
        hostingItem({ pid: 1, amount: 100, currency: 'USD', billingcycle: PricingCycle.Annual }),
      ]

      cart.removeItem(1, PricingCycle.Annual)

      expect(cart.items.map(i => i.billingcycle)).toEqual([PricingCycle.Monthly])
    })

    it('removes a domain line by name and action, keeping another domain on the same product', () => {
      const cart = useCartStore()
      /** Builds a domain registration line for the given name on the same product. */
      const domain = (name: string) => hostingItem({
        pid: 9, amount: 10, currency: 'USD', billingcycle: PricingCycle.Annual,
        itemType: CartItemType.Domain, domain: name, domainAction: DomainOrderAction.Register,
      })
      cart.items = [domain('a.com'), domain('b.com')]

      cart.removeItem(9, PricingCycle.Annual, 'a.com', DomainOrderAction.Register)

      expect(cart.items.map(i => i.domain)).toEqual(['b.com'])
    })
  })

  describe('refreshPrices', () => {
    /**
     * A catalogue product priced monthly and annually in the payer's currency.
     *
     * @param id Product id.
     * @param monthly Monthly price.
     * @param annual Annual price.
     * @returns The product.
     */
    const product = (id: number, monthly: number, annual: number): PortalProduct =>
      ({ id, groupId: 1, name: `Plan ${id}`, pricing: { monthly, annual } })

    it('re-prices hosting lines from the catalogue and saves the cart', () => {
      localStorage.clear()
      const cart = useCartStore()
      cart.items = [
        hostingItem({ pid: 1, amount: 10, currency: 'USD' }),
        hostingItem({ pid: 2, amount: 100, currency: 'USD', billingcycle: PricingCycle.Annual }),
      ]

      cart.refreshPrices([product(1, 12, 120), product(2, 8, 90)], 'USD')

      expect(cart.items.map(i => i.amount)).toEqual([12, 90])
      expect(JSON.parse(localStorage.getItem('innovayse_cart') ?? '[]')).toHaveLength(2)
    })

    it('leaves domain lines, other-currency lines and lines without a catalogue price alone', () => {
      const cart = useCartStore()
      const domain = hostingItem({
        pid: 9, amount: 10, currency: 'USD', itemType: CartItemType.Domain,
        domain: 'a.com', domainAction: DomainOrderAction.Register,
      })
      const stale = hostingItem({ pid: 3, amount: 1200, currency: 'AMD' })
      const gone = hostingItem({ pid: 4, amount: 7, currency: 'USD' })
      cart.items = [domain, stale, gone]

      cart.refreshPrices([product(9, 99, 99), product(3, 1, 1)], 'USD')

      expect(cart.items.map(i => i.amount)).toEqual([10, 1200, 7])
    })
  })

  describe('onlyDigital', () => {
    it('is true when every line in the payer currency is a digital good', () => {
      const cart = useCartStore()
      cart.items = [
        hostingItem({ pid: 1, amount: 10, currency: 'USD', productType: ProductType.Digital }),
        hostingItem({ pid: 2, amount: 5, currency: 'USD', productType: ProductType.Digital }),
      ]

      expect(cart.onlyDigital('USD')).toBe(true)
    })

    it('is false as soon as one line is hosting, or has no known type', () => {
      const cart = useCartStore()
      cart.items = [
        hostingItem({ pid: 1, amount: 10, currency: 'USD', productType: ProductType.Digital }),
        hostingItem({ pid: 2, amount: 5, currency: 'USD', productType: ProductType.SharedHosting }),
      ]
      expect(cart.onlyDigital('USD')).toBe(false)

      cart.items = [hostingItem({ pid: 3, amount: 5, currency: 'USD' })]
      expect(cart.onlyDigital('USD')).toBe(false)
    })

    it('ignores lines left in another currency, which the order does not carry', () => {
      const cart = useCartStore()
      cart.items = [
        hostingItem({ pid: 1, amount: 10, currency: 'USD', productType: ProductType.Digital }),
        hostingItem({ pid: 2, amount: 1200, currency: 'AMD', productType: ProductType.SharedHosting }),
      ]

      expect(cart.onlyDigital('USD')).toBe(true)
    })

    it('is false for an empty cart', () => {
      expect(useCartStore().onlyDigital('USD')).toBe(false)
    })
  })

  describe('init', () => {
    beforeEach(() => {
      localStorage.clear()
    })

    it('loads a saved line with its product type', () => {
      const saved = [hostingItem({ pid: 1, amount: 10, currency: 'USD', productType: ProductType.Digital })]
      localStorage.setItem('innovayse_cart', JSON.stringify(saved))
      const cart = useCartStore()

      cart.init()

      expect(cart.items).toEqual(saved)
    })

    it('still loads a line saved before the product type existed', () => {
      const saved = [hostingItem({ pid: 1, amount: 10, currency: 'USD' })]
      localStorage.setItem('innovayse_cart', JSON.stringify(saved))
      const cart = useCartStore()

      cart.init()

      expect(cart.items).toEqual(saved)
      expect(cart.items[0]?.productType).toBeUndefined()
    })

    it('drops a saved line whose product type is not one the catalogue knows', () => {
      const valid = hostingItem({ pid: 1, amount: 12, currency: 'USD' })
      localStorage.setItem('innovayse_cart', JSON.stringify([valid, { ...hostingItem({ pid: 2, amount: 5, currency: 'USD' }), productType: 'Mystery' }]))
      const cart = useCartStore()

      cart.init()

      expect(cart.items).toEqual([valid])
    })

    it('loads a valid saved cart unchanged', () => {
      const saved = [hostingItem({ pid: 1, amount: 10, currency: 'USD', billingcycle: PricingCycle.Annual })]
      localStorage.setItem('innovayse_cart', JSON.stringify(saved))
      const cart = useCartStore()

      cart.init()

      expect(cart.items).toEqual(saved)
    })

    it('loads a saved domain line with its action', () => {
      const saved = [hostingItem({
        pid: 9, amount: 10, currency: 'USD', billingcycle: PricingCycle.Annual,
        itemType: CartItemType.Domain, domain: 'a.com', domainAction: DomainOrderAction.Transfer, years: 1
      })]
      localStorage.setItem('innovayse_cart', JSON.stringify(saved))
      const cart = useCartStore()

      cart.init()

      expect(cart.items).toEqual(saved)
    })

    it('drops a saved line that carries account credentials', () => {
      const valid = hostingItem({ pid: 1, amount: 12, currency: 'USD' })
      localStorage.setItem('innovayse_cart', JSON.stringify([
        valid,
        { ...hostingItem({ pid: 2, amount: 5, currency: 'USD' }), username: 'u' },
        { ...hostingItem({ pid: 3, amount: 5, currency: 'USD' }), password: 'p' },
      ]))
      const cart = useCartStore()

      cart.init()

      expect(cart.items).toEqual([valid])
      expect(localStorage.getItem('innovayse_cart')).not.toContain('password')
    })

    it('drops items with an unknown cycle or a broken shape and saves the cleaned cart', () => {
      const valid = hostingItem({ pid: 1, amount: 12, currency: 'USD' })
      localStorage.setItem('innovayse_cart', JSON.stringify([
        valid,
        { ...hostingItem({ pid: 2, amount: 5, currency: 'USD' }), billingcycle: 'weekly' },
        { ...hostingItem({ pid: 3, amount: 5, currency: 'USD' }), pid: '3' },
        { name: 'No price', billingcycle: PricingCycle.Monthly },
        null,
      ]))
      const cart = useCartStore()

      cart.init()

      expect(cart.items).toEqual([valid])
      const stored = JSON.parse(localStorage.getItem('innovayse_cart') ?? '[]') as CartItem[]
      expect(stored).toEqual([valid])
    })
  })
})
