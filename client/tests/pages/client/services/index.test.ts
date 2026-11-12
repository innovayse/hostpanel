/**
 * Tests for `pages/client/services/index.vue` against the real client store, API composable and
 * transport, with a stub BFF answering on a real socket (nothing of ours is mocked): the cards, the
 * setup banner and the list states.
 *
 * @module tests/pages/client/services/index.test
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import ServicesPage from '~/pages/client/services/index.vue'
import { makeClientService } from '~/tests/helpers/clientServiceFixture'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { Permission } from '~/types/permission'
import { ServiceStatus } from '~/types/servicestatus'
import type { ClientService } from '~/types/clientservice'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The BFF paths the page reads. */
const PATHS = {
  me: '/api/portal/client/me',
  services: '/api/portal/client/services',
} as const

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Builds a service with the fields the card reads.
 *
 * @param id - Service id.
 * @param status - Lifecycle status; Pending for a service that still needs setup.
 * @returns A service.
 */
const service = (id: number, status: ServiceStatus = ServiceStatus.Active): ClientService =>
  makeClientService({ id, productName: `Plan ${id}`, domain: `site${id}.com`, status })

beforeEach(async () => {
  stubClientRuntime()
  table = {
    [`GET ${PATHS.me}`]: { body: makeClientUser({ permissions: Permission.All }) },
  }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('service list page', () => {
  it('shows one card per service', async () => {
    table[`GET ${PATHS.services}`] = { body: [service(1), service(2)] }

    const wrapper = await mountPage(ServicesPage)
    await waitUntil(() => expect(wrapper.findAll('[data-testid^="service-card-"]')).toHaveLength(2))

    expect(wrapper.find('[data-testid="services-setup-alert"]').exists()).toBe(false)
  })

  it('announces how many services still need setup', async () => {
    table[`GET ${PATHS.services}`] = { body: [service(1), service(2, ServiceStatus.Pending), service(3, ServiceStatus.Pending)] }

    const wrapper = await mountPage(ServicesPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="services-setup-alert"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="services-setup-alert"]').text()).toContain('client.dashboard.setupAlertSubtitle')
    expect(wrapper.findAll('[data-testid="service-needs-setup"]')).toHaveLength(2)
  })

  it('does not count a digital service among those that need setup', async () => {
    table[`GET ${PATHS.services}`] = { body: [service(1), makeClientService({ id: 2, status: ServiceStatus.Pending, isDigital: true })] }

    const wrapper = await mountPage(ServicesPage)
    await waitUntil(() => expect(wrapper.findAll('[data-testid^="service-card-"]')).toHaveLength(2))

    expect(wrapper.find('[data-testid="services-setup-alert"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="service-needs-setup"]')).toHaveLength(0)
  })

  it('shows the empty state with a link to the hosting plans', async () => {
    table[`GET ${PATHS.services}`] = { body: [] }

    const wrapper = await mountPage(ServicesPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="services-browse-hosting"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="services-browse-hosting"]').attributes('href')).toBe('/hosting')
  })

  it('shows the failure, not the empty state, when the read failed', async () => {
    table[`GET ${PATHS.services}`] = { status: 503, body: { detail: 'Hosting is down.' } }

    const wrapper = await mountPage(ServicesPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="services-error"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="services-error"]').text()).toBe('Hosting is down.')
    expect(wrapper.find('[data-testid="ui-empty-state"]').exists()).toBe(false)
  })
})
