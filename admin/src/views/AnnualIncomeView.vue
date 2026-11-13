<script setup lang="ts">
/**
 * Annual income report: income per month of the chosen year, as a bar chart and a table.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportChartCard from '@/components/reports/ReportChartCard.vue'
import ReportPage from '@/components/reports/ReportPage.vue'
import ReportPeriodFilter from '@/components/reports/ReportPeriodFilter.vue'
import ReportStatRow from '@/components/reports/ReportStatRow.vue'
import ReportTable from '@/components/reports/ReportTable.vue'
import UiText from '@/components/ui/UiText.vue'
import { useReport } from '@/composables/useReport'
import { formatCurrencyCode } from '@/lib/money'
import { reportPath } from '@/lib/reportsQuery'
import { currentYear } from '@/lib/reportsPeriod'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportColumn } from '@/types/reportcolumn'
import type { ReportStat } from '@/types/reportstat'
import type { ReportMonthIncome } from '@/types/reportmonthincome'
import { UiChartType } from '@/utils/ui/chart'
import type { UiChartSeries } from '@/utils/ui/chart'
import { UiAlign } from '@/utils/ui/table'

const { t } = useI18n()

/** The year being reported. */
const year = ref(currentYear())

const { data, load } = useReport<ReportMonthIncome[]>(() => reportPath('/reports/annual-income', { year: year.value }))

// Picking another year reloads at once, as the dropdown has always done.
watch(year, load)

/** Months with their income; empty until loaded. */
const rows = computed<ReportMonthIncome[]>(() => data.value ?? [])

/** Base currency the amounts are converted to; empty until loaded. */
const currency = computed<string>(() => rows.value[0]?.currency ?? '')

/**
 * Formats an amount in the base currency the API converted the report to.
 *
 * @param amount - Amount in the report currency.
 * @returns The amount with its currency symbol.
 */
const formatMoney = (amount: number): string => formatCurrencyCode(amount, currency.value)

/** Headline figures: the year's total, the monthly average and the best month. */
const stats = computed<ReportStat[]>(() => {
  const total = rows.value.reduce((sum, row) => sum + row.amount, 0)
  const best = rows.value.reduce<ReportMonthIncome | undefined>((top, row) => (top === undefined || row.amount > top.amount ? row : top), undefined)
  return [
    { key: 'total', label: t('reports.annualIncome.totalIncome'), value: formatMoney(total) },
    { key: 'average', label: t('reports.annualIncome.averageMonth'), value: formatMoney(rows.value.length === 0 ? 0 : total / rows.value.length) },
    { key: 'best', label: t('reports.annualIncome.bestMonth'), value: best?.month ?? '—', hint: best ? formatMoney(best.amount) : undefined },
  ]
})

/** Table columns. */
const columns = computed<ReportColumn[]>(() => [
  { key: 'month', label: t('shared.month') },
  { key: 'amount', label: t('shared.amount'), align: UiAlign.Right, format: ReportCellFormat.Money },
])

/** Chart labels. */
const labels = computed<string[]>(() => rows.value.map(row => row.month))

/** Chart series. */
const series = computed<UiChartSeries[]>(() => [{ label: t('reports.annualIncome.income'), data: rows.value.map(row => row.amount) }])
</script>

<template>
  <ReportPage :retry="load" :title="t('reports.annualIncome.annualIncomeReport')" :description="t('reports.annualIncome.totalIncomePerMonthFor')">
    <template #filters>
      <ReportPeriodFilter v-model:year="year" @generate="load" />
    </template>
    <ReportStatRow :stats="stats" />
    <ReportChartCard :type="UiChartType.Bar" :labels="labels" :series="series" :value-formatter="formatMoney" />
    <ReportTable :columns="columns" :rows="rows" row-key="month" :format-money="formatMoney" />
    <UiText tone="muted" size="sm">
      {{ t('reports.common.convertedToBaseCurrency', { currency }) }}
    </UiText>
  </ReportPage>
</template>
