/**
 * Unit tests for PricingSection: the billing toggle, the plan cards and the empty state.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import PricingSection from '~/templates/nova/sections/PricingSection.vue'
import type { PlanCard } from '~/types/plancard'
import { PricingCycle } from '~/types/pricingcycle'

/**
 * Builds a plan card as the catalogue store formats it.
 *
 * @param id Product id.
 * @returns A plan card.
 */
const card = (id: number): PlanCard => ({
  id,
  name: `Plan ${id}`,
  description: '',
  features: [],
  isFree: false,
  priceMonthly: `$${id}.00`,
  priceAnnual: `$${id}.00`,
  cyclePrices: { monthly: `$${id}.00`, annual: `$${id}.00` },
  href: `/configure/${id}`,
})

describe('PricingSection', () => {
  it('renders one card per plan', () => {
    const wrapper = mountWithKit(PricingSection, { props: { plans: [card(1), card(2), card(3)] } })

    expect(wrapper.findAll('article')).toHaveLength(3)
    expect(wrapper.find('[data-testid="nova-plans-empty"]').exists()).toBe(false)
  })

  it('says so when the catalogue returned nothing', () => {
    const wrapper = mountWithKit(PricingSection, { props: { plans: [] } })

    expect(wrapper.find('[data-testid="nova-plans-empty"]').text()).toContain('nova.plans.empty')
  })

  it('marks the selected billing cycle as checked', () => {
    const plans = [card(1), card(2)]
    const monthly = mountWithKit(PricingSection, { props: { plans, cycle: PricingCycle.Monthly } })
    const annual = mountWithKit(PricingSection, { props: { plans, cycle: PricingCycle.Annual } })

    expect(monthly.find('[data-testid="nova-billing-monthly"]').attributes('aria-checked')).toBe('true')
    expect(annual.find('[data-testid="nova-billing-annual"]').attributes('aria-checked')).toBe('true')
  })

  it('reports the chosen cycle upward', async () => {
    const plans = [card(1), card(2)]
    const monthly = mountWithKit(PricingSection, { props: { plans, cycle: PricingCycle.Monthly } })
    const annual = mountWithKit(PricingSection, { props: { plans, cycle: PricingCycle.Annual } })

    await monthly.find('[data-testid="nova-billing-annual"]').trigger('click')
    await annual.find('[data-testid="nova-billing-monthly"]').trigger('click')

    expect(monthly.emitted('update:cycle')).toEqual([[PricingCycle.Annual]])
    expect(annual.emitted('update:cycle')).toEqual([[PricingCycle.Monthly]])
  })

  it('offers every cycle the plans are priced on, shortest first, and no other', () => {
    const plans = [
      { ...card(1), cyclePrices: { monthly: '$1.00', biennial: '$0.50' } },
      { ...card(2), cyclePrices: { quarterly: '$2.00', semiAnnual: '$1.80', annual: '$1.60' } },
    ]
    const wrapper = mountWithKit(PricingSection, { props: { plans } })

    expect(wrapper.findAll('[data-testid="nova-billing-toggle"] [role="radio"]').map(segment => segment.attributes('data-testid'))).toEqual([
      'nova-billing-monthly',
      'nova-billing-quarterly',
      'nova-billing-semiAnnual',
      'nova-billing-annual',
      'nova-billing-biennial',
    ])
  })

  it('falls back to the shortest offered cycle when no plan is priced on the chosen one', () => {
    const plans = [{ ...card(1), cyclePrices: { quarterly: '$2.00', annual: '$1.60' } }, card(2)]
    const wrapper = mountWithKit(PricingSection, { props: { plans, cycle: PricingCycle.Biennial } })

    expect(wrapper.find('[data-testid="nova-billing-monthly"]').attributes('aria-checked')).toBe('true')
  })

  it('hides the toggle when the plans offer a single cycle', () => {
    const plans = [{ ...card(1), cyclePrices: { monthly: '$1.00' } }]

    expect(mountWithKit(PricingSection, { props: { plans } }).find('[data-testid="nova-billing-toggle"]').exists()).toBe(false)
  })

  it('leaves the heading out when the page supplies its own', () => {
    expect(mountWithKit(PricingSection, { props: { showHeader: true } }).find('h2').exists()).toBe(true)
    expect(mountWithKit(PricingSection, { props: { showHeader: false } }).find('h2').exists()).toBe(false)
  })
})
