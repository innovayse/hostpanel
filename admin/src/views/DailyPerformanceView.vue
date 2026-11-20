<script setup lang="ts">
/**
 * Daily performance report: orders, invoices, tickets and cancellation requests per day over a
 * date range, as a line chart and a table.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import { useReport } from '@/composables/useReport'
import { reportApiPath } from '@/lib/apiPaths'
import { formatMonthDay } from '@/lib/format'
import { reportPath } from '@/lib/reportsQuery'
import { reportColumns } from '@/lib/reportsSchema'
import { valueAtPath } from '@/lib/valueAtPath'
import type { ReportChartData } from '@/types/reportchartdata'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportColumn } from '@/types/reportcolumn'
import type { ReportDailyPerformanceRow } from '@/types/reportdailyperformance'
import type { ReportStatValue } from '@/types/reportstat'
import type { ReportTableData } from '@/types/reporttabledata'
import { DateRangePreset, presetIsoRange } from '@/utils/ui/datePicker'
import { ReportFieldWidth } from '@/utils/ui/reportField'
import schema from '@/views/DailyPerformanceView.schema'

const { t, locale } = useI18n()

/**
 * The days a report opens on and returns to when cleared: the current month so far.
 *
 * @returns The first and last day as ISO days.
 */
const defaultRange = (): [string, string] => presetIsoRange(DateRangePreset.ThisMonth)

/** The days to report; the current month so far at first. */
const dateRange = ref<[string, string] | null>(defaultRange())

const { data, load } = useReport<ReportDailyPerformanceRow[]>(() =>
  reportPath(reportApiPath('daily-performance'), { from: dateRange.value?.[0], to: dateRange.value?.[1] }),
)

/** Key of the day column, the one column that is not a counter. */
const DATE_KEY = 'date'

/** Days with their counters; empty until loaded. */
const rows = computed<ReportDailyPerformanceRow[]>(() => data.value ?? [])

/** The date range takes the free width of the filter row. */
const WIDTHS = { dateRange: ReportFieldWidth.Grow } as const

/** The counter columns of the table, in table and chart order: every column but the day. */
const metrics = computed<ReportColumn[]>(() => reportColumns(schema.table, t).filter(column => column.key !== DATE_KEY))

/** Headline figures: each tile counter of the schema summed over the days. */
const stats = computed<Record<string, ReportStatValue>>(() =>
  Object.fromEntries(
    (schema.stats ?? []).map(stat => [
      stat.key,
      { value: rows.value.reduce((sum, row) => sum + Number(valueAtPath(row, stat.key)), 0), tooltip: t(`reports.dailyPerformance.tooltips.${stat.key}`) },
    ]),
  ),
)

/** The line chart: the short month and day of each row against one series per counter. */
const chart = computed<ReportChartData>(() => ({
  labels: rows.value.map(row => formatMonthDay(row.date, locale.value)),
  series: metrics.value.map(metric => ({ label: metric.label, data: rows.value.map(row => Number(valueAtPath(row, metric.key))) })),
}))

/** The table of the days, the day written as a date. */
const table = computed<ReportTableData<ReportDailyPerformanceRow>>(() => ({
  rows: rows.value,
  rowKey: 'date',
  extras: { [DATE_KEY]: { format: ReportCellFormat.Date } },
}))

/** Puts the range back to the default and reloads. */
const clear = async (): Promise<void> => {
  dateRange.value = defaultRange()
  await load()
}
</script>

<template>
  <ReportPage
    :retry="load"
    :schema="schema"
    :widths="WIDTHS"
    :generate-label="t('shared.apply')"
    clearable
    :stats="stats"
    :chart="chart"
    :table="table"
    @generate="load"
    @clear="clear"
  >
    <template #field-dateRange>
      <UiDateRangePicker v-model="dateRange" :placeholder="t('shared.selectDateRange')" />
    </template>
  </ReportPage>
</template>
