/** One billing currency, as `GET /api/portal/public/currencies` lists it. */
export interface PortalCurrency {
  /** Currency primary key — what a client profile's `currency` field points at. */
  id: number
  /** ISO 4217 code, e.g. "USD". */
  code: string
  /** Symbol printed before an amount. */
  prefix: string
  /** Symbol printed after an amount. */
  suffix: string
}
