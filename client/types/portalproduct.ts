/**
 * One catalogue product, as `GET /api/portal/public/products` returns it.
 *
 * The Nitro proxy adds `pid` and a WHMCS-compatible `pricing.USD` block on top of the C#
 * backend's `ProductDto`, so both spellings of the identifier are present.
 */
export interface PortalProduct {
  /** Product primary key. */
  id: number
  /** WHMCS-compatible alias of {@link PortalProduct.id}, added by the proxy. */
  pid?: number
  /** Product name. */
  name: string
  /** Marketing description; the feature bullets are parsed out of it. */
  description?: string | null
  /** URL slug, when the product has one. */
  slug?: string | null
  /** Product group id this product belongs to. */
  gid?: number
  /** Prices keyed by currency code — only USD is populated today. */
  pricing?: {
    /** Prices in USD. */
    USD?: {
      /** Symbol printed before an amount. */
      prefix?: string
      /** Symbol printed after an amount. */
      suffix?: string
      /** Monthly price as a decimal string. */
      monthly?: string
      /** Annual price as a decimal string. */
      annually?: string
    }
  }
}
