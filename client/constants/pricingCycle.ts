/**
 * What the storefront needs to know about the billing cycles the backend prices.
 *
 * The `PricingCycle` values are the keys of a product's `pricing`, but the locale files spell the
 * yearly cycle `annually` and the six-month one `semiannually`, so every page that names a cycle
 * goes through this one map instead of keeping its own copy.
 *
 * @module constants/pricingCycle
 */

import { BillingCycle } from '~/types/billingcycle'
import { PricingCycle } from '~/types/pricingcycle'

/** The cycles a plan can be sold in, shortest first, which is the order they are offered. */
export const PRICING_CYCLES: readonly PricingCycle[] = [
  PricingCycle.Monthly,
  PricingCycle.Quarterly,
  PricingCycle.SemiAnnual,
  PricingCycle.Annual,
  PricingCycle.Biennial,
]

/** Maps a cycle to the last segment of its `hosting.cycles.*` / `products.pricing.cycles.*` locale key. */
export const PRICING_CYCLE_I18N_KEY: Record<PricingCycle, string> = {
  [PricingCycle.Monthly]: 'monthly',
  [PricingCycle.Quarterly]: 'quarterly',
  [PricingCycle.SemiAnnual]: 'semiannually',
  [PricingCycle.Annual]: 'annually',
  [PricingCycle.Biennial]: 'biennially',
}

/** Maps the cycle of a service the client owns to the last segment of its `products.pricing.cycles.*` locale key. */
export const BILLING_CYCLE_I18N_KEY: Record<BillingCycle, string> = {
  [BillingCycle.Monthly]: PRICING_CYCLE_I18N_KEY[PricingCycle.Monthly],
  [BillingCycle.Quarterly]: PRICING_CYCLE_I18N_KEY[PricingCycle.Quarterly],
  [BillingCycle.SemiAnnual]: PRICING_CYCLE_I18N_KEY[PricingCycle.SemiAnnual],
  [BillingCycle.Annual]: PRICING_CYCLE_I18N_KEY[PricingCycle.Annual],
  [BillingCycle.Biennial]: PRICING_CYCLE_I18N_KEY[PricingCycle.Biennial],
}

/** Months in a year, which turns an annual price into a per-month one. */
export const MONTHS_PER_YEAR = 12

/** How many months one payment of each cycle covers, which turns any cycle's price into a per-month one. */
export const PRICING_CYCLE_MONTHS: Record<PricingCycle, number> = {
  [PricingCycle.Monthly]: 1,
  [PricingCycle.Quarterly]: 3,
  [PricingCycle.SemiAnnual]: 6,
  [PricingCycle.Annual]: MONTHS_PER_YEAR,
  [PricingCycle.Biennial]: 2 * MONTHS_PER_YEAR,
}

/** Maps a cycle to the ISO 8601 duration one payment covers, for the structured data of an offer. */
export const PRICING_CYCLE_ISO_DURATION: Record<PricingCycle, string> = {
  [PricingCycle.Monthly]: 'P1M',
  [PricingCycle.Quarterly]: 'P3M',
  [PricingCycle.SemiAnnual]: 'P6M',
  [PricingCycle.Annual]: 'P1Y',
  [PricingCycle.Biennial]: 'P2Y',
}

/** The billing cycle a domain registration is added to the cart with: domains are sold by the year. */
export const DOMAIN_BILLING_CYCLE = PricingCycle.Annual
