/**
 * Server location type: one data-centre location served by `GET /api/public/locations`.
 *
 * @module types/serverlocation
 */

/** A server location as the storefront receives it, already localised by the API. */
export interface ServerLocation {
  /** Location identifier. */
  id: number
  /** ISO 3166 alpha-2 country code, upper case. */
  countryCode: string
  /** The location name in the request language. */
  name: string
  /** The city, or null. */
  city?: string | null
  /** A note on latency, or null. */
  latencyNote?: string | null
}
