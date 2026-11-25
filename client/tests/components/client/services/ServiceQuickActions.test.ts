/**
 * Unit tests for ServiceQuickActions: which shortcuts show and the event each raises.
 */
import { describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import { grantPermissions } from '~/tests/helpers/grantPermissions'
import { Permission } from '~/types/permission'
import { makeClientService as service } from '~/tests/helpers/clientServiceFixture'
import { ServiceStatus } from '~/types/servicestatus'
import ServiceQuickActions from '~/components/client/services/ServiceQuickActions.vue'

describe('ServiceQuickActions', () => {
  it('offers the control-panel shortcut only on a known server', () => {
    const known = mountClient(ServiceQuickActions, { props: { service: service(), ssoLoading: false } })
    const unknown = mountClient(ServiceQuickActions, { props: { service: service({ server: null }), ssoLoading: false } })

    expect(known.find('[data-testid="service-quick-panel"]').exists()).toBe(true)
    expect(unknown.find('[data-testid="service-quick-panel"]').exists()).toBe(false)
  })

  it('hides the control-panel shortcut on a service that is not active', () => {
    const wrapper = mountClient(ServiceQuickActions, { props: { service: service({ status: ServiceStatus.Pending }), ssoLoading: false } })

    expect(wrapper.find('[data-testid="service-quick-panel"]').exists()).toBe(false)
  })

  it('raises the event of the shortcut that was clicked', async () => {
    const wrapper = mountClient(ServiceQuickActions, { props: { service: service(), ssoLoading: false } })

    await wrapper.find('[data-testid="service-quick-panel"]').trigger('click')
    await wrapper.find('[data-testid="service-quick-invoices"]').trigger('click')
    await wrapper.find('[data-testid="service-quick-cancel"]').trigger('click')

    expect(wrapper.emitted('panel')).toHaveLength(1)
    expect(wrapper.emitted('invoices')).toHaveLength(1)
    expect(wrapper.emitted('cancel')).toHaveLength(1)
  })
})

describe('ServiceQuickActions panel icon', () => {
  it('shows the icon of the plugin that serves the server, named after the plugin', () => {
    const served = { name: 'srv', hostname: 'srv.test', ipAddress: null, panelPlugin: { id: 'acme-panel', name: 'Acme Panel', iconVersion: '1.2.0' } }
    const wrapper = mountClient(ServiceQuickActions, { props: { service: service({ server: served }), ssoLoading: false } })

    const icon = wrapper.get('[data-testid="service-quick-panel"] [data-testid="service-panel-plugin-icon"]')
    expect(icon.attributes('src')).toBe('/api/portal/client/plugins/acme-panel/icon')
    expect(icon.attributes('alt')).toBe('Acme Panel')
  })
})

describe('ServiceQuickActions permissions', () => {
  it('shows only the shortcuts the person may use', async () => {
    const wrapper = mountClient(ServiceQuickActions, { props: { service: service(), ssoLoading: false } })
    await grantPermissions(Permission.ViewProductsServices | Permission.ViewPayInvoices)

    expect(wrapper.find('[data-testid="service-quick-invoices"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="service-quick-panel"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="service-quick-cancel"]').exists()).toBe(false)
  })

  it('shows the control panel and cancel shortcuts with their permissions', async () => {
    const wrapper = mountClient(ServiceQuickActions, { props: { service: service(), ssoLoading: false } })
    await grantPermissions(Permission.AllowSingleSignOn | Permission.PlaceNewOrders)

    expect(wrapper.find('[data-testid="service-quick-panel"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="service-quick-cancel"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="service-quick-invoices"]').exists()).toBe(false)
  })
})
