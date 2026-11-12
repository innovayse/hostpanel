/**
 * One component of a bundle as the bundle screen shows it: the product, the cycles it is priced
 * on and the cycle the visitor chose.
 *
 * @module types/bundlecomponentrow
 */

import type { BundleCycleOption } from '~/types/bundlecycleoption'
import type { ProductType } from '~/types/producttype'

/** One component of a bundle with the visitor's cycle choice. */
export interface BundleComponentRow {
  /** The product's primary key. */
  productId: number
  /** The product's display name. */
  name: string
  /** Whether this is the bundle's primary component. */
  isPrimary: boolean
  /** The product's type, when the catalogue knows it. */
  productType?: ProductType
  /** The cycles the product is priced on; empty when the catalogue carries no price for it. */
  options: BundleCycleOption[]
  /** The cycle the visitor chose; null when the product has no priced cycle. */
  selected: BundleCycleOption | null
}
