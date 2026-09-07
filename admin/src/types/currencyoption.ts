/** Currency option returned by the reference/currencies endpoint. */
export interface CurrencyOption {
  /** ISO 4217 currency code (e.g. "USD"). */
  code: string
  /** Human-readable currency name. */
  name: string
  /** Currency symbol (e.g. "$"). */
  symbol: string
}
