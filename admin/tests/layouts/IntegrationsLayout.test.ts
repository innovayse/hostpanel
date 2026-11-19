/**
 * Mount tests for IntegrationsLayout: the category sidebar sits beside the routed page, and a tab strip lists the same
 * categories for a phone.
 */
import { describe, expect, it } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import IntegrationsLayout from '@/layouts/IntegrationsLayout.vue'
import { mountView } from '../helpers/mount'

describe('IntegrationsLayout', () => {
  it('renders the category sidebar inside the layout', async () => {
    const wrapper = await mountView(IntegrationsLayout, { path: '/integrations/email' })

    expect(wrapper.find('[data-testid="integrations-layout"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="integrations-nav-email"]').exists()).toBe(true)
  })

  it('lists the categories as a tab strip below md, marks the open one and opens the chosen one', async () => {
    const wrapper = await mountView(IntegrationsLayout, { path: '/integrations/email' })

    expect(wrapper.get('[data-testid="integrations-tabs"]').classes()).toContain('md:hidden')
    expect(wrapper.get('[data-testid="integrations-tab-email"]').attributes('aria-selected')).toBe('true')

    await wrapper.get('[data-testid="integrations-tab-payments"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="integrations-tab-payments"]').attributes('aria-selected')).toBe('true')
  })
})
