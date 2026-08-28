/**
 * The signed-in client account, as `GET /api/portal/client/me` answers with it.
 *
 * Field names are the backend's own (WHMCS-derived, lowercase and unseparated); they are kept
 * verbatim rather than camel-cased so a reader can match a field here against the API response
 * without a translation table.
 */
export interface ClientUser {
  /** Client primary key. */
  id: number
  /** Given name. */
  firstname: string
  /** Family name. */
  lastname: string
  /** Company the account bills as, when it bills as one. */
  companyname?: string
  /** Account email — also the sign-in identifier. */
  email: string
  /** Contact phone number as entered. */
  phonenumber?: string
  /** First line of the billing address. */
  address1?: string
  /** Second line of the billing address. */
  address2?: string
  /** Billing address city. */
  city?: string
  /** Billing address state or region. */
  state?: string
  /** Billing address postal code. */
  postcode?: string
  /** ISO 3166-1 alpha-2 country code, e.g. "AM" */
  country?: string
  /** Full country name, e.g. "Armenia" */
  countryname?: string
  /** Default payment gateway module name (e.g. "paypal", "stripe") */
  defaultgateway?: string
  /** WHMCS language preference (e.g. "english", "russian") */
  language?: string
  /** Per-category email opt-in flags — 1 = receives emails, 0 = opted out */
  email_preferences?: {
    /** General account mail. */
    general: 0 | 1
    /** Invoice and billing mail. */
    invoice: 0 | 1
    /** Support ticket mail. */
    support: 0 | 1
    /** Product and service mail. */
    product: 0 | 1
    /** Domain lifecycle mail. */
    domain: 0 | 1
    /** Affiliate programme mail. */
    affiliate: 0 | 1
  }
  /** Numeric id of the account's billing currency. */
  currency?: number
  /** Symbol printed before an amount in that currency. */
  currencyprefix?: string
  /** Symbol printed after an amount in that currency. */
  currencysuffix?: string
  /** User permissions as bit-flags integer (8191 = All). */
  permissions: number
  /** Whether TOTP two-factor authentication is switched on for this account. */
  twoFactorEnabled?: boolean
}
