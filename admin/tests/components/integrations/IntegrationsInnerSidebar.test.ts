/**
 * Mount tests for IntegrationsInnerSidebar: one link per category and the active one follows the
 * route (the bare section counts as payments).
 */
import { describe, expect, it } from 'vitest'
import IntegrationsInnerSidebar from '@/components/integrations/IntegrationsInnerSidebar.vue'
import { mountView } from '../../helpers/mount'

describe('IntegrationsInnerSidebar', () => {
  it('renders one link per category', async () => {
    const wrapper = await mountView(IntegrationsInnerSidebar, { path: '/integrations/email' })

    for (const key of ['payments', 'registrars', 'provisioning', 'email', 'fraud', 'fulfilment', 'identity', 'hosted']) {
      expect(wrapper.find(`[data-testid="integrations-nav-${key}"]`).exists()).toBe(true)
    }
    expect(wrapper.get('[data-testid="integrations-nav-email"]').attributes('href')).toBe('/integrations/email')
  })

  it('marks the category of the current route as active', async () => {
    const wrapper = await mountView(IntegrationsInnerSidebar, { path: '/integrations/email' })

    expect(wrapper.get('[data-testid="integrations-nav-email"]').attributes('aria-current')).toBe('page')
    expect(wrapper.get('[data-testid="integrations-nav-fraud"]').attributes('aria-current')).toBeUndefined()
  })

  it('treats the bare section as payments', async () => {
    const wrapper = await mountView(IntegrationsInnerSidebar, { path: '/integrations' })

    expect(wrapper.get('[data-testid="integrations-nav-payments"]').attributes('aria-current')).toBe('page')
  })
})
