/**
 * The body of a profile save from the account page.
 *
 * @module types/profileupdate
 */

import type { ContactNotifications } from '~/types/contactnotifications'

/**
 * The members a client may change on their own profile, under the backend's camelCase names.
 * Every member is optional: one left out keeps the stored value. Staff-owned fields (currency,
 * admin notes, billing settings, status) are not part of it, and neither is the sign-in address,
 * which changes only through `EmailChangeInput` and a mailed confirmation.
 */
export interface ProfileUpdate {
  /** First name. */
  firstName?: string
  /** Last name. */
  lastName?: string
  /** Company name; empty clears it. */
  companyName?: string
  /** Phone number; empty clears it. */
  phone?: string
  /** Billing address; a line left out keeps its stored value, an empty one clears it. */
  address?: {
    /** Billing street. */
    street?: string
    /** Second address line. */
    address2?: string
    /** Billing city. */
    city?: string
    /** Billing state or region. */
    state?: string
    /** Billing postcode. */
    postCode?: string
    /** Billing country code (ISO 3166-1 alpha-2). */
    country?: string
  }
  /** UI language code; empty means no preference. */
  language?: string
  /** Default payment method. */
  paymentMethod?: string
  /** VAT number, with or without its country prefix; empty clears it. It is checked against the VAT number service once saved. */
  vatNumber?: string
  /** Email notification flags, replaced as a group. */
  notifications?: ContactNotifications
}
