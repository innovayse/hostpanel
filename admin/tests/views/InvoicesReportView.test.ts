/**
 * Tests of InvoicesReportView: it mounts, shows its title and data, and shows the refusal sentence when the API
 * refuses, and runs its own actions.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import InvoicesReportView from '@/views/InvoicesReportView.vue'
import schema from '@/views/InvoicesReportView.schema'
import { DateRangePreset, presetIsoRange } from '@/utils/ui/datePicker'
import { json } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { runReportCase } from '../helpers/reportViews'
import { requestsOf, stubRouteTable } from '../helpers/routes'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportCase({
  name: 'InvoicesReportView',
  view: InvoicesReportView,
  schema,
  emptyResult: true,
  routes: { 'GET /reports/invoices': () => json({ items: [], pageNumber: 1, pageSize: 50, totalCount: 0, totalPages: 0 }) },
  expected: ['Invoices', '0 invoice(s) found', 'No invoices found.'],
})

describe('InvoicesReportView actions', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('loads again on Generate and opens the export in a new tab', async () => {
    const mock = stubRouteTable({ 'GET /reports/invoices': () => json({ items: [], pageNumber: 1, pageSize: 50, totalCount: 0, totalPages: 0 }) })
    const open = vi.fn()
    vi.stubGlobal('open', open)
    const wrapper = await mountView(InvoicesReportView)

    await wrapper.get('[data-testid="report-generate"]').trigger('click')
    await wrapper.get('[data-testid="report-export"]').trigger('click')
    await flushPromises()

    const [from, to] = presetIsoRange(DateRangePreset.ThisMonth)
    const request = `GET /reports/invoices?createdFrom=${from}&createdTo=${to}&page=1&pageSize=50`
    expect(requestsOf(mock)).toEqual([request, request])
    expect(open).toHaveBeenCalledWith(`/api/reports/invoices/export?createdFrom=${from}&createdTo=${to}`, '_blank')
  })

  it('offers only date ranges the endpoint filters by', async () => {
    stubRouteTable({ 'GET /reports/invoices': () => json({ items: [], pageNumber: 1, pageSize: 50, totalCount: 0, totalPages: 0 }) })
    const wrapper = await mountView(InvoicesReportView)

    for (const key of ['created', 'due', 'paid']) {
      expect(wrapper.find(`[data-testid="report-range-${key}"]`).exists()).toBe(true)
    }
    for (const key of ['refunded', 'cancelled']) {
      expect(wrapper.find(`[data-testid="report-range-${key}"]`).exists()).toBe(false)
    }
  })
})
