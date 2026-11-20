<script setup lang="ts">
/**
 * Income forecast report: income booked per month of the year with its trailing quarter, half year
 * and year to date, as a line chart of the running total and a table.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
import { useReport } from '@/composables/useReport'
import { reportApiPath } from '@/lib/apiPaths'
import { baseCurrencyFormatter } from '@/lib/money'
import type { ReportChartData } from '@/types/reportchartdata'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportForecastRow } from '@/types/reportforecast'
import type { ReportTableData } from '@/types/reporttabledata'
import schema from '@/views/IncomeForecastView.schema'

const { t } = useI18n()

const { data, load } = useReport<ReportForecastRow[]>(() => reportApiPath('income-forecast'))

/** Months with their income; empty until loaded. */
const rows = computed<ReportForecastRow[]>(() => data.value ?? [])

/** Base currency the amounts are converted to; empty until loaded. */
const currency = computed<string>(() => rows.value[0]?.currency ?? '')

/** Formats an amount in the base currency the API converted the report to. */
const formatMoney = baseCurrencyFormatter(() => currency.value)

/** The line chart of the running total from January. */
const chart = computed<ReportChartData>(() => ({
  labels: rows.value.map(row => row.month),
  series: [{ label: t('reports.incomeForecast.cumulativeForecast'), data: rows.value.map(row => row.total) }],
  valueFormatter: formatMoney,
}))

/** The table of the months; every figure but the month is money, with the explanation of its window. */
const table = computed<ReportTableData<ReportForecastRow>>(() => ({
  rows: rows.value,
  rowKey: 'month',
  extras: {
    monthly: { format: ReportCellFormat.Money, hint: t('reports.incomeForecast.hints.monthly') },
    quarterly: { format: ReportCellFormat.Money, hint: t('reports.incomeForecast.hints.quarterly') },
    semiAnnual: { format: ReportCellFormat.Money, hint: t('reports.incomeForecast.hints.semiAnnual') },
    annual: { format: ReportCellFormat.Money, hint: t('reports.incomeForecast.hints.annual') },
    total: { format: ReportCellFormat.Money, hint: t('reports.incomeForecast.hints.total') },
  },
  formatMoney,
}))
</script>

<template>
  <ReportPage :retry="load" :schema="schema" :chart="chart" :table="table" :base-currency="currency" />
</template>
