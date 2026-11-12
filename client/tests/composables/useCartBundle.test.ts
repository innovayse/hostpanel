/**
 * Tests for `useCartBundle`: the cart's bundle as display text, empty without a bundle and with
 * the estimated discount in the payer's currency with one.
 *
 * @module tests/composables/useCartBundle.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useCartBundle } from '~/composables/useCartBundle'
import { useCartStore } from '~/stores/cart'
import { BundleDiscountType } from '~/types/bundlediscounttype'
import { PricingCycle } from '~/types/pricingcycle'

vi.mock('~/stores/currency', () => ({
  /**
   * Stands in for the currency store: a USD payer and dollar formatting.
   *
   * @returns The members the composable reads.
   */
  useCurrencyStore: () => ({ payerCode: 'USD', moneyCurrencyFor: () => ({ code: 'USD', prefix: '$', suffix: '', decimals: 2 }) }),
}))

describe('useCartBundle', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.stubGlobal('useI18n', () => ({ t: (key: string) => key }))
  })

  it('has no name and no wording while the cart holds no bundle', () => {
    const { bundleName, bundleOffer } = useCartBundle()

    expect(bundleName.value).toBe('')
    expect(bundleOffer.value).toBe('')
  })

  it('names the bundle and formats the estimated discount in the payer currency', () => {
    useCartStore().startBundle(
      { id: 7, name: 'Starter pack', discountType: BundleDiscountType.Percentage, discountAmount: 10, discountCurrency: null },
      [{ pid: 1, name: 'Hosting', billingcycle: PricingCycle.Monthly, cycleLabel: 'Monthly', amount: 50, currency: 'USD', bundleId: 7 }],
    )
    const { bundleName, bundleOffer, bundleDiscount } = useCartBundle()

    expect(bundleName.value).toBe('Starter pack')
    expect(bundleOffer.value).toBe('bundles.discount.percentage')
    expect(bundleDiscount.value).toBe('$5.00')
  })
})
