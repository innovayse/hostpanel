/**
 * Tests for `pages/client/dashboard.vue` against the real client, content and plugin stores, the
 * real API composables and the real transport, with a stub BFF answering on a real socket (nothing
 * of ours is mocked): the data the page loads, what the cards render from it, the load-error and
 * no-profile notices, the manual refresh, and the cards plugins add, which never change the page
 * when there are none and never take it down when one breaks.
 *
 * @module tests/pages/client/dashboard.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountAuthPage, stubAuthRuntime } from '~/tests/helpers/authScreens'
import { PLUGIN_COMPONENTS } from '~/tests/helpers/mountPlugin'
import { Permission } from '~/types/permission'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { makeClientService } from '~/tests/helpers/clientServiceFixture'
import { slotIndex, slotPath, slotWidgetPath, throwingView, workingView } from '~/tests/helpers/pluginSlotFixture'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { DomainStatus } from '~/types/domainstatus'
import { PluginSlotId } from '~/types/pluginslotid'
import { settle } from '~/tests/helpers/settle'

const { default: DashboardPage } = await import('~/pages/client/dashboard.vue')

/**
 * Mounts the dashboard with the plugin components its slot renders, and waits until its header is on screen: the
 * page's reads go over a real socket, so its async setup ends after the mount's own promise flush.
 *
 * @returns The mounted page.
 */
const mountDashboard = async (): Promise<Awaited<ReturnType<typeof mountAuthPage>>> => {
  const wrapper = await mountAuthPage(DashboardPage, PLUGIN_COMPONENTS)
  await vi.waitFor(() => expect(wrapper.find('[data-testid="dashboard-title"]').exists()).toBe(true))
  return wrapper
}
const { useDashboardSummary } = await import('~/composables/useDashboardSummary')

/** The BFF paths the dashboard reads. */
const PATHS = {
  me: '/api/portal/client/me',
  services: '/api/portal/client/services',
  invoices: '/api/portal/client/invoices',
  domains: '/api/portal/client/domains',
  tickets: '/api/portal/client/tickets',
  news: '/api/portal/public/announcements',
} as const

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
 * Answers every section with one refusal.
 *
 * @param status - The status.
 * @param body - The refusal body.
 * @param paths - The paths to refuse.
 * @returns Nothing.
 */
const refuse = (status: number, body: unknown, paths: string[]): void => {
  for (const path of paths) {
    table[`GET ${path}`] = { status, body }
  }
}

beforeEach(async () => {
  stubAuthRuntime()
  vi.stubGlobal('useDashboardSummary', useDashboardSummary)
  table = {
    [`GET ${PATHS.me}`]: { body: makeClientUser({ email: 'ada@example.com', permissions: Permission.All }) },
    [`GET ${PATHS.services}`]: { body: [makeClientService({ id: 1, productName: 'Starter', domain: 'a.test', username: 'ada', server: null })] },
    [`GET ${PATHS.invoices}`]: { body: [{ id: 7, status: 'Unpaid', dueDate: '2026-11-01', total: 25, currency: 'USD' }] },
    [`GET ${PATHS.domains}`]: { body: [{ id: 3, name: 'a.test', status: DomainStatus.Active, expiresAt: '2030-01-01', nextDueDate: '', registeredAt: '' }] },
    [`GET ${PATHS.tickets}`]: { body: [{ id: 9, subject: 'Help', departmentName: 'Support', status: 'Open' }] },
    [`GET ${PATHS.news}`]: { body: { items: [{ id: 5, title: 'Maintenance', content: '', publishedAt: '2026-10-01T00:00:00Z' }], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 } },
    [`GET ${slotPath(PluginSlotId.DashboardCards)}`]: { body: slotIndex(PluginSlotId.DashboardCards, []) },
  }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('client dashboard page', () => {
  it('shows the title and subtitle in a header the e2e flow can wait for', async () => {
    const wrapper = await mountDashboard()

    const header = wrapper.find('[data-testid="dashboard-title"]')
    expect(header.text()).toContain('client.dashboard.title')
    expect(header.text()).toContain('client.dashboard.subtitle')
  })

  it('loads every section once and renders each card from it', async () => {
    const wrapper = await mountDashboard()
    await settle()

    expect(reads(PATHS.services)).toBe(1)
    expect(reads(PATHS.invoices)).toBe(1)
    expect(reads(PATHS.domains)).toBe(1)
    expect(reads(PATHS.tickets)).toBe(1)
    expect(wrapper.find('[data-testid="dashboard-service-1"]').text()).toContain('Starter')
    expect(wrapper.find('[data-testid="dashboard-invoice-7"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="dashboard-domain-3"]').text()).toContain('a.test')
    expect(wrapper.find('[data-testid="dashboard-ticket-9"]').text()).toContain('Help')
    expect(wrapper.find('[data-testid="dashboard-news-5"]').text()).toContain('Maintenance')
  })

  it('counts the sections in the stat tiles', async () => {
    const wrapper = await mountDashboard()

    expect(wrapper.find('[data-testid="dashboard-stat-services"] [data-testid="ui-stat-tile-value"]').text()).toBe('1')
    expect(wrapper.find('[data-testid="dashboard-stat-invoices"] [data-testid="ui-stat-tile-value"]').text()).toBe('1')
  })

  it('flags the unpaid invoice in the banners', async () => {
    const wrapper = await mountDashboard()

    expect(wrapper.find('[data-testid="dashboard-alert-unpaid"]').exists()).toBe(true)
  })

  it('shows each failed section once as a load error', async () => {
    refuse(503, { detail: 'The billing system is down.' }, [PATHS.services, PATHS.invoices])

    const wrapper = await mountDashboard()

    const alert = wrapper.find('[data-testid="dashboard-load-errors"]')
    expect(alert.text().match(/The billing system is down\./g)).toHaveLength(1)
  })

  it('shows the no-profile notice instead of load errors for a staff account', async () => {
    refuse(404, { type: 'CLIENT_PROFILE_NOT_FOUND', detail: 'No client record.' }, [PATHS.me, PATHS.services, PATHS.invoices, PATHS.domains, PATHS.tickets])

    const wrapper = await mountDashboard()

    expect(wrapper.find('[data-testid="no-profile-notice"]').text()).toContain('No client record.')
    expect(wrapper.find('[data-testid="dashboard-load-errors"]').exists()).toBe(false)
  })

  it('reloads every section from the API when refresh is pressed', async () => {
    const wrapper = await mountDashboard()

    await wrapper.find('[data-testid="dashboard-refresh"]').trigger('click')
    await vi.waitFor(() => expect(reads(PATHS.tickets)).toBe(2))

    expect(reads(PATHS.services)).toBe(2)
    expect(reads(PATHS.invoices)).toBe(2)
    expect(reads(PATHS.domains)).toBe(2)
  })
})

describe('client dashboard page permissions', () => {
  it('neither asks for nor shows the sections the person has no permission for', async () => {
    table[`GET ${PATHS.me}`] = { body: makeClientUser({ email: 'ada@example.com', permissions: Permission.ViewDomains }) }

    const wrapper = await mountDashboard()
    await settle()

    expect(reads(PATHS.domains)).toBe(1)
    expect(reads(PATHS.services)).toBe(0)
    expect(reads(PATHS.invoices)).toBe(0)
    expect(reads(PATHS.tickets)).toBe(0)
    expect(wrapper.find('[data-testid="dashboard-domain-3"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="dashboard-service-1"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="dashboard-invoice-7"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="dashboard-ticket-9"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="dashboard-register-domain"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="dashboard-load-errors"]').exists()).toBe(false)
  })

  it('offers the domain search card to a person who may place orders', async () => {
    table[`GET ${PATHS.me}`] = { body: makeClientUser({ email: 'ada@example.com', permissions: Permission.PlaceNewOrders }) }

    const wrapper = await mountDashboard()

    expect(wrapper.find('[data-testid="dashboard-register-domain"]').exists()).toBe(true)
  })
})

describe('client dashboard plugin cards', () => {
  it('reads the dashboard slot without context and adds nothing without plugins', async () => {
    const wrapper = await mountDashboard()
    await vi.waitFor(() => expect(reads(slotPath(PluginSlotId.DashboardCards))).toBe(1))

    expect(stub.requests.find(request => request.url.startsWith(slotPath(PluginSlotId.DashboardCards)))?.url)
      .toBe(slotPath(PluginSlotId.DashboardCards))
    expect(wrapper.find('[data-testid="plugin-slot-client.dashboard.cards"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="dashboard-title"]').exists()).toBe(true)
  })

  it('shows the cards plugins add, a throwing one as a note, the rest of the page intact', async () => {
    table[`GET ${slotPath(PluginSlotId.DashboardCards)}`] = { body: slotIndex(PluginSlotId.DashboardCards, ['broken', 'ok']) }
    table[`GET ${slotWidgetPath(PluginSlotId.DashboardCards, 'broken')}`] = { body: throwingView() }
    table[`GET ${slotWidgetPath(PluginSlotId.DashboardCards, 'ok')}`] = { body: workingView() }

    const wrapper = await mountDashboard()

    await vi.waitFor(() => expect(wrapper.find('[data-testid="plugin-slot-unavailable-acme-sites-broken"]').exists()).toBe(true))
    await vi.waitFor(() => expect(wrapper.get('[data-testid="plugin-slot-widget-acme-sites-ok"]').text()).toBe('Site is live'))
    expect(wrapper.find('[data-testid="dashboard-service-1"]').text()).toContain('Starter')
  })

  it('keeps the page whole and silent when the slot cannot be read', async () => {
    table[`GET ${slotPath(PluginSlotId.DashboardCards)}`] = { status: 503, body: { detail: 'Plugins are down.' } }

    const wrapper = await mountDashboard()
    // The transport retries a GET once on a 5xx, so a refused slot read is two requests.
    await vi.waitFor(() => expect(reads(slotPath(PluginSlotId.DashboardCards))).toBe(2))
    await settle()

    expect(wrapper.find('[data-testid="plugin-slot-client.dashboard.cards"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="dashboard-load-errors"]').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('Plugins are down.')
  })
})
