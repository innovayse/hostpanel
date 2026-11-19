/**
 * Tests of CustomerRetentionView: it mounts, shows its title and data, and shows the refusal sentence with Retry when the API
 * refuses, and runs its own actions.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { ReportRetentionGroupKind } from '@/types/reportretention'
import CustomerRetentionView from '@/views/CustomerRetentionView.vue'
import schema from '@/views/CustomerRetentionView.schema'
import { json } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { runReportCase } from '../helpers/reportViews'
import { requestsOf, stubRouteTable } from '../helpers/routes'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportCase({
  name: 'CustomerRetentionView',
  view: CustomerRetentionView,
  schema,
  routes: {
    'GET /reports/customer-retention': () => json({
      groups: [
        {
          kind: ReportRetentionGroupKind.ProductGroup,
          groupName: 'Hosting',
          rows: [{ productName: 'Basic', billingCycle: 'Monthly', termYears: null, productCount: 2, avgDaysActive: 400, avgYears: 1, avgMonths: 1 }],
        },
        {
          kind: ReportRetentionGroupKind.Domains,
          groupName: null,
          rows: [{ productName: 'com', billingCycle: null, termYears: 1, productCount: 3, avgDaysActive: 12, avgYears: 0, avgMonths: 0 }],
        },
      ],
    }),
  },
  expected: ['Average Customer Retention Time', 'Hosting', 'Basic', '1 year 1 month', 'Domains', '1 year', '12 days'],
  retryable: true,
})

describe('CustomerRetentionView loading', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('draws its real columns with a skeleton while the report is in flight', async () => {
    stubRouteTable({ 'GET /reports/customer-retention': () => new Promise<Response>(() => undefined) })
    const wrapper = await mountView(CustomerRetentionView)

    expect(wrapper.find('[data-testid="ui-table-skeleton"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Billing Cycle')
    expect(wrapper.find('[data-testid="report-include-active"]').exists()).toBe(true)
  })
})

describe('CustomerRetentionView actions', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('reloads when the checkbox is toggled', async () => {
    const mock = stubRouteTable({ 'GET /reports/customer-retention': () => json({ groups: [] }) })
    const wrapper = await mountView(CustomerRetentionView)

    await wrapper.get('[data-testid="report-include-active"]').trigger('click')
    await flushPromises()

    expect(requestsOf(mock)).toEqual([
      'GET /reports/customer-retention?includeActive=true',
      'GET /reports/customer-retention?includeActive=false',
    ])
  })
})
