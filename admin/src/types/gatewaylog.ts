/**
 * One entry of the payment-gateway log, the outcomes an entry can carry, and the filters the log
 * is listed by.
 */

/** Outcomes a gateway log entry reports; the values are the names the API sends. */
export enum GatewayLogResult {
  Payment = 'Payment',
  Refund = 'Refund',
  Credit = 'Credit',
  Pending = 'Pending',
}

/** A single recorded gateway interaction: a settled transaction or an open payment session. */
export interface GatewayLogEntry {
  /** Unique entry key (`tx-12` or `session-7`). */
  id: string
  /** ISO 8601 time of the interaction. */
  dateTime: string
  /** Name of the gateway. */
  gateway: string
  /** Invoice, gateway reference and notes, as text. */
  debugData: string
  /** How the interaction ended. */
  result: GatewayLogResult
  /** The invoice the entry belongs to, when it has one. */
  invoiceId: number | null
}

/** What narrows the gateway log: the search text, the period and the outcome; each member left out means no narrowing. */
export interface GatewayLogFilters {
  /** Search term over the gateway name and the debug text; empty for none. */
  search?: string
  /** First day kept, `YYYY-MM-DD`. */
  from?: string
  /** Last day kept, `YYYY-MM-DD`, the whole day included. */
  to?: string
  /** Keep only entries with this outcome. */
  result?: GatewayLogResult
}
