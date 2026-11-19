/**
 * Tests of DomainsReportView: it mounts, shows its title and data, and shows the refusal sentence when the API
 * refuses.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import DomainsReportView from '@/views/DomainsReportView.vue'
import schema from '@/views/DomainsReportView.schema'
import { json } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { runReportCase } from '../helpers/reportViews'
import { stubRouteTable } from '../helpers/routes'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportCase({
  name: 'DomainsReportView',
  view: DomainsReportView,
  schema,
  emptyResult: true,
  routes: { 'GET /reports/domains': () => json({ items: [], pageNumber: 1, pageSize: 50, totalCount: 0, totalPages: 0 }) },
  expected: ['Domains', '0 domain(s) found', 'No domains found.'],
})

describe('DomainsReportView ranges', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('offers the three date ranges of its schema, each keyed by the query parameter it sends', async () => {
    stubRouteTable({ 'GET /reports/domains': () => json({ items: [], pageNumber: 1, pageSize: 50, totalCount: 0, totalPages: 0 }) })
    const wrapper = await mountView(DomainsReportView)

    for (const key of ['registered', 'nextDue', 'expires']) {
      expect(wrapper.find(`[data-testid="report-range-${key}"]`).exists()).toBe(true)
    }
  })
})
