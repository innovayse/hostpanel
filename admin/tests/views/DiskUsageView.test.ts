/**
 * Tests of DiskUsageView: it mounts, shows its title and data, and shows the refusal sentence with Retry when the API
 * refuses, and runs its own actions.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import DiskUsageView from '@/views/DiskUsageView.vue'
import schema from '@/views/DiskUsageView.schema'
import { json } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { runReportCase } from '../helpers/reportViews'
import { requestsOf, stubRouteTable } from '../helpers/routes'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportCase({
  name: 'DiskUsageView',
  view: DiskUsageView,
  schema,
  routes: {
    'GET /reports/disk-usage': () => json({
      lastUpdated: null,
      servers: [{
        serverName: 'srv-1',
        rows: [{ clientName: 'Beta', domain: 'beta.test', diskUsage: '1 GB', diskLimit: '2 GB', diskPercent: 95, bwUsage: '1 GB', bwLimit: '5 GB', bwPercent: 10 }],
      }],
    }),
  },
  expected: ['Disk Space & Bandwidth Usage Summary', 'srv-1', 'beta.test', '95%'],
  retryable: true,
})

/** A disk usage report with one server and no accounts. */
const emptyDisk = { lastUpdated: null, servers: [{ serverName: 'srv-1', rows: [] }] }

describe('DiskUsageView actions', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('refreshes through a POST and shows the fresh figures', async () => {
    const mock = stubRouteTable({
      'GET /reports/disk-usage': () => json(emptyDisk),
      'POST /reports/disk-usage/update': () => json({ ...emptyDisk, servers: [{ serverName: 'srv-2', rows: [] }] }),
    })
    const wrapper = await mountView(DiskUsageView)

    await wrapper.get('[data-testid="report-update-now"]').trigger('click')
    await flushPromises()

    expect(requestsOf(mock)).toContain('POST /reports/disk-usage/update')
    expect(wrapper.text()).toContain('srv-2')
  })

  it('shows one empty state with the update action inside when no server has figures', async () => {
    stubRouteTable({ 'GET /reports/disk-usage': () => json({ lastUpdated: null, servers: [], failures: [] }) })
    const wrapper = await mountView(DiskUsageView)

    expect(wrapper.text().match(/No data found/g)).toHaveLength(1)
    expect(wrapper.text()).not.toContain('No data yet')
    expect(wrapper.findAll('[data-testid="report-update-now"]')).toHaveLength(1)
    expect(wrapper.get('[data-testid="ui-empty-state-actions"]').find('[data-testid="report-update-now"]').exists()).toBe(true)
  })

  it('lists the servers a refresh could not read, with the reason', async () => {
    stubRouteTable({
      'GET /reports/disk-usage': () => json({ lastUpdated: null, servers: [], failures: [] }),
      'POST /reports/disk-usage/update': () => json({
        lastUpdated: null,
        servers: [],
        failures: [
          { serverName: 'cwp-1', reason: 'UnsupportedModule' },
          { serverName: 'cp-2', reason: 'Unreachable' },
          { serverName: 'cp-3', reason: 'MissingCredentials' },
        ],
      }),
    })
    const wrapper = await mountView(DiskUsageView)

    await wrapper.get('[data-testid="report-update-now"]').trigger('click')
    await flushPromises()

    const failures = wrapper.findAll('[data-testid="report-failure"]').map(row => row.text())
    expect(failures).toHaveLength(3)
    expect(failures[0]).toContain('cwp-1')
    expect(failures[0]).toContain('cannot report disk and bandwidth')
    expect(failures[1]).toContain('did not answer')
    expect(failures[2]).toContain('no API token')
  })

  it('writes the time of the last fetch as a date and time', async () => {
    stubRouteTable({ 'GET /reports/disk-usage': () => json({ lastUpdated: '2026-10-14T09:30:00', servers: [{ serverName: 'srv-1', rows: [] }], failures: [] }) })
    const wrapper = await mountView(DiskUsageView)

    expect(wrapper.get('[data-testid="report-last-updated"]').text()).toContain('14/10/2026, 09:30')
  })

  it('draws the table skeleton while a refresh is running', async () => {
    let finish: () => void = () => undefined
    stubRouteTable({
      'GET /reports/disk-usage': () => json({ lastUpdated: null, servers: [], failures: [] }),
      'POST /reports/disk-usage/update': () => new Promise<Response>((resolve) => {
        finish = () => resolve(json({ lastUpdated: null, servers: [], failures: [] }))
      }),
    })
    const wrapper = await mountView(DiskUsageView)

    await wrapper.get('[data-testid="report-update-now"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="report-table"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="ui-empty-state-actions"]').exists()).toBe(false)
    finish()
    await flushPromises()
    expect(wrapper.find('[data-testid="ui-empty-state-actions"]').exists()).toBe(true)
  })
})
