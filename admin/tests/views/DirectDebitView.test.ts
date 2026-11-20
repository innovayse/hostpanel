/**
 * Tests of DirectDebitView: it mounts, shows its title and data, and shows the refusal sentence with Retry when the API
 * refuses.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import DirectDebitView from '@/views/DirectDebitView.vue'
import schema from '@/views/DirectDebitView.schema'
import { json } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { runReportCase } from '../helpers/reportViews'
import { stubRouteTable } from '../helpers/routes'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportCase({
  name: 'DirectDebitView',
  view: DirectDebitView,
  schema,
  routes: {
    'GET /reports/direct-debit': () => json({
      rows: [{
        invoiceId: 7, clientName: 'Beta', invoiceDate: '2024-01-01', dueDate: '2024-01-10', subtotal: 10, tax: 1,
        credit: 0, total: 11, bankName: 'First Bank', bankAccountType: null, bankCode: null, bankAccountNumber: null,
      }],
    }),
  },
  expected: ['Direct Debit Processing', 'Beta', 'First Bank', '$11.00'],
  retryable: true,
})

describe('DirectDebitView loading', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('draws its table with every column and skeleton rows, never an empty page, while the report is in flight', async () => {
    stubRouteTable({ 'GET /reports/direct-debit': () => new Promise<Response>(() => undefined) })
    const wrapper = await mountView(DirectDebitView)

    expect(wrapper.findAll('th')).toHaveLength(schema.table?.columns.length ?? 0)
    expect(wrapper.find('[data-testid="ui-table-skeleton"]').exists()).toBe(true)
    expect(wrapper.text()).not.toContain('No data found')
  })
})
