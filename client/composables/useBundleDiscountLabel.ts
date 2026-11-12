/**
 * The words that describe a bundle's discount, shared by every screen that shows one.
 *
 * @module composables/useBundleDiscountLabel
 */

import { useCurrencyStore } from '~/stores/currency'
import { BundleDiscountType } from '~/types/bundlediscounttype'
import type { CartBundle } from '~/types/cartbundle'
import { formatMoney } from '~/utils/formatMoney'

/** The discount members of a bundle, whichever shape carries them. */
type BundleDiscount = Pick<CartBundle, 'discountType' | 'discountAmount' | 'discountCurrency'>

/**
 * Builds the function that words a bundle's discount in the reader's language.
 *
 * A fixed amount is shown in the currency the operator set it in, never converted.
 *
 * @returns A function taking a bundle and answering e.g. "10% off", or an empty string without a discount.
 */
export const useBundleDiscountLabel = () => {
  const { t } = useI18n()
  const currencyStore = useCurrencyStore()

  /**
   * Words one bundle's discount.
   *
   * @param bundle - The bundle, or the cart's copy of it.
   * @returns The discount in words; empty when the bundle has none.
   */
  const label = (bundle: BundleDiscount): string => {
    if (bundle.discountType === BundleDiscountType.Percentage) {
      return t('bundles.discount.percentage', { amount: bundle.discountAmount })
    }
    if (bundle.discountType === BundleDiscountType.Fixed) {
      return t('bundles.discount.fixed', { amount: formatMoney(bundle.discountAmount, currencyStore.moneyCurrencyFor(bundle.discountCurrency)) })
    }
    return ''
  }

  return label
}
