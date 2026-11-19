/**
 * Tests of ProductSuspensionsView: it mounts, shows its title and data, shows the refusal sentence with Retry when the API
 * refuses, and reads the report again on Refresh.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import ProductSuspensionsView from '@/views/ProductSuspensionsView.vue'
import schema from '@/views/ProductSuspensionsView.schema'
import { json } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { runReportCase } from '../helpers/reportViews'
import { runReportSchemaCases, schemaCaptionsOf } from '../helpers/reportSchema'
import { stubRouteTable } from '../helpers/routes'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportSchemaCases([{ name: 'ProductSuspensionsView', view: ProductSuspensionsView, schema }])

runReportCase({
  name: 'ProductSuspensionsView',
  view: ProductSuspensionsView,
  routes: {
    'GET /reports/product-suspensions': () => json([
      { serviceId: 1, clientName: 'Beta', productName: 'Basic', domain: null, nextDueDate: null, suspendReason: 'Unpaid invoice' },
    ]),
  },
  expected: [...schemaCaptionsOf(schema), 'Product Suspensions', 'Beta', 'Unpaid invoice'],
  retryable: true,
})

describe('ProductSuspensionsView actions', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('reads the report again when Refresh is pressed', async () => {
    const calls: string[] = []
    stubRouteTable({
      'GET /reports/product-suspensions': () => {
        calls.push('read')
        return json([])
      },
    })
    const wrapper = await mountView(ProductSuspensionsView)
    await flushPromises()

    await wrapper.get('[data-testid="report-refresh"]').trigger('click')
    await flushPromises()

    expect(calls).toHaveLength(2)
  })
})
