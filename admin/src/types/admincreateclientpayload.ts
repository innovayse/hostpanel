/** Payload for creating a new client via the admin API. */
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
  /** Street address line 1. */
  street?: string
  /** Street address line 2. */
  address2?: string
  /** City. */
  city?: string
  /** State or province. */
  state?: string
  /** Postal code. */
  postCode?: string
  /** ISO 3166-1 alpha-2 country code. */
  country?: string
  /** ISO 4217 currency code. */
  currency?: string
  /** Preferred language code. */
  language?: string
  /** Payment method identifier. */
  paymentMethod?: string
  /** Billing contact name or reference. */
  billingContact?: string
  /** Internal admin notes. */
  adminNotes?: string
  /** Client status (Active, Inactive, Suspended, Closed). */
  status?: string
  /** Whether the client receives general notification emails. */
  notifyGeneral: boolean
  /** Whether the client receives invoice notification emails. */
  notifyInvoice: boolean
  /** Whether the client receives support notification emails. */
  notifySupport: boolean
  /** Whether the client receives product notification emails. */
  notifyProduct: boolean
  /** Whether the client receives domain notification emails. */
  notifyDomain: boolean
  /** Whether the client receives affiliate notification emails. */
  notifyAffiliate: boolean
  /** Whether late fees are applied to overdue invoices. */
  lateFees: boolean
  /** Whether overdue notices are sent. */
  overdueNotices: boolean
  /** Whether the client is exempt from taxes. */
  taxExempt: boolean
  /** Whether invoices are generated separately per product. */
  separateInvoices: boolean
  /** Whether credit card processing is disabled. */
  disableCcProcessing: boolean
  /** Whether the client has opted in to marketing emails. */
  marketingOptIn: boolean
  /** Whether the client status updates are tracked. */
  statusUpdate: boolean
  /** Whether the client may use single sign-on. */
  allowSso: boolean
}
