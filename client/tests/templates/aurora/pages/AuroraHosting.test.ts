/**
 * Unit tests for AuroraHosting: the page headline, the plan cards without their own header, the
 * comparison table that exists only when specifications do, and the billing event.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import AuroraHosting from '~/templates/aurora/pages/AuroraHosting.vue'
import { PricingCycle } from '~/types/pricingcycle'
import { useAuroraGlobals, PLANS } from '../auroraGlobals'

/** The stub server and the loader of the aurora globals; the hooks start the stub before each test and close it after. */
const { loadGlobals } = useAuroraGlobals()

describe('AuroraHosting', () => {
  it('renders the page headline as the only h1 and the plan cards under it', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(AuroraHosting, { props: { plans: PLANS } })

    expect(wrapper.findAll('h1')).toHaveLength(1)
    expect(wrapper.find('[data-testid="hosting-title"]').text()).toBe('aurora.hostingPage.title')
    expect(wrapper.find('[data-testid="plan-card-1"]').exists()).toBe(true)
  })

  it('does not repeat a section headline above the cards', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(AuroraHosting, { props: { plans: PLANS } })

    expect(wrapper.text()).not.toContain('aurora.plans.title')
  })

  it('shows the comparison table only when there are specification rows', async () => {
    await loadGlobals()

    expect(mountWithKit(AuroraHosting, { props: { plans: PLANS } }).find('[data-testid="comparison-table"]').exists()).toBe(false)
    expect(
      mountWithKit(AuroraHosting, { props: { plans: PLANS, comparisonRows: [{ label: 'Disk', values: ['1', '2', '3'] }, { label: 'Sites', values: ['1', '2', '3'] }] } })
        .find('[data-testid="comparison-table"]').exists(),
    ).toBe(true)
  })

  it('hides the comparison table for prod-shaped plans whose lines do not overlap', async () => {
    await loadGlobals()
    const disjoint = [{ label: 'Disk', values: ['1', '', ''] }, { label: 'Sites', values: ['', '2', ''] }, { label: 'Mail', values: ['', '', '3'] }]

    expect(mountWithKit(AuroraHosting, { props: { plans: PLANS, comparisonRows: disjoint } }).find('[data-testid="comparison-table"]').exists()).toBe(false)
  })

  it('ends with the call to action and passes the billing cycle up', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(AuroraHosting, { props: { plans: PLANS } })

    expect(wrapper.find('[data-testid="aurora-cta"]').exists()).toBe(true)

    await wrapper.find('[data-testid="billing-annual"]').trigger('click')
    expect(wrapper.emitted('update:cycle')).toEqual([[PricingCycle.Annual]])
  })
})
