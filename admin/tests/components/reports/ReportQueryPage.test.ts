/**
 * Tests for ReportQueryPage: the shared layout of the query reports, driven by a report state it is
 * handed (the state itself is covered by the useQueryReport tests).
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import ReportQueryPage from '@/components/reports/ReportQueryPage.vue'
import UiAdvancedFilters from '@/components/ui/UiAdvancedFilters.vue'
import type { ReportColumn } from '@/types/reportcolumn'
import type { ReportQueryView } from '@/types/reportquery'
import { UiFilterCondition } from '@/utils/ui/filter'
import { ReportAction } from '@/utils/ui/reportParts'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import { useReportsStore } from '@/stores/reports'
import { createTestI18n } from '../../helpers/i18n'
import { stubPhone } from '../../helpers/phone'

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
 * @param exportable - Whether the schema lists the export button beside print.
 * @returns The wrapper.
 */
const mountPage = (report: ReportQueryView, exportable = false) =>
  mount(ReportQueryPage, {
    props: {
      schema: {
        kind: SkeletonKind.Report,
        actions: exportable ? [ReportAction.Export, ReportAction.Print] : [ReportAction.Print],
        header: { title: 'shared.invoices', subtitle: 'reports.invoicesReport.thisReportCanBeUsed' },
        emptyText: 'shared.noInvoicesFound',
      },
      report,
      columns,
      ranges: [{ key: 'created', label: 'Creation Date', param: 'created' }],
      filterFields: [{ value: 'clientName', label: 'Client' }],
      defaultFilterField: 'clientName',
      foundText: '1 invoice(s) found',
    },
    global: { plugins: [createTestI18n(), createPinia()], stubs: { teleport: true } },
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

  it('shows the empty sentence of its schema when the report has no rows', async () => {
    const wrapper = mountPage(makeReport({ rows: [], totalCount: 0 }))
    await nextTick()

    expect(wrapper.text()).toContain(createTestI18n().global.t('shared.noInvoicesFound'))
  })

  it('shows an ask to choose fields, not an empty bar, when no column is chosen', () => {
    const wrapper = mountPage(makeReport({ visibleKeys: new Set(), activeColumns: [] }))

    expect(wrapper.find('th').exists()).toBe(false)
    expect(wrapper.find('[data-testid="report-table-row"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Choose the fields to include to see the results.')
  })

  it('starts the first filter row on the default field', () => {
    const wrapper = mountPage(makeReport())

    expect(wrapper.findAllComponents(UiAdvancedFilters)[0]?.props('defaultField')).toBe('clientName')
  })

  it('asks for the report and for printing through the state', async () => {
    const report = makeReport()
    const wrapper = mountPage(report)

    await wrapper.get('[data-testid="report-generate"]').trigger('click')
    await wrapper.get('[data-testid="report-print"]').trigger('click')

    expect(report.generate).toHaveBeenCalledTimes(1)
    expect(report.print).toHaveBeenCalledTimes(1)
  })

  it('offers the export only when its schema lists it', async () => {
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

describe('ReportQueryPage on a phone', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('counts the date ranges that are set and the advanced rows on the Filters button, and keeps them in the sheet', async () => {
    stubPhone(true)
    const filters = [{ field: 'clientName', condition: UiFilterCondition.Exact, value: 'Acme' }]
    const wrapper = mountPage(makeReport({ ranges: { created: ['2026-01-01', '2026-01-31'] }, filters }))

    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('2')
    await wrapper.get('[data-testid="report-filter-trigger"]').trigger('click')

    expect(wrapper.find('[data-testid="drawer-body"] [data-testid="report-range-created"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="drawer-body"] [data-testid="field-selector-trigger"]').exists()).toBe(true)
    expect((wrapper.get('[data-testid="advanced-filters-value-0"]').element as HTMLInputElement).value).toBe('Acme')
  })

  it('shows no count while nothing is in force, and Apply runs the report', async () => {
    stubPhone(true)
    const report = makeReport()
    const wrapper = mountPage(report)

    expect(wrapper.find('[data-testid="filter-trigger-count"]').exists()).toBe(false)
    await wrapper.get('[data-testid="report-filter-trigger"]').trigger('click')
    await wrapper.get('[data-testid="filter-apply"]').trigger('click')

    expect(report.generate).toHaveBeenCalledTimes(1)
  })
})
