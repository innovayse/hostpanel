/**
 * Tests for ReportTable: headers from the columns, cell formats, the dash for a missing value,
 * cell and body slots, the totals row and the empty text.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { ref } from 'vue'
import ReportTable from '@/components/reports/ReportTable.vue'
import { REPORT_LOADING } from '@/lib/reportLoading'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportColumn } from '@/types/reportcolumn'
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
const mountTable = (props: Record<string, unknown> = {}, slots: Record<string, string> = {}) =>
  mount(ReportTable, { props: { columns, rows, rowKey: ID_KEY, ...props }, slots, global: { plugins: [createTestI18n()] } })

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

  it('shows the empty text, and no totals, when there are no rows', () => {
    const wrapper = mountTable({ rows: [], emptyText: 'Nothing here', totals: { amount: 1 } })

    expect(wrapper.text()).toContain('Nothing here')
    expect(wrapper.find('[data-testid="report-totals"]').exists()).toBe(false)
  })

  it('shows the default empty text when none is given', () => {
    expect(mountTable({ rows: [] }).text()).toContain('No data.')
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
})
