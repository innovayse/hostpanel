/**
 * Partner type: one partner company served by `GET /api/public/partners`.
 *
 * @module types/partner
 */

/**
 * A partner as the storefront receives it, already localised by the API.
 */
export interface Partner {
  /** Partner identifier. */
  id: number
  /** The partner's name in the request language. */
  name: string
  /** URL of the partner's logo image, or null. */
  logoUrl: string | null
  /** The partner's website, or null. */
  url: string | null
  /** Display order, ascending. */
  sortOrder: number
}
