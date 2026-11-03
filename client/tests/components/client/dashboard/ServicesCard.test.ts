/**
 * Unit tests for ClientDashboardServicesCard: the service list, its states and the
 * control-panel shortcut, against a real client store and a replaced management API.
 *
 * @module tests/components/client/dashboard/ServicesCard.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { nextTick } from 'vue'
import { Permission } from '~/composables/usePermissions'
import { makeClientService } from '~/tests/helpers/clientServiceFixture'
import { grantPermissions } from '~/tests/helpers/grantPermissions'
import { mountClient } from '~/tests/helpers/mountClient'
import { ServiceStatus } from '~/types/servicestatus'

/** The mocked open-control-panel action, re-created for every test. */
const openControlPanel = vi.fn()

vi.mock('~/stores/servicemanagement', () => ({
  /**
   * Stands in for the service-management store, whose transport is not under test here.
   *
   * @returns Only the action the card calls.
   */
  useServiceManagementStore: () => ({ openControlPanel })
}))

const { default: ServicesCard } = await import('~/components/client/dashboard/ServicesCard.vue')
const { useClientStore } = await import('~/stores/client')

/** A provisioned active service on a known host. */
const ACTIVE = makeClientService({ id: 1, productName: 'Starter', domain: 'a.test', username: 'ada', server: { name: 'srv', hostname: 'srv.test', ipAddress: null } })

/** A paid service that is not provisioned yet. */
const PENDING = makeClientService({ id: 2, productName: 'Pro', domain: null, username: null, status: ServiceStatus.Pending, server: null })

beforeEach(() => {
  openControlPanel.mockReset()
})

afterEach(() => {
  vi.restoreAllMocks()
})

describe('ClientDashboardServicesCard', () => {
  it('shows skeletons while loading', async () => {
    const wrapper = mountClient(ServicesCard)
    useClientStore().servicesLoading = true
    await nextTick()

    expect(wrapper.findAll('[data-testid="ui-skeleton"]')).toHaveLength(3)
  })

  it('invites the first order when there are no services', () => {
    const wrapper = mountClient(ServicesCard)

    expect(wrapper.find('[data-testid="dashboard-services-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="dashboard-place-order"]').attributes('href')).toBe('/hosting')
  })

  it('links a provisioned service to its page and an unprovisioned one to setup', async () => {
    const wrapper = mountClient(ServicesCard)
    useClientStore().services = [ACTIVE, PENDING]
    await nextTick()

    expect(wrapper.find('[data-testid="dashboard-service-1"]').attributes('href')).toBe('/client/services/1')
    expect(wrapper.find('[data-testid="dashboard-service-2"]').attributes('href')).toBe('/client/services/2/setup')
  })

  it('links a digital service to its page and shows its status, not the setup badge', async () => {
    const wrapper = mountClient(ServicesCard)
    useClientStore().services = [{ ...PENDING, id: 5, isDigital: true }]
    await nextTick()

    const row = wrapper.find('[data-testid="dashboard-service-5"]')
    expect(row.attributes('href')).toBe('/client/services/5')
    expect(row.text()).not.toContain('client.dashboard.needsSetup')
    expect(row.find('[data-testid="status-badge"]').exists()).toBe(true)
  })

  it('shows the setup badge instead of a status for an unprovisioned service', async () => {
    const wrapper = mountClient(ServicesCard)
    useClientStore().services = [PENDING]
    await nextTick()

    const row = wrapper.find('[data-testid="dashboard-service-2"]')
    expect(row.text()).toContain('client.dashboard.needsSetup')
    expect(row.find('[data-testid="status-badge"]').exists()).toBe(false)
  })

  it('lists at most four services', async () => {
    const wrapper = mountClient(ServicesCard)
    useClientStore().services = [1, 2, 3, 4, 5].map((id) => ({ ...ACTIVE, id })) as never
    await nextTick()

    expect(wrapper.findAll('[data-testid^="dashboard-service-"]').filter((row) => /dashboard-service-\d+$/.test(row.attributes('data-testid') ?? ''))).toHaveLength(4)
  })

  it('offers the control panel only for an active service on a known host', async () => {
    const wrapper = mountClient(ServicesCard)
    useClientStore().services = [ACTIVE, { ...PENDING, id: 3, username: 'x', status: ServiceStatus.Suspended, server: { name: 'srv', hostname: 'srv.test', ipAddress: null } }]
    await nextTick()

    expect(wrapper.find('[data-testid="dashboard-service-cpanel-1"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="dashboard-service-cpanel-3"]').exists()).toBe(false)
  })

  it('hides the control panel from a user without the single sign-on permission', async () => {
    const wrapper = mountClient(ServicesCard)
    await grantPermissions(Permission.ViewProductsServices)
    useClientStore().services = [ACTIVE]
    await nextTick()
    expect(wrapper.find('[data-testid="dashboard-service-cpanel-1"]').exists()).toBe(false)
  })

  it('shows the control panel button when single sign-on is granted', async () => {
    const wrapper = mountClient(ServicesCard)
    await grantPermissions(Permission.ViewProductsServices | Permission.AllowSingleSignOn)
    useClientStore().services = [ACTIVE]
    await nextTick()

    expect(wrapper.find('[data-testid="dashboard-service-cpanel-1"]').exists()).toBe(true)
  })

  it('asks the store to open the control panel of the clicked service with its host', async () => {
    openControlPanel.mockResolvedValue(undefined)
    const wrapper = mountClient(ServicesCard)
    useClientStore().services = [ACTIVE]
    await nextTick()

    await wrapper.find('[data-testid="dashboard-service-cpanel-1"]').trigger('click')
    await flushPromises()

    expect(openControlPanel).toHaveBeenCalledWith('1', 'srv.test')
  })
})
