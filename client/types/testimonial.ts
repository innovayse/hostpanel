/**
 * Testimonial type: one client review served by `GET /api/public/testimonials`.
 *
 * @module types/testimonial
 */

/**
 * A client review as the storefront receives it, already localised by the API.
 */
export interface Testimonial {
  /** Testimonial identifier. */
  id: number
  /** The client's display name. */
  name: string
  /** The client's company, or null. */
  company: string | null
  /** The client's job title in the request language, or null. */
  role: string | null
  /** URL of the client's avatar image, or null. */
  avatarUrl: string | null
  /** The testimonial text in the request language. */
  quote: string
  /** Star rating, from 1 to 5. */
  rating: number
  /** Display order, ascending. */
  sortOrder: number
  /** ISO date shown with the testimonial: the admin-set date, or the creation time when none was set. */
  publishedAt: string
}
