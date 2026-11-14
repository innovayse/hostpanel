/**
 * A tax rule and the body the admin tax rules page sends to `/api/admin/tax-rules`.
 */

/** A tax rule as the admin tax rules page sees it. */
export interface TaxRuleDto {
  /** Rule primary key. */
  id: number
  /** Display name of the rule. */
  name: string
  /** ISO 3166-1 alpha-2 country code, upper case. */
  country: string
  /** The region the rule is limited to, or null for a country-wide rule. */
  region: string | null
  /** Tax rate in percent, from 0 to 100. */
  ratePercent: number
  /** Whether new invoices use the rule. */
  isEnabled: boolean
}

/** What narrows the tax rule list: the search text and the enabled filter; each member left out means no narrowing. */
export interface TaxRuleListFilters {
  /** Search term over the name, country and region; empty for none. */
  search?: string
  /** Keep only the rules new invoices use (true) or the ones switched off (false). */
  enabled?: boolean
}

/** Filter and paging of the tax rule list. */
export interface TaxRuleFilter extends TaxRuleListFilters {
  /** Page, 1-based. */
  page: number
  /** Items per page. */
  pageSize: number
}

/** Body of `POST /api/admin/tax-rules` and `PUT /api/admin/tax-rules/{id}`: every field of a rule except its id. */
export interface SaveTaxRulePayload {
  /** Display name of the rule. */
  name: string
  /** ISO 3166-1 alpha-2 country code. */
  country: string
  /** The region the rule is limited to, or null for the whole country. */
  region: string | null
  /** Tax rate in percent, from 0 to 100. */
  ratePercent: number
  /** Whether new invoices use the rule. */
  isEnabled: boolean
}
