/**
 * Tests of ServicesReportView: it mounts, shows its title and data, and shows the refusal sentence when the API
 * refuses, and runs its own actions.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import ServicesReportView from '@/views/ServicesReportView.vue'
import schema from '@/views/ServicesReportView.schema'
import { json } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { runReportCase } from '../helpers/reportViews'
import { runReportSchemaCases } from '../helpers/reportSchema'
import { stubRouteTable } from '../helpers/routes'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportSchemaCases([{ name: 'ServicesReportView', view: ServicesReportView, schema }])

runReportCase({
  name: 'ServicesReportView',
  view: ServicesReportView,
  routes: { 'GET /reports/services': () => json({ items: [], pageNumber: 1, pageSize: 50, totalCount: 0, totalPages: 0 }) },
  expected: ['Services', '0 service(s) found', 'No services found.'],
})

describe('ServicesReportView actions', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('offers only date ranges the endpoint filters by', async () => {
    stubRouteTable({ 'GET /reports/services': () => json({ items: [], pageNumber: 1, pageSize: 50, totalCount: 0, totalPages: 0 }) })
    const wrapper = await mountView(ServicesReportView)

    for (const key of ['created', 'nextDue', 'terminated']) {
      expect(wrapper.find(`[data-testid="report-range-${key}"]`).exists()).toBe(true)
    }
    expect(wrapper.find('[data-testid="report-range-completed"]').exists()).toBe(false)
  })
})
