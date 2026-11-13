/**
 * Tests for the dashboard store: data on success, the API sentence on refusal, the offline fallback.
 */

import { describe, expect, it, vi } from 'vitest'
import { useDashboardStore } from '@/stores/dashboard'
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

  it('fetchOverview fills every section from its list', async () => {
    const page = { items: [{ id: 1, priority: 'High' }, { id: 2, priority: 'Low' }], pageNumber: 1, pageSize: 5, totalCount: 2, totalPages: 1 }
    vi.stubGlobal('fetch', vi.fn((url: string) => Promise.resolve(
      url.includes('/reports/annual-income') || url.includes('/admin/servers') ? json([{ id: 1 }]) : json(page),
    )))
    const store = useDashboardStore()

    await store.fetchOverview()
    expect(store.overview.income).toEqual([{ id: 1 }])
    expect(store.overview.servers).toEqual([{ id: 1 }])
    expect(store.overview.pendingOrders).toBe(2)
    expect(store.overview.urgentTickets).toEqual([{ id: 1, priority: 'High' }])
    expect(store.overviewError).toBeNull()
    expect(store.overviewLoading).toBe(false)
  })

  it('fetchOverview asks for the four latest invoices of the concept, which keep the dashboard on one screen', async () => {
    const page = { items: [], pageNumber: 1, pageSize: 4, totalCount: 0, totalPages: 1 }
    const fetchMock = vi.fn((url: string) => Promise.resolve(
      url.includes('/reports/annual-income') || url.includes('/admin/servers') ? json([]) : json(page),
    ))
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
    expect(store.overview.income).toEqual([])
    expect(store.overview.overdueInvoices).toBeNull()
  })
})
