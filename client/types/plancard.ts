/**
 * A hosting plan as the storefront templates render it in their plan cards.
 *
 * @module types/plancard
 */
import type { PricingCycle } from '~/types/pricingcycle'

/**
 * A hosting plan as a template's plan cards render it.
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
  /** Summary paragraph from the product record; may be empty. */
  description: string
  /** Feature lines parsed out of the same record; may be empty. */
  features: string[]
  /** True when the plan costs nothing, so it can be shown on its own terms. */
  isFree: boolean
  /** Formatted monthly price, e.g. "$4.99". */
  priceMonthly: string
  /** Formatted annual price expressed per month. */
  priceAnnual: string
  /**
   * Formatted price per month for every billing cycle the plan is priced on (a quarterly price
   * divided by three, and so on). A cycle the plan is not priced on has no entry.
   */
  cyclePrices: Partial<Record<PricingCycle, string>>
  /** Route to begin ordering this plan. */
  href: string
  /**
   * True for the plan the operator marked as recommended. The catalogue store lets at most one
   * card in a list carry it (the first featured plan in display order), so a card can trust it.
   */
  isFeatured?: boolean
}
