/**
 * Product bundles: products sold together as one offer, as `GET /api/products/bundles` returns
 * them, and the body the create and update calls take.
 */

import type { BundleDiscountType } from '@/types/productenums'

/** The fewest years of domain registration a bundle may include. */
export const BUNDLE_DOMAIN_YEARS_MIN = 1

/** The most years of domain registration a bundle may include. */
export const BUNDLE_DOMAIN_YEARS_MAX = 10

/** The largest percentage discount. */
export const BUNDLE_MAX_PERCENTAGE = 100

/** One product a bundle is made of, as the list answers it. */
export interface ProductBundleComponent {
  /** The product's primary key. */
  productId: number
  /** The product's display name. */
  name: string
  /** Whether this is the bundle's primary component. */
  isPrimary: boolean
}

/** A product bundle with every field and its components, the primary one first. */
export interface ProductBundle {
  /** Bundle primary key. */
  id: number
  /** Display name. */
  name: string
  /** Free-text description, null when there is none. */
  description: string | null
  /** Whether the storefront offers the bundle. */
  isActive: boolean
  /** Position in the list, lowest first. */
  sortOrder: number
  /** Whether a domain registration is included. */
  includesDomain: boolean
  /** Years of registration included; null when no domain is included. */
  domainYears: number | null
  /** How the bundle is discounted. */
  discountType: BundleDiscountType
  /** The discount: a percentage or a fixed amount, zero for none. */
  discountAmount: number
  /** The ISO 4217 code of a fixed discount; null otherwise. */
  discountCurrency: string | null
  /** The products the bundle is made of. */
  components: ProductBundleComponent[]
}

/** One component the create and update calls take. */
export interface ProductBundleItem {
  /** The catalogue product sold as this component; never a domain product. */
  productId: number
  /** Whether this is the bundle's primary component; exactly one component is. */
  isPrimary: boolean
}

/** The fields the create and update calls take. */
export interface ProductBundlePayload {
  /** Display name. */
  name: string
  /** Description, or null to leave it empty. */
  description: string | null
  /** Position in the list, lowest first; zero or more. */
  sortOrder: number
  /** Whether a domain registration is included. */
  includesDomain: boolean
  /** Years of registration included, 1 to 10; null when no domain is included. */
  domainYears: number | null
  /** How the bundle is discounted. */
  discountType: BundleDiscountType
  /** A percentage from 0 to 100, a fixed amount, or zero for no discount. */
  discountAmount: number
  /** The ISO 4217 code of a fixed discount; null for any other type. */
  discountCurrency: string | null
  /** The components: at least one product, none twice, exactly one primary. */
  items: ProductBundleItem[]
  /** Whether the storefront offers the bundle; sent on update only, a new bundle always starts active. */
  isActive?: boolean
}
