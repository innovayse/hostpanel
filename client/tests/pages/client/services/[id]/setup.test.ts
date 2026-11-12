/**
 * Tests for `pages/client/services/[id]/setup.vue` against the real service management store, API
 * composable and transport, with a stub BFF answering on a real socket (nothing of ours is mocked):
 * the wizard, the already-set-up state and the unavailable state.
 *
 * @module tests/pages/client/services/[id]/setup.test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { unref } from 'vue'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import { makeClientService } from '~/tests/helpers/clientServiceFixture'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import SetupPage from '~/pages/client/services/[id]/setup.vue'
import type { ClientService } from '~/types/clientservice'
import { ServiceStatus } from '~/types/servicestatus'

/** The BFF path the page reads for service 4. */
const SERVICE_PATH = '/api/portal/client/services/4'

/** The selector of the setup wizard. */
const WIZARD = '[data-testid="setup-wizard"]'

/** Stands in for Nuxt's `navigateTo`, a framework function, not ours. */
const navigate = vi.fn()

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Builds a service waiting for setup: no control-panel account and no domain yet.
 *
 * @param overrides - Fields to set.
 * @returns The service.
 */
const pendingService = (overrides: Partial<ClientService> = {}): ClientService =>
  makeClientService({ status: ServiceStatus.Pending, username: '', domain: '', ...overrides })

beforeEach(async () => {
  navigate.mockReset()
  vi.stubGlobal('navigateTo', navigate)
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('hosting setup page', () => {
  it('shows the wizard for a service waiting for setup and sets the page title', async () => {
    table[`GET ${SERVICE_PATH}`] = { body: pendingService() }
    const { useHead } = stubClientRuntime({ params: { id: '4' } })

    const wrapper = await mountPage(SetupPage)
    await vi.waitFor(() => expect(wrapper.find(WIZARD).exists()).toBe(true))

    expect(requestsTo(stub.requests, SERVICE_PATH).map(request => request.method)).toEqual(['GET'])
    // The page hands `useHead` a computed title, which Nuxt unwraps; unwrap it the same way.
    expect(unref(useHead.mock.calls[0]?.[0].title)).toContain('client.setup.pageTitle')
  })

  it('says a configured service is already set up and links to its page', async () => {
    table[`GET ${SERVICE_PATH}`] = { body: makeClientService({ status: ServiceStatus.Active, username: 'webmstr', domain: 'example.com' }) }
    stubClientRuntime({ params: { id: '4' } })

    const wrapper = await mountPage(SetupPage)
    await vi.waitFor(() => expect(wrapper.find('[data-testid="setup-go-manage"]').exists()).toBe(true))

    expect(wrapper.find(WIZARD).exists()).toBe(false)
    expect(wrapper.find('[data-testid="setup-go-manage"]').attributes('href')).toBe('/client/services/4')
  })

  it('says setup is not available when the service cannot be read', async () => {
    table[`GET ${SERVICE_PATH}`] = { status: 404, body: { detail: 'No such service.' } }
    stubClientRuntime({ params: { id: '4' } })

    const wrapper = await mountPage(SetupPage)
    await vi.waitFor(() => expect(wrapper.find('[data-testid="setup-back-to-services"]').exists()).toBe(true))

    expect(wrapper.find(WIZARD).exists()).toBe(false)
    expect(wrapper.find('[data-testid="setup-back-to-services"]').attributes('href')).toBe('/client/services')
  })

  it('sends a digital service to its own page instead of offering a wizard', async () => {
    table[`GET ${SERVICE_PATH}`] = { body: pendingService({ isDigital: true, username: null }) }
    stubClientRuntime({ params: { id: '4' } })

    await mountPage(SetupPage)

    await vi.waitFor(() => expect(navigate).toHaveBeenCalledWith('/client/services/4', { replace: true }))
  })

  it('keeps the loading panel and never the wizard for a digital service while it is redirected', async () => {
    table[`GET ${SERVICE_PATH}`] = { body: pendingService({ isDigital: true, username: null }) }
    stubClientRuntime({ params: { id: '4' } })

    const wrapper = await mountPage(SetupPage)
    await vi.waitFor(() => expect(wrapper.text()).toContain('client.setup.loading'))

    expect(wrapper.find(WIZARD).exists()).toBe(false)
  })
})
