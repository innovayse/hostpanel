/**
 * The TLD price table, as `GET /api/portal/public/tld-pricing` returns it.
 *
 * Prices are keyed by registration period in years, as strings — "1", "2", … — which is why
 * a one-year price is read as `register['1']` rather than off a named field.
 */
export interface TldPricing {
  /** Currency every price below is quoted in. */
  currency: {
    /** ISO 4217 code, e.g. "AMD". */
    code: string
    /** Symbol printed before an amount. */
    prefix: string
  }
  /** One entry per offered extension, keyed by the extension. */
  pricing: Record<string, {
    /** Registration price per period. */
    register: Record<string, string>
    /** Transfer price per period; falls back to registration when absent. */
    transfer?: Record<string, string>
    /** Renewal price per period; falls back to registration when absent. */
    renew?: Record<string, string>
    /** Category tags the backend assigns, used for filtering. */
    categories?: string[]
  }>
}
