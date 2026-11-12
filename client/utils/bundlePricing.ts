/**
 * The estimated price of a bundle: the discount the storefront shows before the order is placed.
 *
 * Only an estimate. The backend validates the bundle and applies the discount itself when the
 * order is placed, and the checkout asks the payer to confirm when its total differs from this.
 *
 * @module utils/bundlePricing
 */

import { BundleDiscountType } from '~/types/bundlediscounttype'
import type { CartBundle } from '~/types/cartbundle'

/** Percent in a whole. */
const PERCENT = 100

/** Minor units kept when a percentage discount is rounded. */
const CENTS = 100

/**
 * The discount a bundle takes off a subtotal.
 *
 * A fixed discount counts only in its own currency: nothing on the frontend converts money, so a
 * payer in another currency is shown no fixed discount rather than a converted one. The discount
 * never exceeds the subtotal.
 *
 * @param bundle The bundle the cart belongs to.
 * @param subtotal The sum of the bundle's lines, in `currency`.
 * @param currency The ISO 4217 code the subtotal is in; null when the cart is not tied to one.
 * @returns The amount to subtract, zero for none.
 */
export const bundleDiscount = (bundle: CartBundle, subtotal: number, currency: string | null): number => {
  if (bundle.discountType === BundleDiscountType.Percentage) {
    return Math.min(subtotal, Math.round(subtotal * bundle.discountAmount / PERCENT * CENTS) / CENTS)
  }
  if (bundle.discountType === BundleDiscountType.Fixed && (!currency || bundle.discountCurrency === currency)) {
    return Math.min(subtotal, bundle.discountAmount)
  }
  return 0
}
