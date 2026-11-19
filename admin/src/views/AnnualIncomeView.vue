<script setup lang="ts">
/**
 * Annual income report: income per month of the chosen year, as a bar chart and a table.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
import { useReport } from '@/composables/useReport'
import { reportApiPath } from '@/lib/apiPaths'
import { EMPTY_VALUE } from '@/lib/format'
import { baseCurrencyFormatter } from '@/lib/money'
import { currentYear } from '@/lib/reportsPeriod'
import { reportPath } from '@/lib/reportsQuery'
import type { ReportChartData } from '@/types/reportchartdata'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportMonthIncome } from '@/types/reportmonthincome'
import type { ReportStatValue } from '@/types/reportstat'
import type { ReportTableData } from '@/types/reporttabledata'
import schema from '@/views/AnnualIncomeView.schema'

const { t } = useI18n()

/** The year being reported. */
const year = ref(currentYear())

const { data, load } = useReport<ReportMonthIncome[]>(() => reportPath(reportApiPath('annual-income'), { year: year.value }))

// Picking another year reloads at once, as the dropdown has always done.
watch(year, load)

/** Months with their income; empty until loaded. */
const rows = computed<ReportMonthIncome[]>(() => data.value ?? [])

/** Base currency the amounts are converted to; empty until loaded. */
const currency = computed<string>(() => rows.value[0]?.currency ?? '')

/** Formats an amount in the base currency the API converted the report to. */
const formatMoney = baseCurrencyFormatter(() => currency.value)

/** Headline figures: the year's total, the monthly average and the best month, under the captions of the schema. */
const stats = computed<Record<string, ReportStatValue>>(() => {
  const total = rows.value.reduce((sum, row) => sum + row.amount, 0)
  const best = rows.value.reduce<ReportMonthIncome | undefined>((top, row) => (top === undefined || row.amount > top.amount ? row : top), undefined)
  return {
    total: { value: formatMoney(total), tooltip: t('reports.annualIncome.tooltips.total') },
    average: { value: formatMoney(rows.value.length === 0 ? 0 : total / rows.value.length), tooltip: t('reports.annualIncome.tooltips.average') },
    best: { value: best?.month ?? EMPTY_VALUE, hint: best ? formatMoney(best.amount) : undefined, tooltip: t('reports.annualIncome.tooltips.best') },
  }
})

/** The bar chart: the income of each month. */
const chart = computed<ReportChartData>(() => ({
  labels: rows.value.map(row => row.month),
  series: [{ label: t('reports.annualIncome.income'), data: rows.value.map(row => row.amount) }],
  valueFormatter: formatMoney,
}))

/** The table of the months, the amount written as money. */
const table = computed<ReportTableData<ReportMonthIncome>>(() => ({
  rows: rows.value,
  rowKey: 'month',
  extras: { amount: { format: ReportCellFormat.Money } },
  formatMoney,
}))
</script>

<template>
  <ReportPage
    v-model:year="year"
    :schema="schema"
    :retry="load"
    :stats="stats"
    :chart="chart"
    :table="table"
    :base-currency="currency"
    @generate="load"
  />
</template>
