/**
 * Project type: one portfolio project served by `GET /api/public/projects`.
 *
 * @module types/project
 */

/**
 * A portfolio project as the storefront receives it, already localised by the API.
 */
export interface Project {
  /** Project identifier. */
  id: number
  /** The project's URL-safe identifier. */
  slug: string
  /** The project's title in the request language. */
  title: string
  /** A one-line summary in the request language, or null. */
  summary: string | null
  /** The long description in the request language, or null. */
  description: string | null
  /** The project's image URLs, in display order. */
  imageUrls: string[]
  /** The live project's website, or null. */
  url: string | null
  /** Display order, ascending. */
  sortOrder: number
}
