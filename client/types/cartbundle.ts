/**
 * The bundle a cart was filled from: what the summary shows and the estimate subtracts.
 *
 * @module types/cartbundle
 */

import type { BundleDiscountType } from '~/types/bundlediscounttype'

/** The bundle the cart's lines belong to. */
export interface CartBundle {
  /** Bundle primary key; sent with the order as `bundleId`. */
  id: number
  /** Display name, for the cart and the order summary. */
  name: string
  /** How the bundle is discounted. */
  discountType: BundleDiscountType
  /** The discount: a percentage or a fixed amount, zero for none. */
  discountAmount: number
  /** The currency of a fixed discount; null otherwise. */
  discountCurrency: string | null
}
