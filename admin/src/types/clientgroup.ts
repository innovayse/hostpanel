/**
 * Client groups: the named groups staff put clients into, as `GET /api/admin/client-groups`
 * returns them.
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
