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
  /**
   * ISO 4217 code this TLD's own sell prices are set in. An order can only be placed in this
   * currency — see `TldPriceEntryDto`'s note. A visitor whose payer currency differs sees the
   * price (converted for display by the backend) but the add-to-cart action is refused.
   */
  sellCurrency: string
}
