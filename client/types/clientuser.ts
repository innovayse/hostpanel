/**
 * The signed-in client account, as `GET /api/portal/client/me` answers with it.
 *
 * @module types/clientuser
 */

import type { MyProfile } from '~/types/myprofile'
import type { PostalAddress } from '~/types/postaladdress'
import type { MyProfileNotifications } from '~/types/myprofilenotifications'

/**
 * The backend's own profile (`MyProfileDto`, field for field) plus what the portal adds for the
 * signed-in person. The record-only fields are null for an identity with no client record.
 */
export interface ClientUser extends Omit<MyProfile, 'id' | 'address' | 'notifications'> {
  /** Client primary key; null when the identity has no client record. */
  id: number | null
  /** Billing address; null when the identity has no client record. */
  address: PostalAddress | null
  /** Which emails the client receives; null when the identity has no client record. */
  notifications: MyProfileNotifications | null
  /**
   * False when the backend said this identity is not a customer account (a staff login with
   * no Client role or no client record), so every customer-only request would be refused.
   * True means the account may load its services, invoices, domains and tickets.
   */
  hasClientProfile: boolean
  /** The `ClientPermission` flags the person holds, as an integer (8191 = All). */
  permissions: number
  /**
   * True when the person acts for a client account (they own a client record, are linked to one,
   * or an identity plugin vouches for an external account). The permission flags only restrict
   * such a person; a signed-in first-time buyer with no account yet may place their first order.
   */
  actsForAccount: boolean
}
