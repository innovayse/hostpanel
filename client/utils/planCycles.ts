/**
 * The billing-cycle choice of the storefront plan cards, shared by every template that offers one.
 *
 * Which cycles exist is decided by the plans themselves (`pricedCycles`), never by a list a
 * template keeps, so a plan priced quarterly or biennially is orderable from the home page too.
 *
 * @module utils/planCycles
 */

import type { PlanCard } from '~/types/plancard'
import type { PricingCycle } from '~/types/pricingcycle'
import type { SegmentedOption } from '~/types/segmentedoption'
import { pricedCycles } from '~/utils/productFamilies'

/** What a card shows for a cycle its plan is not priced on. */
export const NO_CYCLE_PRICE = '—'

/**
 * The cycles at least one of the cards is priced on, shortest first.
 *
 * @param plans The plan cards on show.
 * @returns The cycles the toggle offers; empty when no card carries a price.
 */
export const planCardCycles = (plans: readonly PlanCard[]): PricingCycle[] =>
  pricedCycles(plans, (plan, cycle) => plan.cyclePrices[cycle] !== undefined)

/**
 * The cycle to show prices for: the visitor's pick when some plan is priced on it, otherwise the
 * shortest cycle on offer, so the toggle never highlights a cycle it does not list.
 *
 * @param cycles The cycles on offer.
 * @param selected The cycle the visitor chose.
 * @returns The cycle in effect.
 */
export const effectiveCycle = (cycles: readonly PricingCycle[], selected: PricingCycle): PricingCycle =>
  cycles.includes(selected) ? selected : (cycles[0] ?? selected)

/**
 * A card's per-month price for a cycle.
 *
 * @param plan The plan card.
 * @param cycle The cycle in effect.
 * @returns The formatted price, or a dash when the plan is not priced on that cycle.
 */
export const cardCyclePrice = (plan: PlanCard, cycle: PricingCycle): string => plan.cyclePrices[cycle] ?? NO_CYCLE_PRICE

/**
 * The segments of a cycle toggle.
 *
 * @param cycles The cycles on offer.
 * @param label Translates a cycle into its visible label.
 * @param testidPrefix Prefix of each segment's `data-testid`; the cycle is appended.
 * @returns One segment per cycle, in the order given.
 */
export const cycleSegments = (
  cycles: readonly PricingCycle[],
  label: (cycle: PricingCycle) => string,
  testidPrefix: string,
): SegmentedOption<PricingCycle>[] =>
  cycles.map(cycle => ({ value: cycle, label: label(cycle), testid: `${testidPrefix}-${cycle}` }))
