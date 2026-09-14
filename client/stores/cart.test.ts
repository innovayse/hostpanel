/**
 * Tests for `stores/cart.ts`'s currency-aware total and stale-item exclusion.
 *
 * Multi-currency pricing means a cart can hold items priced in different currencies — the
 * payer added something, then changed currency. The total must never silently sum across
 * currencies (that would add dollars to drams), and a stale item must be excluded rather than
 * dropped, so the summary can tell the payer to remove and re-add it.
 *
 * @module stores/cart.test
 */

import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useCartStore } from '~/stores/cart'
import type { CartItem } from '~/types/cartitem'

/** Builds a minimal hosting cart item priced in the given currency. */
function hostingItem(overrides: Partial<CartItem> & { pid: number; amount: number; currency: string }): CartItem {
  return {
    name: `Plan ${overrides.pid}`,
    billingcycle: 'monthly',
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
    // The stale item is still in the cart — removal is a user action, not automatic.
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
})
