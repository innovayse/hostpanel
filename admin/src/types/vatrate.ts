/**
 * A VAT rate of an EU member state and the body the admin VAT rates page sends to `/api/admin/vat-rates`.
 */

/** Which of a member state's rates a row holds; the API spells it as the member name. */
export enum VatRateKind {
  /** The standard rate, the one a digital service is taxed at. */
  Standard = 'Standard',
  /** A reduced rate for a narrower class of supplies. */
  Reduced = 'Reduced',
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

/** Filter and paging of the VAT rate list. */
export interface VatRateFilter {
  /** Page, 1-based. */
  page: number
  /** Items per page. */
  pageSize: number
  /** Search term over the country code; empty for none. */
  search?: string
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
