/**
 * A currency as the storefront sees it, as `GET /api/currencies` (`PublicCurrencyDto`) sends
 * it: enough to offer and format it, and deliberately no exchange rate — the storefront is
 * shown stored prices, never converted ones.
 */
export interface BillingCurrency {
  /** ISO 4217 alpha code, upper case, e.g. `AMD`. */
  code: string
  /** ISO 4217 numeric code, three digits. */
  numeric: string
  /** Text printed before a formatted amount, e.g. `$`. */
  prefix: string
  /** Text printed after a formatted amount, e.g. ` ֏`. */
  suffix: string
  /** Decimal places an amount in this currency is shown to. */
  decimals: number
  /** Whether this is the base currency — the one a checkout offers by default. */
  isBase: boolean
}
