/** One row of the TLD price table, formatted for display. */
export interface TldPriceRow {
  /** Extension with its leading dot, e.g. ".am". */
  tld: string
  /** Formatted one-year registration price, or a dash. */
  register: string
  /** Formatted one-year renewal price, or a dash. */
  renew: string
  /** Formatted one-year transfer price, or a dash. */
  transfer: string
  /** Category tags the backend assigns, used for filtering. */
  categories: string[]
  /** One-year registration price as a number, for cart arithmetic. */
  registerAmount: number
}
