<script setup lang="ts">
/**
 * Daily performance report: orders, invoices, tickets and cancellation requests per day over a
 * date range, as a line chart and a table.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportChartCard from '@/components/reports/ReportChartCard.vue'
import ReportField from '@/components/reports/ReportField.vue'
import ReportFilterBar from '@/components/reports/ReportFilterBar.vue'
import ReportPage from '@/components/reports/ReportPage.vue'
import ReportStatRow from '@/components/reports/ReportStatRow.vue'
import ReportTable from '@/components/reports/ReportTable.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import { useReport } from '@/composables/useReport'
import { reportPath } from '@/lib/reportsQuery'
import type { ReportColumn } from '@/types/reportcolumn'
import type { ReportStat } from '@/types/reportstat'
import type { ReportDailyPerformanceRow } from '@/types/reportdailyperformance'
import { UiChartType } from '@/utils/ui/chart'
import type { UiChartSeries } from '@/utils/ui/chart'
import { UiAlign } from '@/utils/ui/table'
import { AppRoute } from '@/lib/routes'

const { t } = useI18n()

/** The days to report; none means the endpoint default. */
const dateRange = ref<[string, string] | null>(null)

const { data, load } = useReport<ReportDailyPerformanceRow[]>(() =>
  reportPath(AppRoute.ReportsDailyPerformance, { from: dateRange.value?.[0], to: dateRange.value?.[1] }),
)

/** Days with their counters; empty until loaded. */
const rows = computed<ReportDailyPerformanceRow[]>(() => data.value ?? [])

/** Counters in table and chart order, with the caption of each. */
const metrics = computed<{ key: keyof ReportDailyPerformanceRow; label: string }[]>(() => [
  { key: 'completedOrders', label: t('reports.dailyPerformance.completedOrders') },
  { key: 'newInvoices', label: t('reports.dailyPerformance.newInvoices') },
  { key: 'paidInvoices', label: t('reports.dailyPerformance.paidInvoices') },
  { key: 'openedTickets', label: t('reports.dailyPerformance.openedTickets') },
  { key: 'ticketReplies', label: t('reports.dailyPerformance.ticketReplies') },
  { key: 'cancellationRequests', label: t('reports.dailyPerformance.cancellationRequests') },
])

/** Counters shown as tiles, in order. */
const STAT_KEYS: readonly (keyof ReportDailyPerformanceRow)[] = ['completedOrders', 'paidInvoices', 'openedTickets', 'cancellationRequests']

/** Headline figures: each tile counter summed over the days. */
const stats = computed<ReportStat[]>(() =>
  metrics.value
    .filter(metric => STAT_KEYS.includes(metric.key))
    .map(metric => ({ key: metric.key, label: metric.label, value: rows.value.reduce((sum, row) => sum + Number(row[metric.key]), 0) })),
)

/** Table columns: the day, then one per counter. */
const columns = computed<ReportColumn[]>(() => [
  { key: 'date', label: t('shared.date') },
  ...metrics.value.map(metric => ({ key: metric.key, label: metric.label, align: UiAlign.Right })),
])

/** Chart labels. */
const labels = computed<string[]>(() => rows.value.map(row => row.date))

/** One series per counter. */
const series = computed<UiChartSeries[]>(() =>
  metrics.value.map(metric => ({ label: metric.label, data: rows.value.map(row => Number(row[metric.key])) })),
)

/** Forgets the range and reloads with the default. */
const clear = async (): Promise<void> => {
  dateRange.value = null
  await load()
}
</script>

<template>
  <ReportPage :retry="load" :title="t('reports.dailyPerformance.dailyPerformance')" :description="t('reports.dailyPerformance.overviewOfDailyActivityMetrics')">
    <template #filters>
      <ReportFilterBar :generate-label="t('shared.apply')" clearable @generate="load" @clear="clear">
        <template #fields>
          <ReportField :label="t('shared.dateRange')" width="grow">
            <UiDateRangePicker v-model="dateRange" :placeholder="t('shared.selectDateRange')" />
          </ReportField>
        </template>
      </ReportFilterBar>
    </template>
    <ReportStatRow :stats="stats" />
    <ReportChartCard :type="UiChartType.Line" :labels="labels" :series="series" height="lg" />
    <ReportTable :columns="columns" :rows="rows" row-key="date" :empty-text="t('reports.dailyPerformance.noDataFound')" />
  </ReportPage>
</template>
