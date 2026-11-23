/**
 * Tests for ReportTable: headers from the columns, cell formats, the dash for a missing value,
 * cell and body slots, the totals row, the compact empty state with the sentence of its report, the width of a column and the
 * placeholder of a group heading row.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { computed, ref } from 'vue'
import ReportTable from '@/components/reports/ReportTable.vue'
import { REPORT_EMPTY } from '@/lib/reportEmpty'
import { REPORT_LOADING } from '@/lib/reportLoading'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportColumn } from '@/types/reportcolumn'
import { UiColumnSize } from '@/utils/ui/table'
import { createTestI18n } from '../../helpers/i18n'

/** A row of the test table. */
interface Row {
  /** Row id. */
  id: number
  /** Amount in USD. */
  amount: number | null
  /** Rate in percent. */
  rate: number
  /** Period in years. */
  years: number
  /** Status word. */
  status: string
  /** Free text. */
  note: string | null
}

/**
 * The `rowKey` that names the `id` property. Mounting a generic component types its row as `object`,
 * whose property names are `never`, so the name is typed by hand here.
 */
const ID_KEY = 'id' as never

/** One column per cell format. */
const columns: ReportColumn[] = [
  { key: 'id', label: 'ID', format: ReportCellFormat.Id },
  { key: 'amount', label: 'Amount', format: ReportCellFormat.Money },
  { key: 'rate', label: 'Rate', format: ReportCellFormat.Percent },
  { key: 'years', label: 'Period', format: ReportCellFormat.Years },
  { key: 'status', label: 'Status', format: ReportCellFormat.Status },
  { key: 'note', label: 'Note' },
]

/** Two rows, the second with missing values. */
const rows: Row[] = [
  { id: 4, amount: 12.5, rate: 19, years: 2, status: 'Active', note: 'hello' },
  { id: 5, amount: null, rate: 0, years: 1, status: 'Overdue', note: null },
]

/**
 * Mounts the table.
 *
 * @param props - Props on top of the columns.
 * @param slots - Slots to fill.
 * @returns The wrapper.
 */
const mountTable = (props: Record<string, unknown> = {}, slots: Record<string, string> = {}, provide: Record<symbol, unknown> = {}) =>
  mount(ReportTable, { props: { columns, rows, rowKey: ID_KEY, ...props }, slots, global: { plugins: [createTestI18n()], provide } })

/**
 * A page scope that says the whole report is empty, as `ReportPage` does once every part is.
 *
 * @returns The `provide` map for the mount.
 */
const collapsedScope = (): Record<symbol, unknown> => ({
  [REPORT_EMPTY as symbol]: { set: () => undefined, remove: () => undefined, collapsed: computed(() => true), text: ref(undefined) },
})

/**
 * A page scope that is not collapsed and carries the sentence of its report's empty state.
 *
 * @param text - The sentence.
 * @returns The `provide` map for the mount.
 */
const sentenceScope = (text: string): Record<symbol, unknown> => ({
  [REPORT_EMPTY as symbol]: { set: () => undefined, remove: () => undefined, collapsed: computed(() => false), text: ref(text) },
})

describe('ReportTable', () => {
  it('draws one header per column and one row per item', () => {
    const wrapper = mountTable()

    expect(wrapper.findAll('th').map(header => header.text())).toEqual(['ID', 'Amount', 'Rate', 'Period', 'Status', 'Note'])
    expect(wrapper.findAll('[data-testid="report-table-row"]')).toHaveLength(2)
  })

  it('keeps the real columns and draws skeleton rows instead of the data while the report loads', () => {
    const wrapper = mount(ReportTable, {
      props: { columns, rows, rowKey: ID_KEY, totals: { amount: 5 } },
      global: { plugins: [createTestI18n()], provide: { [REPORT_LOADING as symbol]: ref(true) } },
    })

    expect(wrapper.findAll('th').map(header => header.text())).toEqual(['ID', 'Amount', 'Rate', 'Period', 'Status', 'Note'])
    expect(wrapper.find('[data-testid="ui-table-skeleton"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="report-table-row"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="report-totals"]').exists()).toBe(false)
  })

  it('formats each cell by its column format', () => {
    const cells = mountTable().findAll('[data-testid="report-table-row"]')[0]?.findAll('td').map(cell => cell.text())

    expect(cells).toEqual(['#4', '$12.50', '19%', '2 yr', 'Active', 'hello'])
  })

  it('shows a dash for a missing value', () => {
    const cells = mountTable().findAll('[data-testid="report-table-row"]')[1]?.findAll('td').map(cell => cell.text())

    expect(cells?.[1]).toBe('—')
    expect(cells?.[5]).toBe('—')
  })

  it('draws a status as a badge', () => {
    expect(mountTable().find('[data-testid="report-status"]').text()).toBe('Active')
  })

  it('formats money with the function it is given', () => {
    const cells = mountTable({ formatMoney: (amount: number) => `${amount} EUR` }).findAll('td').map(cell => cell.text())

    expect(cells).toContain('12.5 EUR')
  })

  it('writes a money cell in the row currency when the column names one', () => {
    const wrapper = mount(ReportTable, {
      props: {
        columns: [{ key: 'amount', label: 'Amount', format: ReportCellFormat.Money, currencyKey: 'currency' }],
        rows: [{ amount: 12.5, currency: 'EUR' }],
        formatMoney: (amount: number) => `${amount} ignored`,
        rowKey: () => 'only',
      },
      global: { plugins: [createTestI18n()] },
    })

    expect(wrapper.get('td').text()).toBe('€12.50')
  })

  it('lets a screen fill the cell of one column', () => {
    const wrapper = mountTable({}, { 'cell-note': '<template #cell-note="{ row }">[{{ row.id }}]</template>' })

    expect(wrapper.findAll('td').map(cell => cell.text())).toContain('[4]')
  })

  it('replaces the rows with the body slot', () => {
    const wrapper = mountTable({}, { body: '<tr><td data-testid="custom-body">custom</td></tr>' })

    expect(wrapper.find('[data-testid="custom-body"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="report-table-row"]').exists()).toBe(false)
  })

  it('adds a totals row with the caption in the first column', () => {
    const wrapper = mountTable({ totals: { amount: 12.5 } })
    const cells = wrapper.get('[data-testid="report-totals"]').findAll('td').map(cell => cell.text())

    expect(cells[0]).toBe('Total')
    expect(cells[1]).toBe('$12.50')
    expect(cells[2]).toBe('')
  })

  it('shows the compact empty state, and no header, totals or table, when there are no rows', () => {
    const wrapper = mountTable({ rows: [], emptyText: 'Nothing here', totals: { amount: 1 } })

    expect(wrapper.get('[data-testid="report-table-empty"]').text()).toContain('Nothing here')
    expect(wrapper.text()).toContain('Try another period or change the filters.')
    expect(wrapper.find('[data-testid="report-table"]').exists()).toBe(false)
    expect(wrapper.find('th').exists()).toBe(false)
    expect(wrapper.find('[data-testid="report-totals"]').exists()).toBe(false)
  })

  it('shows the sentence of its report when the table has none of its own, and its own when it has one', () => {
    expect(mountTable({ rows: [] }, {}, sentenceScope('No invoices this month.')).get('[data-testid="report-table-empty"]').text()).toContain('No invoices this month.')

    const own = mountTable({ rows: [], emptyText: 'No accounts here.' }, {}, sentenceScope('No invoices this month.'))
    expect(own.get('[data-testid="report-table-empty"]').text()).toContain('No accounts here.')
  })

  it('gives a column the width its size names', () => {
    const wrapper = mountTable({ columns: [{ key: 'id', label: 'ID', size: UiColumnSize.Compact }, { key: 'note', label: 'Note', size: UiColumnSize.Grow }] })
    const [compact, grow] = wrapper.findAll('th')

    expect(compact?.classes()).toContain('w-px')
    expect(grow?.classes()).toContain('w-full')
  })

  it('opens the loading placeholder with a group heading row only for a grouped table', () => {
    /**
     * Mounts the table while its report loads.
     *
     * @param grouped - Whether the table has group heading rows.
     * @returns The mounted wrapper.
     */
    const mountLoading = (grouped: boolean) =>
      mount(ReportTable, {
        props: { columns, rows, rowKey: ID_KEY, grouped },
        global: { plugins: [createTestI18n()], provide: { [REPORT_LOADING as symbol]: ref(true) } },
      })

    expect(mountLoading(true).find('[data-testid="ui-table-skeleton-group"]').exists()).toBe(true)
    expect(mountLoading(false).find('[data-testid="ui-table-skeleton-group"]').exists()).toBe(false)
  })

  it('shows the default empty sentence when none is given', () => {
    expect(mountTable({ rows: [] }).text()).toContain('No data for this report.')
  })

  it('draws nothing when the page shows its one empty state for the whole report', () => {
    const wrapper = mountTable({ rows: [] }, {}, collapsedScope())

    expect(wrapper.find('[data-testid="report-table-empty"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="report-table"]').exists()).toBe(false)
  })

  it('reads a column named by a dot path from a nested record', () => {
    const nested = [{ dates: { paid: '2026-10-05' } }]
    const wrapper = mount(ReportTable, {
      props: { columns: [{ key: 'dates.paid', label: 'Paid' }], rows: nested, rowKey: () => 'only' },
      global: { plugins: [createTestI18n()] },
    })

    expect(wrapper.get('td').text()).toBe('2026-10-05')
  })

  it('keys rows by a key built from the row and fails loudly without a rowKey', () => {
    const keyed = mountTable({ rowKey: (row: Row) => `row-${row.id}-${row.status}` })

    expect(keyed.findAll('[data-testid="report-table-row"]')).toHaveLength(2)
    expect(() => mountTable({ rowKey: undefined })).toThrow('rowKey is required')
  })

  it('draws only the ask to choose fields, with no header and no row bars, when there is no column', () => {
    const wrapper = mountTable({ columns: [] })

    expect(wrapper.find('th').exists()).toBe(false)
    expect(wrapper.find('[data-testid="report-table-row"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Choose the fields to include to see the results.')
    expect(wrapper.text()).not.toContain('Try another period')
  })

  it('shows the hint of a column as the tooltip of its caption', () => {
    const wrapper = mountTable({ columns: [{ key: 'id', label: 'ID', hint: 'What it counts.' }] })

    expect(wrapper.get('th span[title]').attributes('title')).toBe('What it counts.')
  })

  it('draws the pager of a paged report in the card footer and tells the page chosen', async () => {
    const wrapper = mountTable({ pagination: { page: 1, total: 120, pageSize: 50, testid: 'rt-pager' } })

    await wrapper.get('[data-testid="rt-pager-next"]').trigger('click')

    expect(wrapper.get('[data-testid="ui-card-footer"]').find('[data-testid="rt-pager-next"]').exists()).toBe(true)
    expect(wrapper.emitted('page')).toEqual([[2]])
  })

  it('draws no footer for a report that is not paged', () => {
    expect(mountTable().find('[data-testid="ui-card-footer"]').exists()).toBe(false)
  })
})
