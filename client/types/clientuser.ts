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
  /**
   * Chosen UI language, as one of the codes the backend supports (`en`, `ru`, `hy`), or
   * empty for "no preference". Null under `AUTH_MODE=sso`, where the SSO owns the person and
   * hostpanel holds no language for them at all -- which is not the same as English.
   */
  language?: string | null
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
  /**
   * The account's billing currency as an ISO 4217 code, e.g. `AMD`, or null when this
   * deployment holds no currency for the account.
   *
   * This was typed `number` — "numeric id of the account's billing currency" — describing a
   * WHMCS currencies table that has no counterpart here. `ClientDto.Currency` is and has
   * always been a `string?` holding the code, so the number was never a value that arrived.
   * The code is enough on its own: `utils/formatCurrency.ts` hands it to `Intl.NumberFormat`,
   * which places the symbol and picks the minor-unit count for the reader's locale.
   *
   * There is deliberately no `currencyprefix` / `currencysuffix` beside it. Neither exists
   * anywhere in the C# API, and while they were declared here the portal read two fields the
   * backend could never fill.
   */
  currency?: string | null
  /** User permissions as bit-flags integer (8191 = All). */
  permissions: number
  /** Whether TOTP two-factor authentication is switched on for this account. */
  twoFactorEnabled?: boolean
}
