/**
 * Tests for the dashboard store: data on success, the API sentence on refusal, the offline fallback, and the chart
 * series of the chosen period (the period is sent, the newest request wins).
 */

import { describe, expect, it, vi } from 'vitest'
import { useDashboardStore } from '@/stores/dashboard'
import { DashboardPeriod } from '@/types/dashboardcharts'
import { dashboardCharts } from '../helpers/dashboardFixtures'
import { json, problem, stubFetch, stubOffline } from '../helpers/fetch'
import { installStoreHooks } from '../helpers/storeHooks'

describe('dashboardStore', () => {
  installStoreHooks()

  it('fetchStats stores the statistics', async () => {
    stubFetch(json({ totalClients: 7 }))
    const store = useDashboardStore()

    await store.fetchStats()
    expect(store.stats).toEqual({ totalClients: 7 })
    expect(store.error).toBeNull()
    expect(store.loading).toBe(false)
  })

  it('fetchStats shows the API sentence on refusal', async () => {
    stubFetch(problem('No access.', 403))
    const store = useDashboardStore()

    await store.fetchStats()
    expect(store.stats).toBeNull()
    expect(store.error).toBe('No access.')
    expect(store.loading).toBe(false)
  })

  it('fetchStats falls back when nothing answered', async () => {
    stubOffline()
    const store = useDashboardStore()

    await store.fetchStats()
    expect(store.error).toBe('Could not reach the server. Check your connection and try again.')
  })

  it('fetchCharts asks for the 30 days first and stores the series', async () => {
    const mock = stubFetch(json(dashboardCharts()))
    const store = useDashboardStore()

    await store.fetchCharts()

    expect(String(mock.mock.calls[0]?.[0])).toContain('/admin/dashboard/charts?period=Days30')
    expect(store.period).toBe(DashboardPeriod.Days30)
    expect(store.charts?.income.total).toBe(450)
    expect(store.chartsError).toBeNull()
    expect(store.chartsLoading).toBe(false)
  })

  it('fetchCharts chooses the period it is given and asks for it', async () => {
    const mock = stubFetch(json(dashboardCharts()))
    const store = useDashboardStore()

    await store.fetchCharts(DashboardPeriod.Days90)

    expect(String(mock.mock.calls[0]?.[0])).toContain('period=Days90')
    expect(store.period).toBe(DashboardPeriod.Days90)
  })

  it('fetchCharts keeps the previous series while the next period loads', async () => {
    const store = useDashboardStore()
    stubFetch(json(dashboardCharts()))
    await store.fetchCharts()
    let release: (response: Response) => void = () => undefined
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>((resolve) => { release = resolve })))

    const pending = store.fetchCharts(DashboardPeriod.Days90)

    expect(store.chartsLoading).toBe(true)
    expect(store.charts?.income.total).toBe(450)
    release(json(dashboardCharts()))
    await pending
    expect(store.chartsLoading).toBe(false)
  })

  it('fetchCharts drops the answer of an older period that arrives after a newer one', async () => {
    const store = useDashboardStore()
    const older = { ...dashboardCharts(), currency: 'OLD' }
    const newer = { ...dashboardCharts(), currency: 'NEW' }
    const releases: Array<(response: Response) => void> = []
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>((resolve) => { releases.push(resolve) })))

    const first = store.fetchCharts(DashboardPeriod.Days30)
    const second = store.fetchCharts(DashboardPeriod.Days90)
    releases[1]?.(json(newer))
    await second
    releases[0]?.(json(older))
    await first

    expect(store.charts?.currency).toBe('NEW')
    expect(store.period).toBe(DashboardPeriod.Days90)
    expect(store.chartsLoading).toBe(false)
  })

  it('fetchCharts shows the API sentence on refusal and keeps the series it had', async () => {
    const store = useDashboardStore()
    stubFetch(json(dashboardCharts()))
    await store.fetchCharts()
    stubFetch(problem('No access.', 403))

    await store.fetchCharts(DashboardPeriod.Days90)

    expect(store.chartsError).toBe('No access.')
    expect(store.charts?.income.total).toBe(450)
    expect(store.chartsLoading).toBe(false)
  })

  it('fetchCharts keeps the period of the series on screen when another period fails to load', async () => {
    const store = useDashboardStore()
    stubFetch(json(dashboardCharts()))
    await store.fetchCharts(DashboardPeriod.Days30)
    stubFetch(problem('No access.', 403))

    await store.fetchCharts(DashboardPeriod.Days90)

    expect(store.period).toBe(DashboardPeriod.Days30)
    expect(store.charts).not.toBeNull()
  })

  it('fetchCharts retries the period the charts cover when no period is given after a failure', async () => {
    const store = useDashboardStore()
    stubFetch(problem('No access.', 403))
    await store.fetchCharts(DashboardPeriod.Days90)
    const mock = stubFetch(json(dashboardCharts()))

    await store.fetchCharts()

    expect(String(mock.mock.calls[0]?.[0])).toContain('period=Days30')
  })

  it('fetchOverview fills every section from its list', async () => {
    const page = { items: [{ id: 1, priority: 'High' }, { id: 2, priority: 'Low' }], pageNumber: 1, pageSize: 5, totalCount: 2, totalPages: 1 }
    vi.stubGlobal('fetch', vi.fn((url: string) => Promise.resolve(url.includes('/admin/servers') ? json([{ id: 1 }]) : json(page))))
    const store = useDashboardStore()

    await store.fetchOverview()
    expect(store.overview.servers).toEqual([{ id: 1 }])
    expect(store.overview.pendingOrders).toBe(2)
    expect(store.overview.urgentTickets).toEqual([{ id: 1, priority: 'High' }])
    expect(store.overviewError).toBeNull()
    expect(store.overviewLoading).toBe(false)
  })

  it('fetchOverview asks for the four latest invoices of the concept, which keep the dashboard on one screen', async () => {
    const page = { items: [], pageNumber: 1, pageSize: 4, totalCount: 0, totalPages: 1 }
    const fetchMock = vi.fn((url: string) => Promise.resolve(url.includes('/admin/servers') ? json([]) : json(page)))
    vi.stubGlobal('fetch', fetchMock)
    const store = useDashboardStore()

    await store.fetchOverview()

    const invoiceUrls = fetchMock.mock.calls.map(call => String(call[0])).filter(url => url.includes('/billing?') && !url.includes('status='))
    expect(invoiceUrls.some(url => url.includes('pageSize=4'))).toBe(true)
  })

  it('fetchOverview keeps the first refusal sentence and leaves the sections empty', async () => {
    stubFetch(problem('No access.', 403))
    const store = useDashboardStore()

    await store.fetchOverview()
    expect(store.overviewError).toBe('No access.')
    expect(store.overview.servers).toEqual([])
    expect(store.overview.overdueInvoices).toBeNull()
  })
})
