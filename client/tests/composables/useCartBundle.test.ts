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
import { useCurrencyStore } from '~/stores/currency'
import { BundleDiscountType } from '~/types/bundlediscounttype'
import type { BillingCurrency } from '~/types/currency'
import { PricingCycle } from '~/types/pricingcycle'

/** The one currency the real store is given: a USD payer and dollars with two decimals. */
const usd: BillingCurrency = { code: 'USD', numeric: '840', prefix: '$', suffix: '', decimals: 2, isBase: true }

describe('useCartBundle', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    useCurrencyStore().currencies = [usd]
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
