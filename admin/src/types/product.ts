/**
 * A sellable product, the group it sits in, and the payloads that create or update one.
 */


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
  /** Whether the product is its group's recommended plan; at most one per group stays featured. */
  isFeatured: boolean
  /** Operator-defined position inside the group, lowest first. */
  sortOrder: number
  /** Stock keeping unit, unique across products; null when the product has none. */
  sku: string | null
  /** Free-form JSON object text a fulfilment plugin reads; null when the product has none. */
  metadata: string | null
  /** Days a service stays usable after its due date before it is suspended, 0 to 365. */
  gracePeriodDays: number
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
  /** Marks the product as the group's recommended plan; saving it clears the flag on the group's others. */
  isFeatured: boolean
  /** Position inside the group, lowest first; zero or more. */
  sortOrder: number
  /** SKU; an empty string clears the stored one on update, omitted keeps it. */
  sku?: string
  /** Metadata as JSON object text, at most 16 KB; an empty string clears it on update, omitted keeps it. */
  metadata?: string
  /** Grace period in days, 0 to 365; omitted keeps the stored value on update. */
  gracePeriodDays?: number
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
  /** Marks the product as the group's recommended plan; saving it clears the flag on the group's others. */
  isFeatured: boolean
  /** Position inside the group, lowest first; zero or more. */
  sortOrder: number
  /** SKU; an empty string clears the stored one on update, omitted keeps it. */
  sku?: string
  /** Metadata as JSON object text, at most 16 KB; an empty string clears it on update, omitted keeps it. */
  metadata?: string
  /** Grace period in days, 0 to 365; omitted keeps the stored value on update. */
  gracePeriodDays?: number
}
