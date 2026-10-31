/**
 * One plan on a product's detail page: a tier with prices for both cycles.
 *
 * @module types/productplan
 */

import type { PortalProductPricing } from '~/types/portalproductpricing'
import type { ProductType } from '~/types/producttype'

/** One plan on a product's detail page: a tier with prices for both cycles. */
export interface ProductPlan {
  /** Catalogue product id: the key of the comparison's specification lines, and what the cart and order link carry. */
  id: number
  /** Plan name, e.g. "Starter". */
  tier: string
  /** The plan's prices in the payer's currency; null when it carries none. */
  pricing: PortalProductPricing | null
  /** Leading prose of the plan description, tags stripped; empty when the copy is bullets only. */
  description: string
  /** Feature lines parsed from the plan description. */
  features: string[]
  /** True for the plan the operator marked as recommended. */
  isFeatured: boolean
  /** The catalogue product's type, when the API sent one; the cart keeps it to tell digital goods from hosting. */
  type?: ProductType
}
