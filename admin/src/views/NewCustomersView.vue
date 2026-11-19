<script setup lang="ts">
/**
 * New customers report: sign-ups, orders placed and orders completed per month of a year, as a
 * line chart and a table with totals. The title, the filter caption and the table columns are the ones of
 * its schema, which the skeleton draws too.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
import { useReport } from '@/composables/useReport'
import { reportPath } from '@/lib/reportsQuery'
import { currentYear } from '@/lib/reportsPeriod'
import type { ReportChartData } from '@/types/reportchartdata'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportNewCustomersRow } from '@/types/reportnewcustomers'
import type { ReportTableData } from '@/types/reporttabledata'
import schema from '@/views/NewCustomersView.schema'

const { t } = useI18n()

/** The year being reported. */
const year = ref(currentYear())

/** Years before the current one the year filter offers. */
const YEARS_BACK = 9

const { data, load } = useReport<ReportNewCustomersRow[]>(() => reportPath('/reports/new-customers', { year: year.value }))

// Picking another year reloads at once, as the dropdown has always done.
watch(year, load)

/** Months with their counters; empty until loaded. */
const rows = computed<ReportNewCustomersRow[]>(() => data.value ?? [])

/** The table of the months: what each counter means, the conversion rate as a percent, and the footer sums of the three counters. */
const table = computed<ReportTableData<ReportNewCustomersRow>>(() => ({
  rows: rows.value,
  rowKey: 'month',
  extras: {
    newSignups: { hint: t('reports.newCustomers.newSignupsHint') },
    ordersPlaced: { hint: t('reports.newCustomers.ordersPlacedHint') },
    ordersCompleted: { hint: t('reports.newCustomers.ordersCompletedHint') },
    conversionRate: { format: ReportCellFormat.Percent, hint: t('reports.newCustomers.conversionRateHint') },
  },
  totals: {
    newSignups: rows.value.reduce((sum, row) => sum + row.newSignups, 0),
    ordersPlaced: rows.value.reduce((sum, row) => sum + row.ordersPlaced, 0),
    ordersCompleted: rows.value.reduce((sum, row) => sum + row.ordersCompleted, 0),
  },
}))

/** The line chart: one series per counter. */
const chart = computed<ReportChartData>(() => ({
  labels: rows.value.map(row => row.month),
  series: [
    { label: t('reports.newCustomers.newSignups'), data: rows.value.map(row => row.newSignups) },
    { label: t('reports.newCustomers.ordersPlaced'), data: rows.value.map(row => row.ordersPlaced) },
    { label: t('reports.newCustomers.ordersCompleted'), data: rows.value.map(row => row.ordersCompleted) },
  ],
}))
</script>

<template>
  <ReportPage v-model:year="year" :retry="load" :schema="schema" :years-back="YEARS_BACK" :chart="chart" :table="table" @generate="load" />
</template>
