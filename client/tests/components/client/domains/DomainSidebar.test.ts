/**
 * Unit tests for DomainSidebar: the panels, the lock on a pending domain and the renew action.
 */
import { describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import { grantPermissions } from '~/tests/helpers/grantPermissions'
import { Permission } from '~/composables/usePermissions'
import DomainSidebar from '~/components/client/domains/DomainSidebar.vue'
import { DomainTab } from '~/types/domaintab'

describe('DomainSidebar', () => {
  it('marks the active panel', () => {
    const wrapper = mountClient(DomainSidebar, { props: { activeTab: DomainTab.Lock, isPending: false } })

    expect(wrapper.find('[data-testid="domain-tab-lock"]').classes()).toContain('font-medium')
    expect(wrapper.find('[data-testid="domain-tab-overview"]').classes()).not.toContain('font-medium')
  })

  it('emits the panel that was clicked', async () => {
    const wrapper = mountClient(DomainSidebar, { props: { activeTab: DomainTab.Overview, isPending: false } })

    await wrapper.find('[data-testid="domain-tab-nameservers"]').trigger('click')

    expect(wrapper.emitted('update:activeTab')).toEqual([[DomainTab.Nameservers]])
  })

  it('locks every panel but the overview, and the renew action, while the domain is pending', async () => {
    const wrapper = mountClient(DomainSidebar, { props: { activeTab: DomainTab.Overview, isPending: true } })

    expect(wrapper.find('[data-testid="domain-tab-contact"]').attributes('disabled')).toBeDefined()
    expect(wrapper.find('[data-testid="domain-tab-overview"]').attributes('disabled')).toBeUndefined()
    expect(wrapper.find('[data-testid="domain-action-renew"]').attributes('disabled')).toBeDefined()

    await wrapper.find('[data-testid="domain-tab-contact"]').trigger('click')
    await wrapper.find('[data-testid="domain-action-renew"]').trigger('click')
    expect(wrapper.emitted('update:activeTab')).toBeUndefined()
  })

  it('opens the renew panel from the actions', async () => {
    const wrapper = mountClient(DomainSidebar, { props: { activeTab: DomainTab.Overview, isPending: false } })

    await wrapper.find('[data-testid="domain-action-renew"]').trigger('click')

    expect(wrapper.emitted('update:activeTab')).toEqual([[DomainTab.Renew]])
  })

  it('links to registering another domain', () => {
    const wrapper = mountClient(DomainSidebar, { props: { activeTab: DomainTab.Overview, isPending: false } })

    expect(wrapper.find('[data-testid="domain-action-register"]').attributes('href')).toBe('/domains')
  })
})

describe('DomainSidebar permissions', () => {
  /** Mounts the menu for a domain that is not pending. */
  const mountMenu = () => mountClient(DomainSidebar, { props: { activeTab: DomainTab.Overview, isPending: false } })

  it('offers only the overview to a person who may not change domain settings or place orders', async () => {
    const wrapper = mountMenu()
    await grantPermissions(Permission.ViewDomains)

    expect(wrapper.find('[data-testid="domain-tab-overview"]').exists()).toBe(true)
    for (const tab of [DomainTab.AutoRenew, DomainTab.Nameservers, DomainTab.Lock, DomainTab.Contact]) {
      expect(wrapper.find(`[data-testid="domain-tab-${tab}"]`).exists()).toBe(false)
    }
    expect(wrapper.find('[data-testid="domain-action-renew"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="domain-action-register"]').exists()).toBe(false)
  })

  it('offers the settings panels with ManageDomainSettings', async () => {
    const wrapper = mountMenu()
    await grantPermissions(Permission.ViewDomains | Permission.ManageDomainSettings)

    for (const tab of [DomainTab.AutoRenew, DomainTab.Nameservers, DomainTab.Lock, DomainTab.Contact]) {
      expect(wrapper.find(`[data-testid="domain-tab-${tab}"]`).exists()).toBe(true)
    }
    expect(wrapper.find('[data-testid="domain-action-renew"]').exists()).toBe(false)
  })

  it('offers renewing and registering with PlaceNewOrders', async () => {
    const wrapper = mountMenu()
    await grantPermissions(Permission.ViewDomains | Permission.PlaceNewOrders)

    expect(wrapper.find('[data-testid="domain-action-renew"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="domain-action-register"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="domain-tab-nameservers"]').exists()).toBe(false)
  })
})
