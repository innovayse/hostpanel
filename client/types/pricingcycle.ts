/**
 * The billing cycles the backend prices, shortest first.
 *
 * The member values are the keys of `PortalProductPricing` (`monthly`, `quarterly`, `semiAnnual`,
 * `annual`, `biennial`), so a cycle can index a product's `pricing` directly. The i18n key for a
 * cycle is spelled differently (`annually`, `semiannually`) and is mapped in `constants/pricingCycle`.
 *
 * @module types/pricingcycle
 */

/** A billing cycle a plan is priced in. */
export enum PricingCycle {
  /** Billed every month. */
  Monthly = 'monthly',
  /** Billed every three months. */
  Quarterly = 'quarterly',
  /** Billed every six months. */
  SemiAnnual = 'semiAnnual',
  /** Billed once a year. */
  Annual = 'annual',
  /** Billed every two years. */
  Biennial = 'biennial',
}
