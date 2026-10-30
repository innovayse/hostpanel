/**
 * Mount tests for IntegrationCard: name, status badge, plugin, plugin type and not-configured badges and the
 * link to the configuration page.
 */
import { describe, expect, it } from 'vitest'
import IntegrationCard from '@/components/integrations/IntegrationCard.vue'
import { PluginType } from '@/types/plugin'
import { mountWithApp } from '../../helpers/mount'
import { integrationDto } from '../../helpers/integrationFixtures'

describe('IntegrationCard', () => {
  it('links to the configuration page and shows name and active status', () => {
    const wrapper = mountWithApp(IntegrationCard, { props: { integration: integrationDto() } })

    expect(wrapper.get('[data-testid="integration-card-stripe"]').attributes('href')).toBe('/integrations/stripe/configure')
    expect(wrapper.text()).toContain('Stripe')
    expect(wrapper.get('[data-testid="integration-status-stripe"]').text()).toBe('Active')
  })

  it('shows an inactive integration as inactive', () => {
    const wrapper = mountWithApp(IntegrationCard, { props: { integration: integrationDto({ isEnabled: false }) } })

    expect(wrapper.get('[data-testid="integration-status-stripe"]').text()).toBe('Inactive')
  })

  it('marks an integration whose required fields are not stored as not configured', () => {
    const configured = mountWithApp(IntegrationCard, { props: { integration: integrationDto() } })
    const unconfigured = mountWithApp(IntegrationCard, { props: { integration: integrationDto({ isConfigured: false }) } })

    expect(configured.find('[data-testid="integration-unconfigured-stripe"]').exists()).toBe(false)
    expect(unconfigured.get('[data-testid="integration-unconfigured-stripe"]').text()).toBe('Not configured')
  })

  it('marks a plugin-provided integration', () => {
    const wrapper = mountWithApp(IntegrationCard, { props: { integration: integrationDto({ isPlugin: true }) } })

    expect(wrapper.text()).toContain('Plugin')
  })

  it.each([
    [PluginType.Fulfilment, 'Fulfilment'],
    [PluginType.Identity, 'Identity'],
    [PluginType.Hosted, 'Background worker'],
    [PluginType.Payment, 'Payment'],
  ])('labels a %s plugin with its type', (pluginType, label) => {
    const wrapper = mountWithApp(IntegrationCard, { props: { integration: integrationDto({ isPlugin: true, pluginType }) } })

    expect(wrapper.get('[data-testid="integration-type-stripe"]').text()).toBe(label)
  })

  it('shows no type badge on a built-in integration', () => {
    const wrapper = mountWithApp(IntegrationCard, { props: { integration: integrationDto() } })

    expect(wrapper.find('[data-testid="integration-type-stripe"]').exists()).toBe(false)
  })
})
