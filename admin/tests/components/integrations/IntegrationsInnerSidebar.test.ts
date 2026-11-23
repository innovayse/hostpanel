/**
 * Mount tests for IntegrationsInnerSidebar: one link per listed category, captioned from the schema's locale keys,
 * and the active one follows the route (the bare section counts as payments).
 */
import { describe, expect, it } from 'vitest'
import IntegrationsInnerSidebar from '@/components/integrations/IntegrationsInnerSidebar.vue'
import schema from '@/layouts/IntegrationsLayout.schema'
import { mountScreen } from '../../helpers/mount'

/**
 * Mounts the sidebar over the integrations layout's categories at a path.
 *
 * @param path - Where the router stands.
 * @returns The mounted wrapper.
 */
const mountAt = async (path: string) => (await mountScreen(IntegrationsInnerSidebar, { categories: schema.tabs }, path)).wrapper

/** Matches the category links only: the sidebar's own panel wrapper shares the testid prefix. */
const ENTRY_SELECTOR = '[data-testid^="integrations-nav-"]:not([data-testid="integrations-nav-panel"])'

describe('IntegrationsInnerSidebar', () => {
  it('renders one link per category', async () => {
    const wrapper = await mountAt('/integrations/email')

    for (const key of ['payments', 'provisioning', 'registrars', 'email', 'fulfilment', 'fraud', 'identity', 'hosted']) {
      expect(wrapper.find(`[data-testid="integrations-nav-${key}"]`).exists()).toBe(true)
    }
    expect(wrapper.get('[data-testid="integrations-nav-email"]').attributes('href')).toBe('/integrations/email')
  })

  it('lists the categories in the order of the schema', async () => {
    const wrapper = await mountAt('/integrations/email')

    expect(wrapper.findAll(ENTRY_SELECTOR).map(link => link.attributes('data-testid'))).toEqual(
      schema.tabs.map(tab => `integrations-nav-${tab.key}`),
    )
  })

  it('names each entry by the short menu caption and gives the full title as its tooltip', async () => {
    const wrapper = await mountAt('/integrations/email')
    const payments = wrapper.get('[data-testid="integrations-nav-payments"]')

    expect(payments.attributes('aria-label')).toBe('Payments')
    expect(payments.attributes('title')).toBe('Payment gateways')
    expect(wrapper.get('[data-testid="integrations-nav-hosted"]').attributes('aria-label')).toBe('Background jobs')
  })

  it('lists only the categories it is given', async () => {
    const wrapper = (await mountScreen(IntegrationsInnerSidebar, { categories: schema.tabs.slice(0, 2) }, '/integrations/email')).wrapper

    expect(wrapper.findAll(ENTRY_SELECTOR)).toHaveLength(2)
  })

  it('marks the category of the current route as active', async () => {
    const wrapper = await mountAt('/integrations/email')

    expect(wrapper.get('[data-testid="integrations-nav-email"]').attributes('aria-current')).toBe('page')
    expect(wrapper.get('[data-testid="integrations-nav-fraud"]').attributes('aria-current')).toBeUndefined()
  })

  it('treats the bare section as payments', async () => {
    const wrapper = await mountAt('/integrations')

    expect(wrapper.get('[data-testid="integrations-nav-payments"]').attributes('aria-current')).toBe('page')
  })
})
