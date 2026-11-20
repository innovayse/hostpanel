<script setup lang="ts">
/**
 * Monthly transactions report: money in, fees and money out per day of a month, as a line chart and
 * a table with totals, in the base currency the API converted them to.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
import { useReport } from '@/composables/useReport'
import { reportApiPath } from '@/lib/apiPaths'
import { formatMonthDay } from '@/lib/format'
import { baseCurrencyFormatter } from '@/lib/money'
import { currentMonthNumber, currentYear, monthName } from '@/lib/reportsPeriod'
import { reportPath } from '@/lib/reportsQuery'
import type { ReportChartData } from '@/types/reportchartdata'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportDailyTransactionRow, ReportDailyTransactions } from '@/types/reportdailytransactions'
import type { ReportTableData } from '@/types/reporttabledata'
import schema from '@/views/MonthlyTransactionsView.schema'

const { t, locale } = useI18n()

/** Year being reported; the current one at first. */
const year = ref(currentYear())

/** Month being reported, 1 to 12; the current one at first. */
const month = ref(currentMonthNumber())

const { data, load } = useReport<ReportDailyTransactions>(() =>
  reportPath(reportApiPath('daily-transactions'), { year: year.value, month: month.value }),
)

/** Title naming the month once a report is loaded; the schema title until then. */
const title = computed<string | undefined>(() =>
  data.value ? t('reports.titles.monthlyTransactionsFor', { month: monthName(data.value.month), year: data.value.year }) : undefined,
)

/** Base currency the figures are converted to; empty until loaded. */
const currency = computed<string>(() => data.value?.currency ?? '')

/** Formats an amount in the base currency the API converted the report to. */
const formatMoney = baseCurrencyFormatter(() => currency.value)

/** The days with their figures; none until the report is read. */
const rows = computed<ReportDailyTransactionRow[]>(() => data.value?.rows ?? [])

/** The table of the days with totals: every figure but the day is money, and the balance is what came in less fees and payouts. */
const table = computed<ReportTableData<ReportDailyTransactionRow>>(() => ({
  rows: rows.value,
  rowKey: 'date',
  extras: {
    date: { format: ReportCellFormat.Date },
    amountIn: { format: ReportCellFormat.Money },
    fees: { format: ReportCellFormat.Money },
    amountOut: { format: ReportCellFormat.Money },
    balance: { format: ReportCellFormat.Money },
  },
  totals: {
    amountIn: data.value?.totalAmountIn ?? 0,
    fees: data.value?.totalFees ?? 0,
    amountOut: data.value?.totalAmountOut ?? 0,
    balance: (data.value?.totalAmountIn ?? 0) - (data.value?.totalFees ?? 0) - (data.value?.totalAmountOut ?? 0),
  },
  formatMoney,
}))

/** The line chart: the short month and day of each row against money in, fees and money out. */
const chart = computed<ReportChartData>(() => ({
  labels: rows.value.map(row => formatMonthDay(row.date, locale.value)),
  series: [
    { label: t('shared.amountIn'), data: rows.value.map(row => row.amountIn) },
    { label: t('shared.fees'), data: rows.value.map(row => row.fees) },
    { label: t('shared.amountOut'), data: rows.value.map(row => row.amountOut) },
  ],
  valueFormatter: formatMoney,
}))
</script>

<template>
  <ReportPage
    v-model:year="year"
    v-model:month="month"
    :retry="load"
    :schema="schema"
    :title="title"
    :years-back="2"
    :years-ahead="2"
    :chart="chart"
    :table="table"
    :base-currency="currency"
    @generate="load"
  />
</template>
