/**
 * Tests for `useBundleDiscountLabel`: a percentage and a fixed discount are worded through the
 * locale, a fixed amount in its own currency, and a bundle without a discount has no words.
 *
 * @module tests/composables/useBundleDiscountLabel.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useBundleDiscountLabel } from '~/composables/useBundleDiscountLabel'
import { useCurrencyStore } from '~/stores/currency'
import { BundleDiscountType } from '~/types/bundlediscounttype'
import type { BillingCurrency } from '~/types/currency'

/** The one currency the real store is given: dollars with two decimals. */
const usd: BillingCurrency = { code: 'USD', numeric: '840', prefix: '$', suffix: '', decimals: 2, isBase: true }

describe('useBundleDiscountLabel', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    useCurrencyStore().currencies = [usd]
    vi.stubGlobal('useI18n', () => ({
      t: (key: string, params: Record<string, unknown> = {}) => `${key}:${String(params.amount ?? '')}`,
    }))
  })

  it('words a percentage with the number', () => {
    const label = useBundleDiscountLabel()

    expect(label({ discountType: BundleDiscountType.Percentage, discountAmount: 15, discountCurrency: null })).toBe('bundles.discount.percentage:15')
  })

  it('words a fixed discount with the amount formatted in its currency', () => {
    const label = useBundleDiscountLabel()

    expect(label({ discountType: BundleDiscountType.Fixed, discountAmount: 7, discountCurrency: 'USD' })).toBe('bundles.discount.fixed:$7.00')
  })

  it('has no words for a bundle without a discount', () => {
    const label = useBundleDiscountLabel()

    expect(label({ discountType: BundleDiscountType.None, discountAmount: 0, discountCurrency: null })).toBe('')
  })
})
