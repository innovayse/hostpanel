/**
 * Unit tests for NovaHosting: its own h1, the headerless pricing section and the shared state.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import NovaHosting from '~/templates/nova/pages/NovaHosting.vue'
import { PLANS } from '~/tests/templates/aurora/auroraGlobals'
import { PricingCycle } from '~/types/pricingcycle'
import { useTemplateGlobals } from '~/tests/helpers/templateGlobals'

/** The stub server and the loader of the template globals; the hooks start the stub before each test and close it after. */
const { loadGlobals } = useTemplateGlobals()

describe('NovaHosting', () => {
  it('is the skip-link target', async () => {
    await loadGlobals()

    expect(mountWithKit(NovaHosting).attributes('id')).toBe('nova-main')
  })

  it('states what the page is in its own h1 and keeps the pricing section headerless', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(NovaHosting)

    expect(wrapper.findAll('h1')).toHaveLength(1)
    expect(wrapper.find('h1').text()).toBe('nova.plans.title')
    expect(wrapper.find('#plans h2').exists()).toBe(false)
  })

  it('passes the billing choice up unchanged', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(NovaHosting, { props: { plans: PLANS, cycle: PricingCycle.Annual } })

    await wrapper.find('[data-testid="nova-billing-monthly"]').trigger('click')

    expect(wrapper.emitted('update:cycle')).toEqual([[PricingCycle.Monthly]])
  })

  it('renders the comparison table when rows arrive', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(NovaHosting, {
      props: { plans: [], comparisonRows: [{ label: 'Storage', values: ['1 GB', '2 GB'] }, { label: 'Sites', values: ['1', '5'] }] },
    })

    expect(wrapper.find('[data-testid="nova-comparison-table"]').exists()).toBe(true)
  })
})
