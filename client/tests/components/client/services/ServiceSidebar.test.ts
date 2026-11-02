/**
 * Unit tests for ServiceSidebar: the panel entries, the conditional entries and the events.
 */
import { describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import { grantPermissions } from '~/tests/helpers/grantPermissions'
import { Permission } from '~/composables/usePermissions'
import ServiceSidebar from '~/components/client/services/ServiceSidebar.vue'
import { makeClientService as service } from '~/tests/helpers/clientServiceFixture'
import { ServiceStatus } from '~/types/servicestatus'
import { ServiceTab } from '~/types/servicetab'

/**
 * Mounts the menu with defaults.
 *
 * @param props - Props to override.
 * @returns The wrapper.
 */
const mountSidebar = (props: Record<string, unknown> = {}) =>
  mountClient(ServiceSidebar, { props: { service: service(), serviceId: '4', activeTab: ServiceTab.Overview, ssoLoading: false, ssoError: '', ...props } })

describe('ServiceSidebar', () => {
  it('marks the active panel', () => {
    const wrapper = mountSidebar({ activeTab: ServiceTab.Password })

    expect(wrapper.find('[data-testid="service-tab-password"]').classes()).toContain('font-medium')
    expect(wrapper.find('[data-testid="service-tab-overview"]').classes()).not.toContain('bg-primary-50')
  })

  it('emits the panel that was clicked', async () => {
    const wrapper = mountSidebar()

    await wrapper.find('[data-testid="service-tab-cancel"]').trigger('click')

    expect(wrapper.emitted('update:activeTab')).toEqual([[ServiceTab.Cancel]])
  })

  it('links to a new ticket and, while pending, to the setup', () => {
    const active = mountSidebar()
    const pending = mountSidebar({ service: service({ status: ServiceStatus.Pending }) })

    expect(active.find('[data-testid="service-open-ticket"]').attributes('href')).toBe('/client/tickets/new')
    expect(active.find('[data-testid="service-nav-setup"]').exists()).toBe(false)
    expect(pending.find('[data-testid="service-nav-setup"]').attributes('href')).toBe('/client/services/4/setup')
  })

  it('does not offer the setup of a pending digital service', () => {
    const wrapper = mountSidebar({ service: service({ status: ServiceStatus.Pending, isDigital: true }) })

    expect(wrapper.find('[data-testid="service-nav-setup"]').exists()).toBe(false)
  })

  it('offers the quick links only with a known server', () => {
    const known = mountSidebar()
    const unknown = mountSidebar({ service: service({ server: null }) })

    expect(known.find('[data-testid="service-link-webmail"]').attributes('href')).toBe('https://srv1.example.net:2096')
    expect(unknown.find('[data-testid="service-link-cpanel"]').exists()).toBe(false)
  })

  it('does not offer the control panel on a terminated service', async () => {
    await grantPermissions(Permission.AllowSingleSignOn)
    const wrapper = mountSidebar({ service: service({ status: ServiceStatus.Terminated }) })

    expect(wrapper.find('[data-testid="service-link-cpanel"]').exists()).toBe(false)
  })

  it('does not offer the control panel on a service without a server even with single sign-on granted', async () => {
    await grantPermissions(Permission.AllowSingleSignOn)
    const wrapper = mountSidebar({ service: service({ server: null }) })

    expect(wrapper.find('[data-testid="service-link-cpanel"]').exists()).toBe(false)
  })

  it('asks for the control panel and shows a sign-in failure', async () => {
    const wrapper = mountSidebar({ ssoError: 'No answer' })

    await wrapper.find('[data-testid="service-link-cpanel"]').trigger('click')

    expect(wrapper.emitted('cpanel')).toHaveLength(1)
    expect(wrapper.find('[data-testid="service-sso-error"]').text()).toBe('No answer')
  })
})

describe('ServiceSidebar permissions', () => {
  it('offers only the overview and the webmail link to a person with no service permission', async () => {
    const wrapper = mountSidebar()
    await grantPermissions(Permission.ViewProductsServices)

    expect(wrapper.find('[data-testid="service-tab-overview"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="service-tab-password"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="service-tab-cancel"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="service-open-ticket"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="service-link-cpanel"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="service-link-webmail"]').exists()).toBe(true)
  })

  it('offers the password panel with ViewModifyPasswords and the control panel with AllowSingleSignOn', async () => {
    const wrapper = mountSidebar()
    await grantPermissions(Permission.ViewProductsServices | Permission.ViewModifyPasswords | Permission.AllowSingleSignOn)

    expect(wrapper.find('[data-testid="service-tab-password"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="service-link-cpanel"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="service-tab-cancel"]').exists()).toBe(false)
  })

  it('offers cancelling and the setup with PlaceNewOrders, and a new ticket with ViewOpenSupportTickets', async () => {
    const wrapper = mountSidebar({ service: service({ status: ServiceStatus.Pending }) })
    await grantPermissions(Permission.PlaceNewOrders | Permission.ViewOpenSupportTickets)

    expect(wrapper.find('[data-testid="service-tab-cancel"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="service-nav-setup"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="service-open-ticket"]').exists()).toBe(true)
  })
})
