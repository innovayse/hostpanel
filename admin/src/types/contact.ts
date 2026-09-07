import type { Domain } from './domain'
import type { Invoice } from './invoice'
import type { Product } from './product'

/** Contact record linked to a client account. */
export interface Contact {
  /** Contact primary key. */
  id: number
  /** First name. */
  firstName: string
  /** Last name. */
  lastName: string
  /** Optional company name. */
  companyName?: string
  /** Email address. */
  email: string
  /** Phone number. */
  phone?: string
  /** Contact type: Billing, Technical, or General. */
  type: string
  /** Street address. */
  street?: string
  /** Second address line. */
  address2?: string
  /** City. */
  city?: string
  /** State or region. */
  state?: string
  /** Postal code. */
  postCode?: string
  /** ISO 3166-1 alpha-2 country code. */
  country?: string
  /** General email notifications. */
  notifyGeneral: boolean
  /** Invoice email notifications. */
  notifyInvoice: boolean
  /** Support email notifications. */
  notifySupport: boolean
  /** Product email notifications. */
  notifyProduct: boolean
  /** Domain email notifications. */
  notifyDomain: boolean
  /** Affiliate email notifications. */
  notifyAffiliate: boolean
  /** ISO 8601 creation timestamp. */
  createdAt: string
}
