/**
 * Tests of SslMonitoringView: it mounts, shows its title and data, and shows the refusal sentence with Retry when the API
 * refuses, and runs its own actions.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import SslMonitoringView from '@/views/SslMonitoringView.vue'
import schema from '@/views/SslMonitoringView.schema'
import { json } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { runReportCase } from '../helpers/reportViews'
import { runReportSchemaCases, schemaCaptionsOf } from '../helpers/reportSchema'
import { requestsOf, stubRouteTable } from '../helpers/routes'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportSchemaCases([{ name: 'SslMonitoringView', view: SslMonitoringView, schema }])

runReportCase({
  name: 'SslMonitoringView',
  view: SslMonitoringView,
  routes: {
    'GET /reports/ssl-monitoring': () => json({
      groups: [{
        group: 'Within30Days',
        rows: [{ domainName: 'beta.test', hasSsl: true, issuer: 'Test CA', expiresAt: '2030-01-01', lastUpdate: '2024-01-01', isActive: true }],
      }],
    }),
  },
  expected: [...schemaCaptionsOf(schema), 'SSL Certificate Monitoring', 'Expires within 30 days', 'beta.test', 'Valid', 'Test CA', '01/01/2030'],
  retryable: true,
})

/** A report with domains in two of the five groups. */
const twoGroups = {
  groups: [
    { group: 'Within30Days', rows: [{ domainName: 'a.test', hasSsl: true, issuer: 'CA', expiresAt: '2030-01-01', lastUpdate: '2024-01-01', isActive: true }] },
    { group: 'Within90Days', rows: [] },
    { group: 'NoSsl', rows: [
      { domainName: 'b.test', hasSsl: false, issuer: null, expiresAt: null, lastUpdate: '2024-01-01', isActive: true },
      { domainName: 'c.test', hasSsl: false, issuer: null, expiresAt: null, lastUpdate: '2024-01-01', isActive: true },
    ] },
  ],
}

describe('SslMonitoringView groups', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('counts the domains of every group in a chip and draws a section only for the filled ones', async () => {
    stubRouteTable({ 'GET /reports/ssl-monitoring': () => json(twoGroups) })
    const wrapper = await mountView(SslMonitoringView)

    const chips = wrapper.findAll('[data-testid="ssl-summary-chip"]').map((chip) => chip.text())
    expect(chips).toEqual(['Expires within 30 days: 1', 'Expires within 90 days: 0', 'No SSL detected: 2'])
    expect(wrapper.findAll('[data-testid="report-group-label"]').map((label) => label.text())).toEqual(['Expires within 30 days', 'No SSL detected'])
    expect(wrapper.text()).not.toContain('No Records Found')
  })

  it('shows one empty state and no chips when no group holds a domain', async () => {
    stubRouteTable({ 'GET /reports/ssl-monitoring': () => json({ groups: [{ group: 'NoSsl', rows: [] }] }) })
    const wrapper = await mountView(SslMonitoringView)

    expect(wrapper.text()).toContain('No domains to monitor.')
    expect(wrapper.find('[data-testid="ssl-summary"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="report-group-label"]').exists()).toBe(false)
  })
})

describe('SslMonitoringView actions', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('reloads with inactive domains when the switch is pressed', async () => {
    const mock = stubRouteTable({ 'GET /reports/ssl-monitoring': () => json({ groups: [] }) })
    const wrapper = await mountView(SslMonitoringView)

    await wrapper.get('[data-testid="report-toggle-inactive"]').trigger('click')
    await flushPromises()

    expect(requestsOf(mock)).toEqual([
      'GET /reports/ssl-monitoring?includeInactive=false',
      'GET /reports/ssl-monitoring?includeInactive=true',
    ])
  })
})
