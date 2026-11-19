/** Active clients of one country. */
export interface ReportCountryRow {
  /** ISO-2 country code; null when the client country is unresolved. */
  country: string | null
  /** Number of active clients. */
  clientCount: number
  /** Revenue from the country clients. */
  totalRevenue: number
}
