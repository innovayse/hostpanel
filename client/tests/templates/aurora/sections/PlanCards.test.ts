/**
 * Unit tests for PlanCards: the free plan on its own, equal-height paid cards, the billing toggle,
 * the header switch, the empty state and the shared "included" fallback.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import PlanCards from '~/templates/aurora/sections/PlanCards.vue'
import { balancedColumns } from '~/utils/balancedGrid'
import type { PlanCard } from '~/types/plancard'
import { stubAuroraGlobals, PLANS } from '../auroraGlobals'

describe('PlanCards', () => {
  it('pulls the free plan out of the grid and keeps the paid ones in it', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(PlanCards, { props: { plans: PLANS } })

    expect(wrapper.find('[data-testid="plan-free"]').text()).toContain('Free')
    expect(wrapper.find('[data-testid="plan-card-3"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plan-card-1"]').text()).toContain('Starter')
    expect(wrapper.find('[data-testid="plan-card-2"]').text()).toContain('Business')
  })

  it('links every plan button to its order route through the e2e ids', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(PlanCards, { props: { plans: PLANS } })

    expect(wrapper.find('[data-testid="plan-choose-1"]').attributes('href')).toBe('/configure/1')
    expect(wrapper.find('[data-testid="plan-choose-3"]').attributes('href')).toBe('/configure/3')
  })

  it('keeps the paid cards the same height and pins price and button to the bottom', () => {
    stubAuroraGlobals()
    const card = mountWithKit(PlanCards, { props: { plans: PLANS } }).find('[data-testid="plan-card-1"]')

    expect(card.classes()).toEqual(expect.arrayContaining(['h-full', 'flex', 'flex-col']))
    expect(card.find('.mt-auto').exists()).toBe(true)
  })

  it('frames every plan card with a theme token and a hover state, never an opacity-modified accent', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(PlanCards, { props: { plans: PLANS } })
    const free = wrapper.find('[data-testid="plan-free"]').classes()
    const paid = wrapper.find('[data-testid="plan-card-1"]').classes()

    // border-ac1/25 emits no CSS (ac1 is a var()), which left the grey default border in dark mode.
    expect(free).toContain('border-line-brand')
    expect(free).not.toContain('border-ac1/25')
    expect(paid).toContain('border-line')
    expect(free).toContain('hover:border-ac1')
    expect(paid).toContain('hover:border-ac1')
  })

  it('shows the monthly price by default and the per-month annual price when yearly', () => {
    stubAuroraGlobals()

    expect(mountWithKit(PlanCards, { props: { plans: PLANS } }).find('[data-testid="plan-card-1"]').text()).toContain('$4.99')
    expect(mountWithKit(PlanCards, { props: { plans: PLANS, yearly: true } }).find('[data-testid="plan-card-1"]').text()).toContain('$3.99')
  })

  it('reports the billing period the visitor picks', async () => {
    stubAuroraGlobals()

    const monthly = mountWithKit(PlanCards, { props: { plans: PLANS } })
    await monthly.find('[data-testid="billing-yearly"]').trigger('click')
    const yearly = mountWithKit(PlanCards, { props: { plans: PLANS, yearly: true } })
    await yearly.find('[data-testid="billing-monthly"]').trigger('click')

    expect(monthly.emitted('update:yearly')).toEqual([[true]])
    expect(yearly.emitted('update:yearly')).toEqual([[false]])
  })

  it('renders the toggle as the kit segmented control with the active period checked', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(PlanCards, { props: { plans: PLANS, yearly: true } })
    const active = wrapper.find('[data-testid="billing-yearly"]')

    expect(wrapper.find('[data-testid="billing-toggle"]').attributes('role')).toBe('radiogroup')
    expect(active.attributes('aria-checked')).toBe('true')
    expect(wrapper.find('[data-testid="billing-monthly"]').attributes('aria-checked')).toBe('false')
    expect(active.classes()).toEqual(expect.arrayContaining(['rounded-full', 'bg-brand', 'text-on-brand']))
    expect(active.classes()).not.toContain('rounded-lg')
  })

  it('drops the section headline when the page already has one', () => {
    stubAuroraGlobals()

    expect(mountWithKit(PlanCards, { props: { plans: PLANS } }).find('h2').text()).toBe('aurora.plans.title')
    expect(mountWithKit(PlanCards, { props: { plans: PLANS, showHeader: false } }).find('h2').exists()).toBe(false)
  })

  it('says so when there are no plans', () => {
    stubAuroraGlobals()

    expect(mountWithKit(PlanCards).find('[data-testid="plans-empty"]').text()).toBe('aurora.plans.empty')
  })

  it('lists what every plan includes only when no plan carries its own features', () => {
    stubAuroraGlobals()
    const bare = PLANS.filter((plan) => plan.features.length === 0)

    expect(mountWithKit(PlanCards, { props: { plans: bare } }).find('[data-testid="plans-included"]').exists()).toBe(true)
    expect(mountWithKit(PlanCards, { props: { plans: PLANS } }).find('[data-testid="plans-included"]').exists()).toBe(false)
  })

  /**
   * Builds `count` paid plans.
   *
   * @param count - How many plans.
   * @param overrides - Fields shared by every plan.
   * @returns The plans.
   */
  const paid = (count: number, overrides: Partial<PlanCard> = {}): PlanCard[] =>
    Array.from({ length: count }, (_, i) => ({ ...PLANS[0]!, id: 100 + i, name: `Plan ${i}`, href: `/configure/${100 + i}`, ...overrides }))

  it.each([1, 2, 5, 7])('balances %i plans in one centred grid with equal-height cards', (count) => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(PlanCards, { props: { plans: paid(count) } })
    const grid = wrapper.find('[data-testid="plan-grid"]').element as HTMLElement

    expect(wrapper.findAll('[data-testid^="plan-card-"]')).toHaveLength(count)
    expect(grid.getAttribute('data-columns')).toBe(String(balancedColumns(count)))
    expect(wrapper.find('[data-testid="plan-card-100"]').classes()).toContain('h-full')
  })

  it('caps the feature list and links the rest as "+N more"', () => {
    stubAuroraGlobals()
    const features = Array.from({ length: 9 }, (_, i) => `Feature ${i}`)
    const wrapper = mountWithKit(PlanCards, { props: { plans: paid(1, { features }), maxFeatures: 4 } })
    const card = wrapper.find('[data-testid="plan-card-100"]')

    expect(card.text()).toContain('Feature 3')
    expect(card.text()).not.toContain('Feature 4')
    expect(card.find('[data-testid="plan-more-100"]').exists()).toBe(true)
  })

  it('shows no "+N more" link when every line fits', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(PlanCards, { props: { plans: paid(1, { features: ['a', 'b'] }) } })

    expect(wrapper.find('[data-testid="plan-more-100"]').exists()).toBe(false)
  })

  it('clamps a very long description and lets a long name wrap', () => {
    stubAuroraGlobals()
    const long = 'word '.repeat(300)
    const card = mountWithKit(PlanCards, { props: { plans: paid(1, { name: long, description: long }) } }).find('[data-testid="plan-card-100"]')

    expect(card.find('.line-clamp-2').exists()).toBe(true)
    expect(card.find('.break-words').exists()).toBe(true)
  })

  it('badges the recommended plan and frames it with the brand border token', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(PlanCards, { props: { plans: paid(2).map((plan, index) => ({ ...plan, isFeatured: index === 1 })) } })

    expect(wrapper.find('[data-testid="plan-recommended-101"]').text()).toContain('aurora.plans.recommended')
    expect(wrapper.find('[data-testid="plan-card-101"]').classes()).toContain('border-ac1')
    expect(wrapper.find('[data-testid="plan-recommended-100"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plan-card-100"]').classes()).toContain('border-line')
  })

  it('shows no recommended badge when no plan is featured', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(PlanCards, { props: { plans: paid(2) } })

    expect(wrapper.find('[data-testid^="plan-recommended-"]').exists()).toBe(false)
  })
})
