/** OSS figures of one member state, rate and invoice currency. */
export interface ReportOssRow {
  /** ISO-2 country code; the screen names the country in the reader's language. */
  countryCode: string
  /** VAT rate in percent. */
  vatRate: number
  /** Taxed invoices paid in the quarter. */
  numberOfInvoices: number
  /** Value invoiced without VAT, less the net part of the quarter's refunds. */
  totalExclVat: number
  /** VAT collected, less the VAT part of the quarter's refunds. */
  totalVatCollected: number
  /** ISO 4217 code both totals are in. */
  currency: string
}

/** The OSS (One Stop Shop) report of one quarter. */
export interface ReportOss {
  /** First day of the quarter, `yyyy-MM-dd`. */
  periodStart: string
  /** Last day of the quarter, `yyyy-MM-dd`. */
  periodEnd: string
  /** One row per member state, rate and currency. */
  rows: ReportOssRow[]
}
