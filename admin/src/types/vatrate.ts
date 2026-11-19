/**
 * A VAT rate of an EU member state and the body the admin VAT rates page sends to `/api/admin/vat-rates`.
 */

/** Which of a member state's rates a row holds; the API spells it as the member name. */
export enum VatRateKind {
  /** The standard rate, the one a digital service is taxed at. */
  Standard = 'Standard',
  /** A reduced rate for a narrower class of supplies; stored for reference, never applied to an invoice automatically. */
  Reduced = 'Reduced',
}

/** Where a row stands today among the rows of its country and kind; the API spells it as the member name. */
export enum VatRateStatus {
  /** The row in force: its first day has come and no later row of the same country and kind has started. */
  Active = 'Active',
  /** A later row of the same country and kind has started, so this one no longer applies. */
  Superseded = 'Superseded',
  /** The row's first day has not come yet. */
  Upcoming = 'Upcoming',
}

/** A VAT rate as the admin VAT rates page sees it. */
export interface VatRateDto {
  /** Row primary key. */
  id: number
  /** ISO 3166-1 alpha-2 code of the member state, upper case. */
  country: string
  /** Whether the row holds the standard or a reduced rate. */
  kind: VatRateKind
  /** The rate in percent, from 0 to 100. */
  ratePercent: number
  /** First day the rate applies, `yyyy-MM-dd`. */
  validFrom: string
}

/** What narrows the VAT rate list: the search text and the filters of the filter dialog; each member left out means no narrowing. */
export interface VatRateListFilters {
  /** Search term over the country code; empty for none. */
  search?: string
  /** Keep only the rows of this kind. */
  kind?: VatRateKind
  /** Keep only the rows with this standing today. */
  status?: VatRateStatus
}

/** Filter and paging of the VAT rate list. */
export interface VatRateFilter extends VatRateListFilters {
  /** Page, 1-based. */
  page: number
  /** Items per page. */
  pageSize: number
}

/** Body of `POST /api/admin/vat-rates` and `PUT /api/admin/vat-rates/{id}`: every field of a row except its id. */
export interface SaveVatRatePayload {
  /** ISO 3166-1 alpha-2 code of an EU member state. */
  country: string
  /** Whether the row holds the standard or a reduced rate. */
  kind: VatRateKind
  /** The rate in percent, from 0 to 100. */
  ratePercent: number
  /** First day the rate applies, `yyyy-MM-dd`. */
  validFrom: string
}
