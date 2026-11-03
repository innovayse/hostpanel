/**
 * The product's price in the caller's own currency, as `ProductPricingDto` sends it.
 *
 * @module types/portalproductpricing
 */

/**
 * The product's price in the caller's own currency, as `ProductPricingDto` sends it.
 *
 * Every field is `null` when the product carries no price for that cycle in that currency (the
 * cycles added after monthly and annual may also be absent, as from an API that predates them): the
 * storefront hides such a product for the cycle that is null (`GET /products` without
 * `includeUnsellable` already excludes a product with no price in any currency, but a product
 * priced only annually, say, still needs the monthly card hidden).
 */
export interface PortalProductPricing {
  /** Monthly price in the caller's currency, or null when not offered. */
  monthly: number | null
  /** Quarterly price in the caller's currency, or null or absent when not offered. */
  quarterly?: number | null
  /** Semi-annual price in the caller's currency, or null or absent when not offered. */
  semiAnnual?: number | null
  /** Annual price in the caller's currency, or null when not offered. */
  annual: number | null
  /** Biennial price in the caller's currency, or null or absent when not offered. */
  biennial?: number | null
}
