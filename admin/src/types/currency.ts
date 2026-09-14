/**
 * A configured currency, its display and rate shape, and the payloads the admin
 * currencies page sends back to `/api/admin/currencies`.
 */

/** A configured currency as the admin currencies page sees it, rate included. */
export interface CurrencyDto {
  /** ISO 4217 alpha code, upper case. */
  code: string
  /** ISO 4217 numeric code, three digits. */
  numeric: string
  /** Text printed before a formatted amount, e.g. "$". */
  prefix: string
  /** Text printed after a formatted amount, e.g. " ֏". */
  suffix: string
  /** Decimal places an amount is rounded and shown to. */
  decimals: number
  /** How much of the base one unit of this currency is worth. */
  rateToBase: number
  /** Whether this is the base currency; exactly one row is. */
  isBase: boolean
  /** Whether new clients may still choose it. */
  isEnabled: boolean
}

/** Body sent to `PUT /api/admin/currencies/{code}` to create or rewrite a currency. */
export interface UpsertCurrencyPayload {
  /** ISO 4217 numeric code, three digits. Read only on creation. */
  numeric: string
  /** Text printed before a formatted amount, e.g. "$". */
  prefix: string
  /** Text printed after a formatted amount, e.g. " ֏". */
  suffix: string
  /** Decimal places an amount is rounded and shown to. */
  decimals: number
  /** How much of the base one unit of this currency is worth; ignored for the base itself. */
  rateToBase: number
  /** Whether new clients may choose it. The base cannot be switched off. */
  isEnabled: boolean
}

/** Result of `POST /api/admin/currencies/update-rates`. */
export interface UpdateExchangeRatesResult {
  /** Codes whose rate was rewritten from the source's quote. */
  updated: string[]
  /** Codes the source did not quote; their stored rate was left alone. */
  missing: string[]
}
