/**
 * Unit tests for the billing-cycle choice of the storefront plan cards: which cycles are offered,
 * which one is in effect, the price a card shows and the segments of the toggle.
 */
import { describe, expect, it } from 'vitest'
import type { PlanCard } from '~/types/plancard'
import { PricingCycle } from '~/types/pricingcycle'
import { NO_CYCLE_PRICE, cardCyclePrice, cycleSegments, effectiveCycle, planCardCycles } from '~/utils/planCycles'

/**
 * Builds a plan card priced on the given cycles.
 *
 * @param id Product id.
 * @param cyclePrices Per-month price of each cycle the plan is priced on.
 * @returns The plan card.
 */
const card = (id: number, cyclePrices: PlanCard['cyclePrices']): PlanCard => ({
  id,
  name: `Plan ${id}`,
  description: '',
  features: [],
  isFree: false,
  priceMonthly: cyclePrices.monthly ?? NO_CYCLE_PRICE,
  priceAnnual: cyclePrices.annual ?? NO_CYCLE_PRICE,
  cyclePrices,
  href: `/configure/${id}`,
})

describe('planCardCycles', () => {
  it('lists every cycle some card is priced on, shortest first, whatever order the cards come in', () => {
    const plans = [
      card(1, { biennial: '$1.00', quarterly: '$3.00' }),
      card(2, { semiAnnual: '$2.00', monthly: '$4.00' }),
    ]

    expect(planCardCycles(plans)).toEqual([
      PricingCycle.Monthly,
      PricingCycle.Quarterly,
      PricingCycle.SemiAnnual,
      PricingCycle.Biennial,
    ])
  })

  it('leaves out a cycle no card is priced on and answers empty without cards', () => {
    expect(planCardCycles([card(1, { monthly: '$4.00', annual: '$3.00' })])).toEqual([PricingCycle.Monthly, PricingCycle.Annual])
    expect(planCardCycles([])).toEqual([])
  })
})

describe('effectiveCycle', () => {
  it('keeps the chosen cycle when it is offered', () => {
    expect(effectiveCycle([PricingCycle.Monthly, PricingCycle.Annual], PricingCycle.Annual)).toBe(PricingCycle.Annual)
  })

  it('falls back to the shortest offered cycle when the chosen one is not offered', () => {
    expect(effectiveCycle([PricingCycle.Quarterly, PricingCycle.Annual], PricingCycle.Monthly)).toBe(PricingCycle.Quarterly)
  })

  it('keeps the chosen cycle when nothing is offered', () => {
    expect(effectiveCycle([], PricingCycle.Monthly)).toBe(PricingCycle.Monthly)
  })
})

describe('cardCyclePrice', () => {
  it('returns the per-month price of the cycle, or a dash when the plan is not priced on it', () => {
    const plan = card(1, { monthly: '$4.00', quarterly: '$3.50' })

    expect(cardCyclePrice(plan, PricingCycle.Quarterly)).toBe('$3.50')
    expect(cardCyclePrice(plan, PricingCycle.Annual)).toBe(NO_CYCLE_PRICE)
  })
})

describe('cycleSegments', () => {
  it('builds one labelled, identified segment per cycle in the order given', () => {
    const segments = cycleSegments([PricingCycle.Monthly, PricingCycle.SemiAnnual], cycle => `label:${cycle}`, 'billing')

    expect(segments).toEqual([
      { value: PricingCycle.Monthly, label: 'label:monthly', testid: 'billing-monthly' },
      { value: PricingCycle.SemiAnnual, label: 'label:semiAnnual', testid: 'billing-semiAnnual' },
    ])
  })
})
