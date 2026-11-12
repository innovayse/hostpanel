/**
 * Unit tests for `utils/bundlePricing.ts`: the estimated discount of each discount kind.
 *
 * @module tests/utils/bundlePricing.test
 */

import { describe, expect, it } from 'vitest'
import { BundleDiscountType } from '~/types/bundlediscounttype'
import type { CartBundle } from '~/types/cartbundle'
import { bundleDiscount } from '~/utils/bundlePricing'

/**
 * Builds a bundle.
 *
 * @param overrides Fields that differ from the defaults.
 * @returns The bundle.
 */
const bundle = (overrides: Partial<CartBundle> = {}): CartBundle => ({
  id: 1,
  name: 'Starter pack',
  discountType: BundleDiscountType.None,
  discountAmount: 0,
  discountCurrency: null,
  ...overrides,
})

describe('bundleDiscount', () => {
  it('takes nothing off when the bundle has no discount', () => {
    expect(bundleDiscount(bundle(), 50, 'USD')).toBe(0)
  })

  it('takes a percentage of the subtotal, rounded to cents', () => {
    expect(bundleDiscount(bundle({ discountType: BundleDiscountType.Percentage, discountAmount: 15 }), 33.33, 'USD')).toBe(5)
  })

  it('takes a fixed amount in its own currency', () => {
    expect(bundleDiscount(bundle({ discountType: BundleDiscountType.Fixed, discountAmount: 7, discountCurrency: 'USD' }), 50, 'USD')).toBe(7)
  })

  it('takes no fixed amount from a payer in another currency, since nothing here converts money', () => {
    expect(bundleDiscount(bundle({ discountType: BundleDiscountType.Fixed, discountAmount: 7, discountCurrency: 'USD' }), 50, 'EUR')).toBe(0)
  })

  it('never takes more than the subtotal', () => {
    expect(bundleDiscount(bundle({ discountType: BundleDiscountType.Fixed, discountAmount: 70, discountCurrency: 'USD' }), 50, 'USD')).toBe(50)
  })
})
