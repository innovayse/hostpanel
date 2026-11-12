/**
 * Client groups: the named groups staff put clients into, as `GET /api/admin/client-groups`
 * returns them, and the body the create and update calls take.
 */

/** A client group with how many clients belong to it. */
export interface ClientGroup {
  /** Group primary key. */
  id: number
  /** Display name. */
  name: string
  /** Free-text description, null when there is none. */
  description: string | null
  /** Discount members are due, in percent from 0 to 100. */
  discountPercent: number
  /** Colour the panel shows the group in, as upper-case `#RRGGBB`. */
  color: string
  /** Whether new clients created without a group join this one. */
  isDefault: boolean
  /** How many clients belong to the group. */
  clientCount: number
}

/** The fields the create and update calls take; whether a group is the default is set by its own call. */
export interface ClientGroupPayload {
  /** Display name. */
  name: string
  /** Description, or null to leave it empty. */
  description: string | null
  /** Discount in percent from 0 to 100, with at most two decimals. */
  discountPercent: number
  /** Colour as `#RRGGBB`. */
  color: string
}
