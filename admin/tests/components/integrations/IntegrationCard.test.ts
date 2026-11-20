/**
 * Mount tests for IntegrationCard: name, status badge, the one plugin marker, the not-configured badge, the body text
 * (a built-in's catalogue text, a plugin's own description), the connection badge with the plugin's sentence as its
 * tooltip, and the link to the configuration page.
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

  it('marks a plugin once and draws no badge for its category or type', () => {
    const wrapper = mountWithApp(IntegrationCard, {
      props: { integration: integrationDto({ isPlugin: true, pluginType: PluginType.Fulfilment }) },
    })

    expect(wrapper.findAll('[data-testid="integration-plugin-bank-transfer"]')).toHaveLength(1)
    expect(wrapper.find('[data-testid="integration-type-bank-transfer"]').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('Fulfilment')
  })

  it('shows no plugin marker on a built-in integration', () => {
    const wrapper = mountWithApp(IntegrationCard, { props: { integration: integrationDto() } })

    expect(wrapper.find('[data-testid="integration-plugin-bank-transfer"]').exists()).toBe(false)
  })

  it('keeps the name on one line and gives the full name as its tooltip', () => {
    const wrapper = mountWithApp(IntegrationCard, { props: { integration: integrationDto({ name: 'A very long integration name' }) } })
    const name = wrapper.get('[data-testid="integration-card-name"]')

    expect(name.classes()).toContain('truncate')
    expect(name.attributes('title')).toBe('A very long integration name')
  })

  it('shows the catalogue text of a built-in integration', () => {
    const wrapper = mountWithApp(IntegrationCard, { props: { integration: integrationDto() } })

    expect(wrapper.text()).toContain('Allow clients to pay via direct bank transfer with your banking details.')
  })

  it("shows the plugin's own description as the body", () => {
    const wrapper = mountWithApp(IntegrationCard, {
      props: { integration: integrationDto({ slug: 'deliver', isPlugin: true, description: 'Delivers licence keys after payment.' }) },
    })

    expect(wrapper.text()).toContain('Delivers licence keys after payment.')
  })

  it('shows a plugin integration with its icon and falls back to the initial', () => {
    const withIcon = mountWithApp(IntegrationCard, { props: { integration: integrationDto({ isPlugin: true, iconUrl: '/icon.png' }) } })
    const without = mountWithApp(IntegrationCard, { props: { integration: integrationDto({ isPlugin: true }) } })
    const builtIn = mountWithApp(IntegrationCard, { props: { integration: integrationDto() } })

    expect(withIcon.get('[data-testid="plugin-avatar-image"]').attributes('src')).toBe('/icon.png')
    expect(without.find('[data-testid="plugin-avatar-fallback"]').exists()).toBe(true)
    expect(builtIn.find('[data-testid="integration-plugin-avatar"]').exists()).toBe(false)
  })

  it('shows the connection state a plugin reported as a short badge, with its message only as the tooltip', () => {
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
    expect(connection.text()).toBe('Connected')
    expect(connection.attributes('title')).toBe('Both services answer.')
    expect(wrapper.text()).not.toContain('Both services answer.')
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
