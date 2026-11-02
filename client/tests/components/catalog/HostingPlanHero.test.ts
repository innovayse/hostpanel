/**
 * Unit tests for HostingPlanHero: the loading skeleton, the not-found notice, the plan with its
 * price and the order button.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import HostingPlanHero from '~/components/catalog/HostingPlanHero.vue'
import type { PortalProduct } from '~/types/portalproduct'

/** The plan the tests render. */
const plan: PortalProduct = { id: 2, groupId: 1, name: 'Starter', description: '', pricing: { monthly: 5, annual: 50 } }

/**
 * Mounts the hero.
 *
 * @param props Props that differ from the defaults.
 * @returns The wrapper.
 */
const mountHero = (props: Record<string, unknown> = {}) =>
  mountWithKit(HostingPlanHero, {
    props: { pending: false, plan, summary: 'Small sites', price: '$5.00', period: '/mo', inCart: false, ...props },
  })

describe('HostingPlanHero', () => {
  beforeEach(() => {
    stubPublicPageRuntime()
  })

  it('shows a skeleton while the catalogue is read', () => {
    const wrapper = mountHero({ pending: true, plan: null })

    expect(wrapper.find('[data-testid="hosting-plan-loading"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="hosting-plan-name"]').exists()).toBe(false)
  })

  it('shows a notice with the way back to all plans when no plan matches', () => {
    const wrapper = mountHero({ plan: null })

    expect(wrapper.find('[data-testid="hosting-plan-missing"]').text()).toContain('hosting.planNotFound')
    expect(wrapper.find('[data-testid="hosting-plan-all"]').attributes('href')).toBe('/en/hosting')
  })

  it('names the unit the price is per, whatever the cycle', () => {
    expect(mountHero().text()).toContain('/mo')
    expect(mountHero({ price: '$170.00', period: '/2 yr' }).text()).toContain('/2 yr')
    expect(mountHero({ price: '$170.00', period: '/2 yr' }).text()).not.toContain('/month')
  })

  it('shows the plan name, summary and price', () => {
    const wrapper = mountHero()

    expect(wrapper.find('[data-testid="hosting-plan-name"]').element.tagName).toBe('H1')
    expect(wrapper.find('[data-testid="hosting-plan-name"]').text()).toBe('Starter')
    expect(wrapper.find('[data-testid="hosting-plan-summary"]').text()).toBe('Small sites')
    expect(wrapper.find('[data-testid="hosting-plan-price"]').text()).toBe('$5.00')
  })

  it('leaves out the summary when the plan has none', () => {
    expect(mountHero({ summary: '' }).find('[data-testid="hosting-plan-summary"]').exists()).toBe(false)
  })

  it('labels the order button by whether the plan is in the cart, and reports a press', async () => {
    const wrapper = mountHero()
    expect(wrapper.find('[data-testid="hosting-plan-order"]').text()).toContain('hosting.orderNow')

    await wrapper.find('[data-testid="hosting-plan-order"]').trigger('click')

    expect(wrapper.emitted('order')).toHaveLength(1)
    expect(mountHero({ inCart: true }).find('[data-testid="hosting-plan-order"]').text()).toContain('hosting.inCart')
  })

  it('links back to the plans list', () => {
    expect(mountHero().find('[data-testid="hosting-plan-back"]').attributes('href')).toBe('/en/hosting')
  })
})
