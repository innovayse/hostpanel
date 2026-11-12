/**
 * Unit tests for ClientDashboardServicesCard: the service list, its states and the
 * control-panel shortcut, against the real client and service management stores, API composables and
 * transport, with a stub server playing the BFF.
 *
 * @module tests/components/client/dashboard/ServicesCard.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import ServicesCard from '~/components/client/dashboard/ServicesCard.vue'
import { useClientStore } from '~/stores/client'
import { Permission } from '~/types/permission'
import { makeClientService } from '~/tests/helpers/clientServiceFixture'
import { grantPermissions } from '~/tests/helpers/grantPermissions'
import { mountClient } from '~/tests/helpers/mountClient'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { ServiceStatus } from '~/types/servicestatus'
import { controlPanelUrl } from '~/utils/controlPanel'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The BFF path that answers the single sign-on address of the control panel of service 1. */
const SSO_PATH = '/api/portal/client/services/1/cpanel-sso'

/** The single sign-on address the BFF hands out. */
const SSO_URL = 'https://srv.test:2083/sso-session'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it acts. */
let table: StubRouteTable

/** A provisioned active service on a known host. */
const ACTIVE = makeClientService({ id: 1, productName: 'Starter', domain: 'a.test', username: 'ada', server: { name: 'srv', hostname: 'srv.test', ipAddress: null } })

/** A paid service that is not provisioned yet. */
const PENDING = makeClientService({ id: 2, productName: 'Pro', domain: null, username: null, status: ServiceStatus.Pending, server: null })

beforeEach(async () => {
  table = { [`GET ${SSO_PATH}`]: { body: { url: SSO_URL } } }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  vi.restoreAllMocks()
  await stub.close()
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

  it('signs the clicked service in through the BFF and opens the panel it answers with', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null)
    const wrapper = mountClient(ServicesCard)
    useClientStore().services = [ACTIVE]
    await nextTick()

    await wrapper.find('[data-testid="dashboard-service-cpanel-1"]').trigger('click')

    await waitUntil(() => expect(open).toHaveBeenCalledWith(SSO_URL, '_blank', 'noopener'))
    expect(requestsTo(stub.requests, SSO_PATH)).toHaveLength(1)
  })

  it('falls back to the plain sign-in page of the service host when single sign-on is refused', async () => {
    table[`GET ${SSO_PATH}`] = { status: 502, body: { detail: 'The panel is unreachable.' } }
    const open = vi.spyOn(window, 'open').mockReturnValue(null)
    const wrapper = mountClient(ServicesCard)
    useClientStore().services = [ACTIVE]
    await nextTick()

    await wrapper.find('[data-testid="dashboard-service-cpanel-1"]').trigger('click')

    await waitUntil(() => expect(open).toHaveBeenCalledWith(controlPanelUrl('srv.test'), '_blank', 'noopener'))
  })
})
