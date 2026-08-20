import type { ComparisonRow } from '~/composables/useProductFeatures'

export type { ComparisonRow }

/**
 * One navigation entry.
 *
 * `to` is a path relative to the locale root; the components run it through
 * `useLocalePath`. Every entry must point at a route this portal actually has —
 * a link to a page that does not exist is a 404 the visitor blames on the host.
 */
export interface NavItem {
  /** Stable key, used for the list key and to mark the current entry. */
  key: string
  /** i18n key for the visible label. */
  labelKey: string
  /** Target path, pre-locale. */
  to: string
}

/**
 * A card that states one capability: an icon, a title and a body line.
 *
 * Used by the "why", performance, security and trust sections, which differ in
 * layout but not in the shape of what they render.
 */
export interface Feature {
  /** Stable key, used for the list key. */
  key: string
  /** Iconify name, e.g. `lucide:shield-check`. */
  icon: string
  /** i18n key for the title. */
  titleKey: string
  /** i18n key for the body; omitted where the section shows titles only. */
  bodyKey?: string
  /**
   * Operator setting that must be non-empty for this card to render, as a
   * `[settingKey, envKey]` pair for {@link usePortalSettings}.
   *
   * Present on every claim this codebase cannot verify on its own — NVMe
   * storage, a CDN, a malware scanner. The platform provisions accounts through
   * cPanel/WHM or CWP and has no idea what hardware sits behind them, so
   * printing those as facts would be inventing them. An operator who does offer
   * them turns the line on from Admin → Settings; everyone else never sees it.
   */
  requiresSetting?: readonly [key: string, envKey: string]
}

/** One audience card in the use-cases grid. */
export interface UseCase {
  key: string
  icon: string
  titleKey: string
  bodyKey: string
}

/** One question in the FAQ accordion. */
export interface FaqItem {
  key: string
  questionKey: string
  answerKey: string
}

/** One numbered step of the migration walkthrough. */
export interface MigrationStep {
  key: string
  titleKey: string
  bodyKey: string
}

/** One row of the control-panel preview: an icon, a label and a stated value. */
export interface DashboardRow {
  key: string
  icon: string
  labelKey: string
  valueKey: string
}

/**
 * One customer quote.
 *
 * No endpoint returns these today, so the array in `content.ts` is empty and
 * the section does not render. The shape is here so a future source can fill it
 * without the section being designed from scratch — and so nobody is tempted to
 * write a plausible-looking name into the markup in the meantime.
 */
export interface Testimonial {
  key: string
  /** Attributed person, as they gave it. Never a generated name. */
  name: string
  /** Role and company, optional. */
  role?: string
  /** The quote itself, verbatim. */
  quote: string
}

/**
 * A hosting plan as nova's pricing cards render it.
 *
 * Built from the {@link PlanCard} shape `usePortalPlans` returns, with the two
 * decisions the cards need layered on: which plan carries the "most popular"
 * badge and what the yearly saving is. Both are derived, never fetched — see
 * `pricing.ts`, which is where they are computed and tested.
 */
export interface NovaPlan {
  id: number
  name: string
  description: string
  features: string[]
  /** Formatted monthly price, or `—` when the backend priced neither. */
  priceMonthly: string
  /** Formatted annual price expressed per month. */
  priceAnnual: string
  /** Route that starts an order for this plan. */
  href: string
  /** Whether this plan carries the badge. At most one plan in a list has it. */
  popular: boolean
  /**
   * Whole-percent saving of the annual price against twelve monthly ones, or
   * `null` when the two prices do not support the claim. Never rendered unless
   * it is a positive number.
   */
  discountPercent: number | null
}
