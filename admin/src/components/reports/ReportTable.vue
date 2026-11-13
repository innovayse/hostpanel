<script setup lang="ts" generic="T extends object">
/**
 * Table every report draws its rows in.
 *
 * Columns are data (`ReportColumn`): the key reads a property of the row, the format turns the raw
 * value into text (money, percent, date, status badge and so on), and an empty value shows a dash. A
 * screen that needs a cell the formats cannot express fills the `cell-<key>` slot; one that needs
 * grouped rows replaces the whole body with the `body` slot. `totals` adds a footer row, and the
 * empty text shows when there are no rows. The card is glass; the table scrolls inside it past a
 * height cap with its header stuck, and sideways on a narrow screen.
 */
import { computed, inject } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportStatusBadge from '@/components/reports/ReportStatusBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableFoot from '@/components/ui/UiTableFoot.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { formatDate, EMPTY_VALUE } from '@/lib/format'
import { formatCurrencyCode, formatReportAmount } from '@/lib/money'
import { NOT_LOADING, REPORT_LOADING } from '@/lib/reportLoading'
import { valueAtPath } from '@/lib/valueAtPath'
import { ReportCellFormat } from '@/types/reportcolumn'
import { isNumericColumn } from '@/types/reportcolumn'
import type { ReportColumn } from '@/types/reportcolumn'
import { UiTableDensity } from '@/utils/ui/injection'

const { t } = useI18n()

/** Props for ReportTable. */
const props = withDefaults(defineProps<{
  /** Columns in display order. */
  columns: readonly ReportColumn[]
  /** Rows to draw. */
  rows?: T[]
  /** What identifies a row: the name of a unique property, or a function building a key from the row. Required whenever `rows` is passed. */
  rowKey?: (keyof T & string) | ((row: T) => string)
  /** Text shown when there are no rows; "No data." when omitted. */
  emptyText?: string
  /** Forces the empty state on or off; by default it follows `rows`. */
  empty?: boolean
  /** Footer figures by column key; the first column shows `totalsLabel`. */
  totals?: Record<string, string | number> | null
  /** Caption of the footer row; "Total" when omitted. */
  totalsLabel?: string
  /** Formats a money cell; US dollars when omitted. */
  formatMoney?: (amount: number) => string
  /** Prefix of the `data-testid`s (`<prefix>`, `<prefix>-row`). */
  testid?: string
}>(), {
  /**
   * Default for the rows.
   *
   * @returns A new empty list, so instances never share one.
   */
  rows: () => [],
  rowKey: undefined,
  emptyText: undefined,
  empty: undefined,
  totals: null,
  totalsLabel: undefined,
  /**
   * Default money formatter: US dollars.
   *
   * @param amount - Value to format.
   * @returns The formatted amount.
   */
  formatMoney: (amount: number) => formatReportAmount(amount),
  testid: 'report-table',
})

/** Slots a screen may fill. */
defineSlots<{
  /** Replaces the cell of one column; the slot name is `cell-` plus the column key. */
  [slot: `cell-${string}`]: (scope: { row: T; value: unknown }) => unknown
  /** Replaces all body rows (grouped reports). */
  body?: () => unknown
  /** Extra rows under the totals. */
  footer?: () => unknown
}>()

/** True while the report loads: the real columns stay and skeleton rows stand in for the data. */
const loading = inject(REPORT_LOADING, NOT_LOADING)

/** Whether the empty state shows. */
const isEmpty = computed<boolean>(() => props.empty ?? props.rows.length === 0)

/**
 * Reads the raw value a column shows for a row.
 *
 * @param row - The row.
 * @param key - Column key, a property name or a dot path into a nested record.
 * @returns The property of the row, whatever its type.
 */
const valueOf = (row: T, key: string): unknown => valueAtPath(row, key)

/**
 * Turns a raw value into the text of a cell.
 *
 * @param value - The raw value.
 * @param format - How the column formats it.
 * @returns The text; a dash when the value is missing.
 */
const formatValue = (value: unknown, format: ReportCellFormat | undefined): string => {
  if (value === null || value === undefined || value === '') {
    return EMPTY_VALUE
  }
  switch (format) {
    case ReportCellFormat.Money:
      return props.formatMoney(Number(value))
    case ReportCellFormat.Percent:
      return `${value}%`
    case ReportCellFormat.Date:
      return formatDate(String(value), EMPTY_VALUE)
    case ReportCellFormat.Id:
      return `#${value}`
    case ReportCellFormat.Years:
      return t('reports.common.years', { count: Number(value) })
    default:
      return String(value)
  }
}

/**
 * Text of one body cell: a money cell whose column names a `currencyKey` is written in the row's
 * own currency; every other cell goes through {@link formatValue}.
 *
 * @param row - The row.
 * @param column - The column.
 * @returns The cell text.
 */
const cellText = (row: T, column: ReportColumn): string => {
  const value = valueOf(row, column.key)
  const currency = column.currencyKey === undefined ? undefined : valueOf(row, column.currencyKey)
  if (column.format === ReportCellFormat.Money && typeof currency === 'string' && typeof value === 'number') {
    return formatCurrencyCode(value, currency)
  }
  return formatValue(value, column.format)
}

/**
 * Finds the key of a row for the list rendering.
 *
 * @param row - The row.
 * @returns The row property named by `rowKey`, or the key `rowKey` builds from the row.
 */
const keyOf = (row: T): string => {
  const rowKey = props.rowKey
  if (rowKey === undefined) {
    throw new Error('ReportTable: rowKey is required when rows are passed.')
  }
  return typeof rowKey === 'function' ? rowKey(row) : String(row[rowKey])
}

/**
 * Text of one footer cell.
 *
 * @param column - The column.
 * @param position - Index of the column; the first shows the footer caption.
 * @returns The caption, the formatted total, or nothing.
 */
const totalText = (column: ReportColumn, position: number): string => {
  if (position === 0) {
    return props.totalsLabel ?? t('shared.total')
  }
  const value = props.totals?.[column.key]
  return value === undefined || value === '' ? '' : formatValue(value, column.format)
}
</script>

<template>
  <UiCard class="overflow-hidden" :data-testid="testid">
    <UiTable :density="UiTableDensity.Comfortable" :loading="loading" :empty="isEmpty && !loading">
      <UiTableHead>
        <UiTableRow>
          <UiTableHeader v-for="column in columns" :key="column.key" :align="column.align" :numeric="isNumericColumn(column)">
            {{ column.label }}
          </UiTableHeader>
        </UiTableRow>
      </UiTableHead>
      <UiTableBody divided>
        <slot name="body">
          <UiTableRow v-for="row in rows" :key="keyOf(row)" hoverable :data-testid="`${testid}-row`">
            <UiTableCell v-for="column in columns" :key="column.key" :align="column.align" :numeric="isNumericColumn(column)">
              <slot :name="`cell-${column.key}`" :row="row" :value="valueOf(row, column.key)">
                <ReportStatusBadge
                  v-if="column.format === ReportCellFormat.Status && valueOf(row, column.key)"
                  :status="String(valueOf(row, column.key))"
                />
                <template v-else>
                  {{ cellText(row, column) }}
                </template>
              </slot>
            </UiTableCell>
          </UiTableRow>
        </slot>
      </UiTableBody>
      <UiTableFoot v-if="totals && !isEmpty && !loading">
        <UiTableRow data-testid="report-totals">
          <UiTableCell v-for="(column, position) in columns" :key="column.key" :align="column.align" :numeric="isNumericColumn(column)" class="text-text-primary">
            {{ totalText(column, position) }}
          </UiTableCell>
        </UiTableRow>
      </UiTableFoot>
      <slot name="footer" />
      <template #empty>
        <UiEmptyState :title="emptyText ?? t('shared.noData')" />
      </template>
    </UiTable>
  </UiCard>
</template>
