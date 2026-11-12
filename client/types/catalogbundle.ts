/**
 * One product bundle, as `GET /api/portal/public/product-bundles` returns it.
 *
 * The route forwards the C# `CatalogProductBundleDto` verbatim: only active bundles, in the order
 * the operator set. The storefront shows the discount and an estimated total; the backend
 * validates the bundle and applies the discount when the order is placed.
 *
 * @module types/catalogbundle
 */

import type { BundleDiscountType } from '~/types/bundlediscounttype'
import type { CatalogBundleComponent } from '~/types/catalogbundlecomponent'

/** One product bundle the storefront offers. */
export interface CatalogBundle {
  /** Bundle primary key. */
  id: number
  /** Display name. */
  name: string
  /** Optional description. */
  description: string | null
  /** Whether a domain registration or transfer is included. */
  includesDomain: boolean
  /** Years of registration included; null when no domain is included. */
  domainYears: number | null
  /** How the bundle is discounted. */
  discountType: BundleDiscountType
  /** The discount: a percentage or a fixed amount, zero for none. */
  discountAmount: number
  /** The currency of a fixed discount; null otherwise. */
  discountCurrency: string | null
  /** The products the bundle is made of, the primary one first. */
  components: CatalogBundleComponent[]
}
