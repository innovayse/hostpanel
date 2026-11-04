/**
 * Tests for `pages/client/services/[id]/index.vue` against the real stores, API composables and
 * transport, with a stub BFF answering on a real socket (nothing of ours is mocked): the panels, the
 * notices, the redirect of a service that has no account yet, a failed read (not found, or the API
 * sentence with a retry), the permissions, and the tabs plugins add for this service, each an
 * isolated widget that never takes the page with it.
 *
 * @module tests/pages/client/services/[id]/index.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import { PLUGIN_COMPONENTS } from '~/tests/helpers/mountPlugin'
import ServicePage from '~/pages/client/services/[id]/index.vue'
import { Permission } from '~/composables/usePermissions'
import { PortalErrorCode } from '~/constants/portalErrorCode'
import { makeClientService as service } from '~/tests/helpers/clientServiceFixture'
import {
  SERVICE_NOT_FOUND, slotEntry, slotIndex, slotPath, slotWidgetPath, STATUS_ROUTE_PREFIX, throwingView, workingView, readingView,
} from '~/tests/helpers/pluginSlotFixture'
import { pathOf, queryOf, requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { PluginSlotId } from '~/types/pluginslotid'
import { ServiceStatus } from '~/types/servicestatus'
import { SuspensionSource } from '~/types/suspensionsource'
import { settle } from '~/tests/helpers/settle'

/** The BFF paths the page reads for service 4. */
const PATHS = {
  me: '/api/portal/client/me',
  service: '/api/portal/client/services/4',
  hosting: '/api/portal/client/services/4/hosting-info',
  cancellation: '/api/portal/client/services/4/cancellation-status',
  invoices: '/api/portal/client/services/4/invoices',
} as const

/** Stands in for Nuxt's `navigateTo`, a framework function, not ours. */
const navigate = vi.fn()

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * How many times a path was read.
 *
 * @param path - The path.
 * @returns The count.
 */
const reads = (path: string): number => requestsTo(stub.requests, path).length

/**
 * The selector of a plugin tab in the side menu.
 *
 * @param widgetId - The widget id.
 * @returns The selector.
 */
const pluginTab = (widgetId: string): string => `[data-testid="service-tab-plugin.acme-sites.${widgetId}"]`

/**
 * Builds the replies for a service and a person.
 *
 * @param permissions - The person's permissions.
 * @returns The replies.
 */
const baseTable = (permissions: Permission): StubRouteTable => ({
  [`GET ${PATHS.me}`]: { body: { currency: 'USD', permissions } },
  [`GET ${PATHS.service}`]: { body: service() },
  [`GET ${PATHS.hosting}`]: { body: { nameservers: ['ns1.example.net'], ssl: null } },
  [`GET ${PATHS.cancellation}`]: { body: { pending: false } },
  [`GET ${PATHS.invoices}`]: { body: { invoices: [], unattributedInvoiceCount: 0 } },
  [`GET ${slotPath(PluginSlotId.ServiceTabs)}`]: { body: slotIndex(PluginSlotId.ServiceTabs, []) },
})

/**
 * Mounts the service page with the plugin components its tabs render, and lets its first reads land: they go over a
 * real socket, so the page's async setup ends after the mount's own promise flush.
 *
 * @returns The mounted page.
 */
const mountService = async (): Promise<Awaited<ReturnType<typeof mountPage>>> => {
  const wrapper = await mountPage(ServicePage, { components: PLUGIN_COMPONENTS })
  await vi.waitFor(() => expect(reads(PATHS.service)).toBeGreaterThan(0))
  await settle()
  return wrapper
}

/**
 * Starts the stub with the given replies; plugin routes answer live unless a test changes them.
 *
 * @param replies - The replies.
 * @returns Resolves once the stub listens.
 */
const start = async (replies: StubRouteTable): Promise<void> => {
  table = replies
  const answer = routeTable(table)
  stub = await startTransport(request => pathOf(request).startsWith(STATUS_ROUTE_PREFIX) ? { body: { status: 'live' } } : answer(request))
}

afterEach(async () => {
  vi.useRealTimers()
  await stub.close()
})

describe('service page', () => {
  beforeEach(async () => {
    navigate.mockReset()
    vi.stubGlobal('navigateTo', navigate)
    stubClientRuntime({ params: { id: '4' } })
    await start(baseTable(Permission.All))
  })

  it('reads the service, its hosting info and its cancellation status', async () => {
    await mountService()

    expect(reads(PATHS.service)).toBe(1)
    expect(reads(PATHS.hosting)).toBe(1)
    expect(reads(PATHS.cancellation)).toBe(1)
  })

  it('opens on the overview with the details, the server and the invoices', async () => {
    const wrapper = await mountService()

    expect(wrapper.find('[data-testid="service-details"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="service-server"]').text()).toContain('ns1.example.net')
    expect(wrapper.find('[data-testid="service-invoices"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="service-suspension-reason"]').exists()).toBe(false)
  })

  it('names why a suspended service is suspended', async () => {
    table[`GET ${PATHS.service}`] = { body: service({ status: ServiceStatus.Suspended, suspensionSource: SuspensionSource.Overdue }) }

    const wrapper = await mountService()

    expect(wrapper.find('[data-testid="service-suspension-reason"]').text()).toContain('client.services.suspensionSources.overdue')
  })

  it('hides a leftover suspension source once the service is active again', async () => {
    table[`GET ${PATHS.service}`] = { body: service({ status: ServiceStatus.Active, suspensionSource: SuspensionSource.Overdue }) }

    const wrapper = await mountService()

    expect(wrapper.find('[data-testid="service-suspension-reason"]').exists()).toBe(false)
  })

  it('switches to the password and the cancellation panels', async () => {
    const wrapper = await mountService()

    await wrapper.find('[data-testid="service-tab-password"]').trigger('click')
    expect(wrapper.find('[data-testid="service-password"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="service-overview"]').exists()).toBe(false)

    await wrapper.find('[data-testid="service-tab-cancel"]').trigger('click')
    expect(wrapper.find('[data-testid="service-cancel"]').exists()).toBe(true)
  })

  it('opens the cancellation panel from the quick action', async () => {
    const wrapper = await mountService()

    await wrapper.find('[data-testid="service-quick-cancel"]').trigger('click')

    expect(wrapper.find('[data-testid="service-cancel"]').exists()).toBe(true)
  })

  it('sends a service without a control-panel account to its setup wizard', async () => {
    table[`GET ${PATHS.service}`] = { body: service({ username: null, status: ServiceStatus.Pending }) }

    await mountService()

    expect(navigate).toHaveBeenCalledWith('/client/services/4/setup')
  })

  it('keeps a digital service on its page, which has nothing to set up and no control-panel account', async () => {
    table[`GET ${PATHS.service}`] = { body: service({ username: null, status: ServiceStatus.Pending, isDigital: true }) }

    await mountService()

    expect(navigate).not.toHaveBeenCalled()
  })

  it('does not send a terminated service to setup', async () => {
    table[`GET ${PATHS.service}`] = { body: service({ username: null, status: ServiceStatus.Terminated }) }

    await mountService()

    expect(navigate).not.toHaveBeenCalled()
  })

  it('says the service was not found when the read is refused as not found', async () => {
    table[`GET ${PATHS.service}`] = { status: 404, body: { data: { code: PortalErrorCode.ServiceNotFound } } }

    const wrapper = await mountService()

    expect(wrapper.find('[data-testid="service-not-found"]').text()).toContain('client.services.notFound')
    expect(wrapper.find('[data-testid="service-load-error"]').exists()).toBe(false)
  })

  it('shows the API sentence and a retry when the read fails for another reason, and reads again on retry', async () => {
    let attempts = 0
    table[`GET ${PATHS.service}`] = () => {
      attempts += 1
      // The transport retries a GET once on a 5xx, so the first read is two refused requests.
      return attempts <= 2 ? { status: 503, body: { detail: 'The service could not be read.' } } : { body: service() }
    }

    const wrapper = await mountService()

    expect(wrapper.find('[data-testid="service-load-error"]').text()).toContain('The service could not be read.')
    expect(wrapper.find('[data-testid="service-not-found"]').exists()).toBe(false)

    await wrapper.find('[data-testid="service-retry"]').trigger('click')
    await vi.waitFor(() => expect(wrapper.find('[data-testid="service-details"]').exists()).toBe(true))

    expect(reads(PATHS.service)).toBe(3)
    expect(wrapper.find('[data-testid="service-load-error"]').exists()).toBe(false)
  })
})

describe('service page permissions', () => {
  beforeEach(() => {
    navigate.mockReset()
    vi.stubGlobal('navigateTo', navigate)
    stubClientRuntime({ params: { id: '4' } })
  })

  it('shows neither the invoices nor the password and cancel panels to a person who may only view services', async () => {
    await start(baseTable(Permission.ViewProductsServices))

    const wrapper = await mountService()
    await settle()

    expect(wrapper.find('[data-testid="service-details"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="service-invoices"]').exists()).toBe(false)
    expect(reads(PATHS.invoices)).toBe(0)
    expect(wrapper.find('[data-testid="service-tab-password"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="service-tab-cancel"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="service-quick-cancel"]').exists()).toBe(false)
  })

  it('shows the invoices to a person who may pay invoices', async () => {
    await start(baseTable(Permission.ViewProductsServices | Permission.ViewPayInvoices))

    const wrapper = await mountService()

    expect(wrapper.find('[data-testid="service-invoices"]').exists()).toBe(true)
    expect(reads(PATHS.invoices)).toBe(1)
  })
})

describe('service page plugin tabs', () => {
  beforeEach(async () => {
    navigate.mockReset()
    vi.stubGlobal('navigateTo', navigate)
    stubClientRuntime({ params: { id: '4' } })
    await start(baseTable(Permission.All))
  })

  it('reads the slot with the service on screen and adds no tab without plugins', async () => {
    const wrapper = await mountService()
    await vi.waitFor(() => expect(reads(slotPath(PluginSlotId.ServiceTabs))).toBe(1))

    expect(requestsTo(stub.requests, slotPath(PluginSlotId.ServiceTabs)).map(queryOf)).toEqual([{ serviceId: '4' }])
    expect(wrapper.find('[data-testid^="service-tab-plugin."]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="service-details"]').exists()).toBe(true)
  })

  it('adds no tab and no notice when the backend refuses the slot for the service', async () => {
    table[`GET ${slotPath(PluginSlotId.ServiceTabs)}`] = SERVICE_NOT_FOUND

    const wrapper = await mountService()
    await vi.waitFor(() => expect(reads(slotPath(PluginSlotId.ServiceTabs))).toBe(1))
    await settle()

    expect(wrapper.find('[data-testid^="service-tab-plugin."]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="service-details"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="service-not-found"]').exists()).toBe(false)
  })

  it('adds a tab per widget, labelled by its title or its id, and opens the widget for this service', async () => {
    table[`GET ${slotPath(PluginSlotId.ServiceTabs)}`] = {
      body: { slotId: PluginSlotId.ServiceTabs, widgets: [slotEntry('slow'), { ...slotEntry('ok', null), order: 1 }] },
    }
    table[`GET ${slotWidgetPath(PluginSlotId.ServiceTabs, 'slow')}`] = { body: readingView() }

    const wrapper = await mountService()
    await vi.waitFor(() => expect(wrapper.find(pluginTab('slow')).exists()).toBe(true))
    expect(wrapper.find(pluginTab('slow')).text()).toContain('Widget slow')
    expect(wrapper.find(pluginTab('ok')).text()).toContain('ok')

    await wrapper.get(pluginTab('slow')).trigger('click')
    await vi.waitFor(() => expect(wrapper.find('[data-testid="plugin-slot-widget-acme-sites-slow"]').text()).toContain('live'))

    expect(requestsTo(stub.requests, slotWidgetPath(PluginSlotId.ServiceTabs, 'slow')).map(queryOf)).toEqual([{ serviceId: '4' }])
    expect(wrapper.find('[data-testid="service-overview"]').exists()).toBe(false)
  })

  it('reads no widget view until its tab is opened', async () => {
    table[`GET ${slotPath(PluginSlotId.ServiceTabs)}`] = { body: slotIndex(PluginSlotId.ServiceTabs, ['ok']) }

    const wrapper = await mountService()
    await vi.waitFor(() => expect(wrapper.find(pluginTab('ok')).exists()).toBe(true))
    await settle()

    expect(reads(slotWidgetPath(PluginSlotId.ServiceTabs, 'ok'))).toBe(0)
  })

  it('keeps the page whole when a plugin tab throws', async () => {
    table[`GET ${slotPath(PluginSlotId.ServiceTabs)}`] = { body: slotIndex(PluginSlotId.ServiceTabs, ['broken', 'ok']) }
    table[`GET ${slotWidgetPath(PluginSlotId.ServiceTabs, 'broken')}`] = { body: throwingView() }
    table[`GET ${slotWidgetPath(PluginSlotId.ServiceTabs, 'ok')}`] = { body: workingView() }

    const wrapper = await mountService()
    await vi.waitFor(() => expect(wrapper.find(pluginTab('broken')).exists()).toBe(true))
    await wrapper.get(pluginTab('broken')).trigger('click')
    await vi.waitFor(() => expect(wrapper.find('[data-testid="plugin-slot-unavailable-acme-sites-broken"]').exists()).toBe(true))
    expect(wrapper.find('[data-testid="service-sidebar"]').exists()).toBe(true)

    await wrapper.get(pluginTab('ok')).trigger('click')
    await vi.waitFor(() => expect(wrapper.find('[data-testid="plugin-slot-widget-acme-sites-ok"]').text()).toBe('Site is live'))
    await wrapper.get('[data-testid="service-tab-overview"]').trigger('click')
    expect(wrapper.find('[data-testid="service-details"]').exists()).toBe(true)
  })

  it('11. on another service, the open plugin tab reads its view again for that service', async () => {
    table[`GET ${slotPath(PluginSlotId.ServiceTabs)}`] = { body: slotIndex(PluginSlotId.ServiceTabs, ['ok']) }
    table[`GET ${slotWidgetPath(PluginSlotId.ServiceTabs, 'ok')}`] = { body: workingView() }
    table['GET /api/portal/client/services/5'] = { body: service({ id: 5 }) }
    table['GET /api/portal/client/services/5/hosting-info'] = { body: { nameservers: [], ssl: null } }
    table['GET /api/portal/client/services/5/cancellation-status'] = { body: { pending: false } }
    table['GET /api/portal/client/services/5/invoices'] = { body: { invoices: [], unattributedInvoiceCount: 0 } }
    const first = await mountService()
    await vi.waitFor(() => expect(first.find(pluginTab('ok')).exists()).toBe(true))
    await first.get(pluginTab('ok')).trigger('click')
    await vi.waitFor(() => expect(first.find('[data-testid="plugin-slot-widget-acme-sites-ok"]').exists()).toBe(true))

    // The page reads its id once (`route.params.id`), and Nuxt mounts the page again for another id, which is what this
    // stands in for; the widget key `${activeTab}:${serviceId}` makes the widget start over with the new service.
    first.unmount()
    stubClientRuntime({ params: { id: '5' } })
    const second = await mountService()
    await vi.waitFor(() => expect(second.find(pluginTab('ok')).exists()).toBe(true))
    await second.get(pluginTab('ok')).trigger('click')
    await vi.waitFor(() => expect(second.find('[data-testid="plugin-slot-widget-acme-sites-ok"]').exists()).toBe(true))

    expect(requestsTo(stub.requests, slotPath(PluginSlotId.ServiceTabs)).map(queryOf)).toEqual([{ serviceId: '4' }, { serviceId: '5' }])
    expect(requestsTo(stub.requests, slotWidgetPath(PluginSlotId.ServiceTabs, 'ok')).map(queryOf)).toEqual([{ serviceId: '4' }, { serviceId: '5' }])
  })

  it('gives a slow plugin tab a note after its time limit, the page still usable', async () => {
    table[`GET ${slotPath(PluginSlotId.ServiceTabs)}`] = { body: slotIndex(PluginSlotId.ServiceTabs, ['slow']) }
    table[`GET ${slotWidgetPath(PluginSlotId.ServiceTabs, 'slow')}`] = { hold: true }

    const wrapper = await mountService()
    await vi.waitFor(() => expect(wrapper.find(pluginTab('slow')).exists()).toBe(true))
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    await wrapper.get(pluginTab('slow')).trigger('click')
    vi.advanceTimersByTime(5000)
    await settle()

    expect(wrapper.find('[data-testid="plugin-slot-unavailable-acme-sites-slow"]').exists()).toBe(true)
    await wrapper.get('[data-testid="service-tab-overview"]').trigger('click')
    expect(wrapper.find('[data-testid="service-details"]').exists()).toBe(true)
  })
})
