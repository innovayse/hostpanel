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
  /** Monthly and annual pricing. */
  pricing: { monthly: number; annual: number }
  /** Optional FK to the server group for provisioning. */
  serverGroupId: number | null
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
  /** Monthly price. */
  monthlyPrice: number
  /** Annual price. */
  annualPrice: number
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
  /** Monthly price. */
  monthlyPrice: number
  /** Annual price. */
  annualPrice: number
  /** Optional FK to the server group for provisioning. */
  serverGroupId: number | null
}
