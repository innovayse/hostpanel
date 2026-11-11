/** Payload for creating a new client via the admin API. */

import type { ClientPreferences, ClientSettings, ClientStatus } from '@/types/client'
import type { ContactNotifications } from '@/types/contactnotifications'
import type { PostalAddress } from '@/types/postaladdress'

/** Everything the admin create endpoint takes. */
export interface AdminCreateClientPayload {
  /** True to create a new Identity user alongside the client. */
  createNewUser: boolean
  /** Identity user ID to associate with, when createNewUser is false. */
  existingUserId?: string
  /** Email for the new user (required when createNewUser is true). */
  email?: string
  /** Password for the new user (required when createNewUser is true). */
  password?: string
  /** Client first name. */
  firstName: string
  /** Client last name. */
  lastName: string
  /** Optional company name. */
  companyName?: string
  /** Optional phone number. */
  phone?: string
  /** Billing address. */
  address: PostalAddress
  /** Language, ISO 4217 currency, payment method, billing contact and internal admin notes. */
  preferences: ClientPreferences
  /** The status the client starts in, or null to start it Active. */
  status: ClientStatus | null
  /** Which kinds of email the client receives. */
  notifications: ContactNotifications
  /** The account switches staff set on the client. */
  settings: ClientSettings
  /** The client group to put the client in; null or absent puts it in the default group, when one is set. */
  clientGroupId?: number | null
}
