/**
 * Tests for ReportPage: title and description, the filters slot, the loading state that keeps the layout, the
 * refusal sentence from the store with its Retry button, and the "generated on" line after the
 * content; and the parts it draws from the schema: the header buttons, the filters, the tiles, the chart, the table with
 * its pager, the placeholders of the parts the data fills in, the note and the empty sentence.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent, h, inject } from 'vue'
import type { Component, DefineComponent } from 'vue'
import { createPinia, setActivePinia } from 'pinia'
import ReportPage from '@/components/reports/ReportPage.vue'
import ReportChartCard from '@/components/reports/ReportChartCard.vue'
import ReportStatRow from '@/components/reports/ReportStatRow.vue'
import ReportTable from '@/components/reports/ReportTable.vue'
import { NOT_LOADING, REPORT_LOADING } from '@/lib/reportLoading'
import { useReportsStore } from '@/stores/reports'
import { ChartKind } from '@/utils/ui/chart'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'
import { ReportAction, ReportNote } from '@/utils/ui/reportParts'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { createTestI18n } from '../../helpers/i18n'

/** The page as a plain component: it is generic in its row type, so a loose props bag is not typed against it. */
const PlainReportPage = ReportPage as unknown as DefineComponent

/** The table as a plain component: `h` cannot take a generic one, whose slot types depend on its row type. */
const PlainReportTable = ReportTable as unknown as Component

/** What a screen puts in a slot of the page: markup, or a function of the slot's scope. */
type SlotFill = string | ((scope: { columns: unknown[] }) => unknown)

/** The schema the page reads its header from: the invoices report's title and description. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  header: { title: 'shared.invoices', subtitle: 'reports.invoicesReport.thisReportCanBeUsed' },
}

/**
 * Mounts the page with a filter and a body.
 *
 * @param retry - The screen's reload, when it offers one.
 * @returns The wrapper.
 */
const mountPage = (retry?: () => unknown) =>
  mount(ReportPage, {
    props: { schema, retry },
    slots: { actions: '<p data-testid="actions-slot">actions</p>', filters: '<p data-testid="filter-slot">filters</p>', default: '<p data-testid="body-slot">body</p>' },
    global: { plugins: [createTestI18n(), createPinia()] },
  })

describe('ReportPage', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('takes the title of its schema unless the screen names the period in its own', () => {
    const named = mount(ReportPage, { props: { schema, title: 'Invoices for 2026' }, global: { plugins: [createTestI18n(), createPinia()] } })

    expect(named.get('h1').text()).toBe('Invoices for 2026')
    expect(mountPage().get('h1').text()).toBe('Invoices')
  })

  it('shows the title, description, filters, body and the generated-on line', () => {
    const wrapper = mountPage()

    expect(wrapper.text()).toContain('Invoices')
    expect(wrapper.text()).toContain('generate a custom export of invoices')
    expect(wrapper.find('[data-testid="actions-slot"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="filter-slot"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="body-slot"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="report-timestamp"]').exists()).toBe(true)
  })

  it('keeps the header, filters and body while the store loads, marks it busy and hides the generated-on line', async () => {
    const wrapper = mountPage()
    useReportsStore().loading = true
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="filter-slot"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="body-slot"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="report-body"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.find('[data-testid="report-timestamp"]').exists()).toBe(false)
  })

  it('tells the parts in the body that the report is loading', async () => {
    const probe = defineComponent({
      setup: () => {
        const loading = inject(REPORT_LOADING, NOT_LOADING)
        return () => h('p', { 'data-testid': 'probe' }, String(loading.value))
      },
    })
    const wrapper = mount(ReportPage, {
      props: { schema },
      slots: { default: () => h(probe) },
      global: { plugins: [createTestI18n(), createPinia()] },
    })
    useReportsStore().loading = true
    await wrapper.vm.$nextTick()

    expect(wrapper.get('[data-testid="probe"]').text()).toBe('true')
  })

  it('shows the sentence the store holds when a load was refused', async () => {
    const wrapper = mountPage()
    useReportsStore().error = 'No access.'
    await wrapper.vm.$nextTick()

    expect(wrapper.get('[data-testid="report-error"]').text()).toBe('No access.')
    expect(wrapper.find('[data-testid="body-slot"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="report-error-retry"]').exists()).toBe(false)
  })

  it('offers Retry on a refused load when the screen can reload', async () => {
    const retry = vi.fn()
    const wrapper = mountPage(retry)
    useReportsStore().error = 'No access.'
    await wrapper.vm.$nextTick()

    expect(wrapper.get('[data-testid="report-error"]').text()).toBe('No access.')
    await wrapper.get('[data-testid="report-error-retry"]').trigger('click')

    expect(retry).toHaveBeenCalledTimes(1)
  })

  describe('with no data', () => {
    /** Columns of the table in the empty-state tests. */
    const columns = [{ key: 'name', label: 'Name' }]

    /**
     * Mounts a page holding stats, a chart and a table.
     *
     * @param chartData - Values of the chart.
     * @param rows - Rows of the table.
     * @returns The wrapper.
     */
    const mountReport = (chartData: number[], rows: { name: string }[]) =>
      mount(ReportPage, {
        props: { schema, emptyText: 'No invoices.' },
        slots: {
          default: () => [
            h(ReportStatRow, { stats: [{ key: 'total', label: 'Total', value: '0' }] }),
            h(ReportChartCard, { chart: { kind: ChartKind.GroupedBar }, labels: chartData.map((_, index) => String(index)), series: [{ label: 'Sum', data: chartData }] }),
            h(PlainReportTable, { columns, rows, rowKey: 'name' }),
          ],
        },
        global: { plugins: [createTestI18n(), createPinia()], stubs: { UiChart: true } },
      })

    it('shows one compact empty state, keeps the stats and draws no empty chart card or table', async () => {
      const wrapper = mountReport([], [])
      await wrapper.vm.$nextTick()

      expect(wrapper.findAll('[data-testid="report-empty"]')).toHaveLength(1)
      expect(wrapper.get('[data-testid="report-empty"]').text()).toContain('No invoices.')
      expect(wrapper.find('[data-testid="report-stat-total"]').exists()).toBe(true)
      expect(wrapper.find('[data-testid="report-chart"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="report-chart-empty"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="report-table"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="report-table-empty"]').exists()).toBe(false)
    })

    it('shows no page-level state while one part has data, and the empty part stands in its own place', async () => {
      const wrapper = mountReport([], [{ name: 'Acme' }])
      await wrapper.vm.$nextTick()

      expect(wrapper.find('[data-testid="report-empty"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="report-chart-empty"]').exists()).toBe(true)
      expect(wrapper.find('[data-testid="report-table"]').exists()).toBe(true)
    })

    it('shows no empty state while the report loads', async () => {
      const wrapper = mountReport([], [])
      useReportsStore().loading = true
      await wrapper.vm.$nextTick()

      expect(wrapper.find('[data-testid="report-empty"]').exists()).toBe(false)
    })

    it('shows the generic sentence when the screen gives none', async () => {
      const wrapper = mount(ReportPage, {
        props: { schema },
        slots: { default: () => h(PlainReportTable, { columns, rows: [], rowKey: 'name' }) },
        global: { plugins: [createTestI18n(), createPinia()] },
      })
      await wrapper.vm.$nextTick()

      expect(wrapper.get('[data-testid="report-empty"]').text()).toContain('No data for this report.')
    })
  })

  describe('drawn from the schema', () => {
    /** A schema with every part. */
    const full: ReportSkeletonSchema = {
      kind: SkeletonKind.Report,
      actions: [ReportAction.Refresh, ReportAction.Export, ReportAction.Print],
      header: { title: 'shared.invoices' },
      filters: [{ key: 'year', label: 'shared.year' }, { key: 'dateRange', label: 'shared.dateRange' }],
      stats: [{ key: 'total', label: 'shared.total' }],
      map: true,
      alert: true,
      summary: true,
      chart: { kind: ChartKind.GroupedBar },
      table: {
        columns: [
          { key: 'name', label: 'shared.name', size: UiColumnSize.Grow },
          { key: 'amount', label: 'shared.amount', align: UiAlign.Right },
        ],
        pagination: true,
      },
      closing: true,
      note: ReportNote.Converted,
      emptyText: 'shared.noDataFoundForThis',
    }

    /** One row of the table. */
    interface Item {
      /** Name of the row. */
      name: string
      /** Its amount. */
      amount: number
    }

    /** The one row the full report holds. */
    const items: Item[] = [{ name: 'Acme', amount: 3 }]

    /**
     * Mounts the page with the data of every part.
     *
     * @param props - Props on top of the schema and the data.
     * @param slots - Slots to fill.
     * @returns The wrapper.
     */
    const mountFull = (props: Record<string, unknown> = {}, slots: Record<string, SlotFill> = {}) =>
      mount(PlainReportPage, {
        props: {
          schema: full,
          stats: { total: { value: '$10' } },
          chart: { labels: ['Jan'], series: [{ label: 'Sum', data: [3] }] },
          table: { rows: items, rowKey: 'name', pagination: { page: 1, total: 120, pageSize: 50, testid: 'report-pager' } },
          baseCurrency: 'USD',
          ...props,
        },
        slots,
        global: { plugins: [createTestI18n(), createPinia()], stubs: { UiChart: true } },
      })

    it('draws the buttons of the schema and tells the screen when each is pressed', async () => {
      const retry = vi.fn()
      const wrapper = mountFull({ retry })

      expect(wrapper.findAll('[data-testid="report-refresh"], [data-testid="report-export"], [data-testid="report-print"]')).toHaveLength(3)
      await wrapper.get('[data-testid="report-refresh"]').trigger('click')
      await wrapper.get('[data-testid="report-export"]').trigger('click')
      await wrapper.get('[data-testid="report-print"]').trigger('click')

      expect(retry).toHaveBeenCalledTimes(1)
      expect(wrapper.emitted('export')).toHaveLength(1)
      expect(wrapper.emitted('print')).toHaveLength(1)
    })

    it('draws no button for a schema with none', () => {
      expect(mountPage().find('[data-testid="report-refresh"]').exists()).toBe(false)
    })

    it('draws the filters with the controls of the screen and the dropdown it knows, and asks for the report from Generate', async () => {
      const wrapper = mountFull({}, { 'field-dateRange': '<span data-testid="range">range</span>' })

      expect(wrapper.get('[data-testid="report-filters"]').text()).toContain('Year')
      expect(wrapper.get('[data-testid="report-filters"]').text()).toContain('Date Range')
      expect(wrapper.find('[data-testid="report-year"]').exists()).toBe(true)
      expect(wrapper.get('[data-testid="range"]').text()).toBe('range')
      await wrapper.get('[data-testid="report-generate"]').trigger('click')

      expect(wrapper.emitted('generate')).toHaveLength(1)
    })

    it('lets the filters slot replace the filter card of the schema', () => {
      const wrapper = mountFull({}, { filters: '<p data-testid="own-filters">own</p>' })

      expect(wrapper.find('[data-testid="own-filters"]').exists()).toBe(true)
      expect(wrapper.find('[data-testid="report-filters"]').exists()).toBe(false)
    })

    it('draws the tiles, the chart, the table and its pager, and the note with the base currency', async () => {
      const wrapper = mountFull()
      await wrapper.vm.$nextTick()

      expect(wrapper.get('[data-testid="report-stat-total"]').text()).toContain('$10')
      expect(wrapper.find('[data-testid="report-chart"]').exists()).toBe(true)
      expect(wrapper.findAll('th').map(header => header.text())).toEqual(['Name', 'Amount'])
      expect(wrapper.findAll('th')[0]?.classes()).toEqual(expect.arrayContaining(['w-[28%]', 'max-w-0']))
      expect(wrapper.get('[data-testid="report-table"]').text()).toContain('Acme')
      expect(wrapper.find('[data-testid="report-pager-next"]').exists()).toBe(true)
      expect(wrapper.get('[data-testid="report-note"]').text()).toContain('USD')
    })

    it('passes the page chosen in the pager on to the screen', async () => {
      const wrapper = mountFull()

      await wrapper.get('[data-testid="report-pager-next"]').trigger('click')

      expect(wrapper.emitted('page')).toEqual([[2]])
    })

    it('draws a table cell from the slot of its column and the rows of the body slot over the columns', () => {
      const cell = mountFull({}, { 'cell-name': '<b data-testid="own-cell">own</b>' })
      const body = mountFull({}, { body: (scope: { columns: unknown[] }) => h('tr', { 'data-testid': 'own-body' }, String(scope.columns.length)) })

      expect(cell.get('[data-testid="own-cell"]').text()).toBe('own')
      expect(body.get('[data-testid="own-body"]').text()).toBe('2')
    })

    it('draws no note until the base currency is known', () => {
      expect(mountFull({ baseCurrency: '' }).find('[data-testid="report-note"]').exists()).toBe(false)
    })

    it('words the note by the kind the schema picks', () => {
      const converted = mountFull().get('[data-testid="report-note"]').text()
      const recurring = mountFull({ schema: { ...full, note: ReportNote.Recurring } }).get('[data-testid="report-note"]').text()

      expect(recurring).toContain('USD')
      expect(recurring).not.toBe(converted)
    })

    it('draws the parts the data fills in from their slots, and a placeholder of each while the report loads', async () => {
      const ids = ['own-map', 'own-alert', 'own-summary', 'own-closing']
      const wrapper = mountFull({}, {
        map: '<i data-testid="own-map" />',
        alert: '<i data-testid="own-alert" />',
        summary: '<i data-testid="own-summary" />',
        closing: '<i data-testid="own-closing" />',
      })

      expect(ids.every(id => wrapper.find(`[data-testid="${id}"]`).exists())).toBe(true)
      expect(wrapper.find('[data-testid="ui-skeleton-map"]').exists()).toBe(false)

      useReportsStore().loading = true
      await wrapper.vm.$nextTick()

      expect(ids.some(id => wrapper.find(`[data-testid="${id}"]`).exists())).toBe(false)
      expect(wrapper.find('[data-testid="ui-skeleton-map"]').exists()).toBe(true)
      expect(wrapper.find('[data-testid="ui-skeleton-alert"]').exists()).toBe(true)
      expect(wrapper.findAll('[data-testid="ui-skeleton-lines"]')).toHaveLength(2)
      expect(wrapper.find('[data-testid="ui-skeleton-note"]').exists()).toBe(true)
    })

    it('words the empty state with the sentence of the schema, unless the screen words it itself', async () => {
      const empty = { chart: { labels: [], series: [] }, table: { rows: [], rowKey: 'name' } }
      const fromSchema = mountFull(empty)
      const fromScreen = mountFull({ ...empty, emptyText: 'Choose a client.' })
      await fromSchema.vm.$nextTick()
      await fromScreen.vm.$nextTick()

      expect(fromSchema.get('[data-testid="report-empty"]').text()).toContain(createTestI18n().global.t('shared.noDataFoundForThis'))
      expect(fromScreen.get('[data-testid="report-empty"]').text()).toContain('Choose a client.')
    })
  })
})
