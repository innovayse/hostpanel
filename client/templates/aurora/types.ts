/** One row of the domain availability list rendered by the aurora domain search. */
export interface DomainResult {
  /** Fully qualified domain, e.g. "innovayse.am". */
  name: string
  /** Formatted price, or a dash when unavailable or unpriced. */
  price: string
  /**
   * Availability as reported by the registrar check.
   *
   * `unknown` is its own state rather than being folded into `taken`: a check
   * that failed — no registrar configured, registrar unreachable — is not the
   * same as a name someone owns, and showing it as taken hides the fault.
   */
  status: 'available' | 'taken' | 'unknown'
}

/**
 * A hosting plan as the aurora plan cards render it.
 *
 * Prices arrive pre-formatted: the page owns currency formatting because the
 * prefix comes from the API, and templates never fetch.
 *
 * The backend's ProductDto carries no feature list, so the design's per-card
 * bullet points have no data behind them. The section renders one shared
 * "included in every plan" list from i18n instead of inventing per-plan claims.
 */
export interface PlanCard {
  /** Product id, used as the list key and in the order link. */
  id: number
  /** Display name, e.g. "Business". */
  name: string
  /** Short description from the product record; may be empty. */
  description: string
  /** Formatted monthly price, e.g. "$4.99". */
  priceMonthly: string
  /** Formatted annual price expressed per month. */
  priceAnnual: string
  /** Route to begin ordering this plan. */
  href: string
}
