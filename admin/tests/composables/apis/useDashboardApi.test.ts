/**
 * Tests for the dashboard API composable: paths, verbs and typed results, through the real
 * `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useDashboardApi } from '@/composables/apis/useDashboardApi'
import { DashboardPeriod } from '@/types/dashboardcharts'
import { callOf, json, problem, stubFetch } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useDashboardApi', () => {
  it('fetchStats reads /admin/dashboard/stats', async () => {
    const mock = stubFetch(json({ totalClients: 3 }))

    expect(await useDashboardApi().fetchStats()).toEqual({ totalClients: 3 })
    expect(String(mock.mock.calls[0]?.[0])).toContain('/admin/dashboard/stats')
  })

  it('fetchCharts reads /admin/dashboard/charts for the chosen period', async () => {
    const mock = stubFetch(json({ currency: 'USD' }))

    expect(await useDashboardApi().fetchCharts(DashboardPeriod.Days90)).toEqual({ currency: 'USD' })
    expect(String(mock.mock.calls[0]?.[0])).toContain('/admin/dashboard/charts?period=Days90')
  })

  it('fetchActivity passes the limit in the query', async () => {
    const mock = stubFetch(json([]))

    await useDashboardApi().fetchActivity(5)
    expect(String(mock.mock.calls[0]?.[0])).toContain('/admin/dashboard/activity?limit=5')
  })

  it('markActivityRead posts the event key', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useDashboardApi().markActivityRead('InvoiceOverdue:33:1')

    const call = callOf(mock)
    expect(call.method).toBe('POST')
    expect(call.url).toContain('/admin/dashboard/activity/read')
    expect(call.body).toEqual({ eventKey: 'InvoiceOverdue:33:1' })
  })

  it('markAllActivityRead posts to the read-all route', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useDashboardApi().markAllActivityRead()

    const call = callOf(mock)
    expect(call.method).toBe('POST')
    expect(call.url).toContain('/admin/dashboard/activity/read-all')
  })

  it('rejects when the API refuses', async () => {
    stubFetch(problem('No access.', 403))

    await expect(useDashboardApi().fetchStats()).rejects.toBeTruthy()
  })
})
