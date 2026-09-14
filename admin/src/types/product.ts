/**
 * A sellable product, the group it sits in, and the payloads that create or update one.
 */

import type { Domain } from './domain'

/** Represents a product/plan available for purchase. */
export interface Product {
  /** Unique product identifier. */
  id: number
  /** Parent product group ID. */
  groupId: number
  /** Product name. */
  name: string
  /** Optional description. */
  description: string | null
  /** Optional website URL. */
  website: string | null
  /** Optional URL-friendly slug. */
  slug: string | null
  /** Optional hosting package name used for provisioning. */
  packageName: string | null
  /** Product type (SharedHosting, Vps, Dedicated, Domain, Ssl, Other). */
  type: string
  /** Current status (Active, Inactive). */
  status: string
  /** Monthly and annual pricing in the caller's currency; null when the product has no price for that cycle. */
  pricing: { monthly: number | null; annual: number | null }
  /** Every stored price, one entry per currency the product sells in. */
  prices: ProductPrice[]
  /** Optional FK to the server group for provisioning. */
  serverGroupId: number | null
}

/** A product's stored prices in one currency. */
export interface ProductPrice {
  /** ISO 4217 alpha code. */
  currencyCode: string
  /** Monthly price, or null when the product does not bill monthly in this currency. */
  monthly: number | null
  /** Annual price, or null when the product does not bill annually in this currency. */
  annual: number | null
}

/** Represents a product group. */
export interface ProductGroup {
  /** Unique group identifier. */
  id: number
  /** Group display name. */
  name: string
  /** Optional description. */
  description: string | null
  /** Whether this group is publicly visible. */
  isActive: boolean
  /** Number of products in this group. */
  productCount: number
}

/** Payload for creating a new product. */
export interface CreateProductPayload {
  /** Parent group ID. */
  groupId: number
  /** Product name. */
  name: string
  /** Optional description. */
  description: string | null
  /** Optional website URL. */
  website: string | null
  /** Optional URL slug. */
  slug: string | null
  /** Optional hosting package name. */
  packageName: string | null
  /** Product type. */
  type: string
  /** The product's prices, one entry per currency it sells in; enabled rows only. */
  prices: ProductPrice[]
  /** Optional FK to the server group for provisioning. */
  serverGroupId: number | null
}

/** Payload for updating an existing product. */
export interface UpdateProductPayload {
  /** Product name. */
  name: string
  /** Optional description. */
  description: string | null
  /** Optional website URL. */
  website: string | null
  /** Optional URL slug. */
  slug: string | null
  /** Optional hosting package name. */
  packageName: string | null
  /** The product's prices after the update, one entry per currency it sells in; enabled rows only. */
  prices: ProductPrice[]
  /** Optional FK to the server group for provisioning. */
  serverGroupId: number | null
}
