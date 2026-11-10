/**
 * The body of `PUT /api/clients/{id}`: a full replacement of the client record, never a patch.
 * The API stores every flag the body leaves out as `false` and clears every optional text it
 * leaves out, so a caller always sends every group (see `toUpdateClientPayload`).
 */

import type { ClientPreferences, ClientSettings, ClientStatus } from '@/types/client'
import type { ContactNotifications } from '@/types/contactnotifications'
import type { PostalAddress } from '@/types/postaladdress'

/** Every field the client update endpoint replaces. */
export interface UpdateClientPayload {
  /** New sign-in email, or null to keep the address the account has. */
  email: string | null
  /** Client first name. */
  firstName: string
  /** Client last name. */
  lastName: string
  /** Company name, or null to clear it. */
  companyName: string | null
  /** Phone number, or null to clear it. */
  phone: string | null
  /** Billing address; a line that is null is cleared. */
  address: PostalAddress
  /**
   * Language (null leaves it alone), currency (null keeps the current one), payment method,
   * billing contact and admin notes (null clears each).
   */
  preferences: ClientPreferences
  /** Which kinds of email the client receives. */
  notifications: ContactNotifications
  /** The account switches staff set on the client. */
  settings: ClientSettings
  /** The status to move the client to, or null to keep the current one. */
  status: ClientStatus | null
  /** The client group to keep the client in; null takes the client out of its group, like every other optional field of this full replacement. */
  clientGroupId: number | null
}
