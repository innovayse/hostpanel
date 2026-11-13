/**
 * Tests for ReportQueryPage: the shared layout of the query reports, driven by a report state it is
 * handed (the state itself is covered by the useQueryReport tests).
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import ReportQueryPage from '@/components/reports/ReportQueryPage.vue'
import type { ReportColumn } from '@/types/reportcolumn'
import type { ReportQueryView } from '@/types/reportquery'
import { useReportsStore } from '@/stores/reports'
import { createTestI18n } from '../../helpers/i18n'

/** Every column of the test report. */
const columns: ReportColumn[] = [
  { key: 'id', label: 'ID' },
  { key: 'clientName', label: 'Client' },
]

/** The one row of the default report: an id plus the column the table draws. */
const defaultRows = [{ id: 1, clientName: 'Acme' }]

/**
 * Builds a report state with spies for every action.
 *
 * @param overrides - Fields to replace.
 * @returns The state.
 */
const makeReport = (overrides: Partial<ReportQueryView> = {}): ReportQueryView => ({
  rows: defaultRows,
  totalCount: 1,
  page: 1,
  pageSize: 50,
  filters: [],
  ranges: { created: null },
  visibleKeys: new Set(['id', 'clientName']),
  activeColumns: columns,
  load: vi.fn(() => Promise.resolve()),
  generate: vi.fn(() => Promise.resolve()),
  goToPage: vi.fn(() => Promise.resolve()),
  setFilters: vi.fn(),
  setRange: vi.fn(),
  toggleColumn: vi.fn(),
  selectAllColumns: vi.fn(),
  clearColumns: vi.fn(),
  print: vi.fn(),
  exportCsv: vi.fn(),
  ...overrides,
})

/**
 * Mounts the page.
 *
 * @param report - The report state.
 * @param exportable - Whether the export button shows.
 * @returns The wrapper.
 */
const mountPage = (report: ReportQueryView, exportable = false) =>
  mount(ReportQueryPage, {
    props: {
      title: 'Invoices',
      description: 'A custom export',
      report,
      columns,
      ranges: [{ key: 'created', label: 'Creation Date', param: 'created' }],
      filterFields: [{ value: 'clientName', label: 'Client' }],
      foundText: '1 invoice(s) found',
      emptyText: 'No invoices found.',
      exportable,
    },
    global: { plugins: [createTestI18n(), createPinia()] },
  })

describe('ReportQueryPage', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('shows the title, the found line, one date range and the chosen columns with their rows', () => {
    const wrapper = mountPage(makeReport())

    expect(wrapper.text()).toContain('Invoices')
    expect(wrapper.get('[data-testid="report-found"]').text()).toBe('1 invoice(s) found')
    expect(wrapper.find('[data-testid="report-range-created"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Creation Date')
    expect(wrapper.findAll('th').map(header => header.text())).toEqual(['ID', 'Client'])
    expect(wrapper.text()).toContain('Acme')
  })

  it('shows the empty text when the report has no rows', () => {
    const wrapper = mountPage(makeReport({ rows: [], totalCount: 0 }))

    expect(wrapper.text()).toContain('No invoices found.')
  })

  it('asks for the report and for printing through the state', async () => {
    const report = makeReport()
    const wrapper = mountPage(report)

    await wrapper.get('[data-testid="report-generate"]').trigger('click')
    await wrapper.get('[data-testid="report-print"]').trigger('click')

    expect(report.generate).toHaveBeenCalledTimes(1)
    expect(report.print).toHaveBeenCalledTimes(1)
  })

  it('offers the export only when the report has one', async () => {
    const report = makeReport()

    expect(mountPage(report).find('[data-testid="report-export"]').exists()).toBe(false)

    const wrapper = mountPage(report, true)
    await wrapper.get('[data-testid="report-export"]').trigger('click')
    expect(report.exportCsv).toHaveBeenCalledTimes(1)
  })

  it('reloads the current page through the state when Retry is pressed after a refused load', async () => {
    const report = makeReport()
    const wrapper = mountPage(report)
    useReportsStore().error = 'No access.'
    await wrapper.vm.$nextTick()

    await wrapper.get('[data-testid="report-error-retry"]').trigger('click')

    expect(report.load).toHaveBeenCalledTimes(1)
  })

  it('shows the pager only when the rows do not fit one page', () => {
    expect(mountPage(makeReport()).find('[data-testid="report-pagination-next"]').exists()).toBe(false)
    expect(mountPage(makeReport({ totalCount: 120 })).find('[data-testid="report-pagination-next"]').exists()).toBe(true)
  })
})
