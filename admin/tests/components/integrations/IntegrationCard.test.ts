/**
 * Mount tests for IntegrationCard: name, status badge, plugin, plugin type and not-configured badges and the
 * link to the configuration page.
 */
import { describe, expect, it } from 'vitest'
import IntegrationCard from '@/components/integrations/IntegrationCard.vue'
import { PluginType } from '@/types/plugin'
import { IntegrationConnectionState } from '@/types/integration'
import { mountWithApp } from '../../helpers/mount'
import { integrationDto } from '../../helpers/integrationFixtures'

describe('IntegrationCard', () => {
  it('links to the configuration page and shows name and active status', () => {
    const wrapper = mountWithApp(IntegrationCard, { props: { integration: integrationDto() } })

    expect(wrapper.get('[data-testid="integration-card-bank-transfer"]').attributes('href')).toBe('/integrations/bank-transfer/configure')
    expect(wrapper.text()).toContain('Bank Transfer')
    expect(wrapper.get('[data-testid="integration-status-bank-transfer"]').text()).toBe('Active')
  })

  it('shows an inactive integration as inactive', () => {
    const wrapper = mountWithApp(IntegrationCard, { props: { integration: integrationDto({ isEnabled: false }) } })

    expect(wrapper.get('[data-testid="integration-status-bank-transfer"]').text()).toBe('Inactive')
  })

  it('marks an integration whose required fields are not stored as not configured', () => {
    const configured = mountWithApp(IntegrationCard, { props: { integration: integrationDto() } })
    const unconfigured = mountWithApp(IntegrationCard, { props: { integration: integrationDto({ isConfigured: false }) } })

    expect(configured.find('[data-testid="integration-unconfigured-bank-transfer"]').exists()).toBe(false)
    expect(unconfigured.get('[data-testid="integration-unconfigured-bank-transfer"]').text()).toBe('Not configured')
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
    [PluginType.Provisioning, 'Provisioning'],
    [PluginType.Registrar, 'Registrar'],
  ])('labels a %s plugin with its type', (pluginType, label) => {
    const wrapper = mountWithApp(IntegrationCard, { props: { integration: integrationDto({ isPlugin: true, pluginType }) } })

    expect(wrapper.get('[data-testid="integration-type-bank-transfer"]').text()).toBe(label)
  })

  it('shows no type badge on a built-in integration', () => {
    const wrapper = mountWithApp(IntegrationCard, { props: { integration: integrationDto() } })

    expect(wrapper.find('[data-testid="integration-type-bank-transfer"]').exists()).toBe(false)
  })

  it('shows a plugin integration with its icon and falls back to the initial', () => {
    const withIcon = mountWithApp(IntegrationCard, { props: { integration: integrationDto({ isPlugin: true, iconUrl: '/icon.png' }) } })
    const without = mountWithApp(IntegrationCard, { props: { integration: integrationDto({ isPlugin: true }) } })
    const builtIn = mountWithApp(IntegrationCard, { props: { integration: integrationDto() } })

    expect(withIcon.get('[data-testid="plugin-avatar-image"]').attributes('src')).toBe('/icon.png')
    expect(without.find('[data-testid="plugin-avatar-fallback"]').exists()).toBe(true)
    expect(builtIn.find('[data-testid="integration-plugin-avatar"]').exists()).toBe(false)
  })

  it('shows the connection state a plugin reported, with its message', () => {
    const wrapper = mountWithApp(IntegrationCard, {
      props: {
        integration: integrationDto({
          isPlugin: true,
          pluginType: PluginType.Fulfilment,
          connection: {
            state: IntegrationConnectionState.Connected,
            message: 'Both services answer.',
            checkedAt: '2026-11-04T10:00:00Z',
          },
        }),
      },
    })

    const connection = wrapper.get('[data-testid="integration-connection-bank-transfer"]')
    expect(connection.text()).toContain('Connected')
    expect(connection.text()).toContain('Both services answer.')
  })

  it('shows no connection state when the API sent none', () => {
    const wrapper = mountWithApp(IntegrationCard, { props: { integration: integrationDto() } })

    expect(wrapper.find('[data-testid="integration-connection-bank-transfer"]').exists()).toBe(false)
  })

  it('draws the same card with bars and no link while there is no integration', () => {
    const wrapper = mountWithApp(IntegrationCard)
    const card = wrapper.get('[data-testid="integration-card-loading"]')

    expect(card.attributes('href')).toBeUndefined()
    expect(wrapper.find('[aria-busy="true"]').exists()).toBe(true)
    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(5)
    expect(wrapper.text()).toContain('Configure')
  })
})
