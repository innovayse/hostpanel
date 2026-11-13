<script setup lang="ts">
/**
 * Top clients report: the ten clients with the highest net income, as a pie chart and a table,
 * with a CSV download of the table.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportActions from '@/components/reports/ReportActions.vue'
import ReportChartCard from '@/components/reports/ReportChartCard.vue'
import ReportPage from '@/components/reports/ReportPage.vue'
import ReportStatRow from '@/components/reports/ReportStatRow.vue'
import ReportTable from '@/components/reports/ReportTable.vue'
import UiText from '@/components/ui/UiText.vue'
import { useReport } from '@/composables/useReport'
import { downloadCsv } from '@/lib/reportsCsv'
import { formatCurrencyCode } from '@/lib/money'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportColumn } from '@/types/reportcolumn'
import type { ReportStat } from '@/types/reportstat'
import type { ReportTopClient } from '@/types/reporttopclient'
import { UiChartType } from '@/utils/ui/chart'
import type { UiChartSeries } from '@/utils/ui/chart'
import { UiAlign } from '@/utils/ui/table'

const { t } = useI18n()

/** Name of the downloaded file. */
const CSV_FILE_NAME = 'top-clients-by-income.csv'

const { data, load } = useReport<ReportTopClient[]>(() => '/reports/top-clients-by-income?take=10')

/** Clients ranked by income; empty until loaded. */
const rows = computed<ReportTopClient[]>(() => data.value ?? [])

/** Base currency the amounts are converted to; empty until loaded. */
const currency = computed<string>(() => rows.value[0]?.currency ?? '')

/**
 * Formats an amount in the base currency the API converted the report to.
 *
 * @param amount - Amount in the report currency.
 * @returns The amount with its currency symbol.
 */
const formatMoney = (amount: number): string => formatCurrencyCode(amount, currency.value)

/** Headline figures: the leader's income, the total of the ranked clients and how many were ranked. */
const stats = computed<ReportStat[]>(() => [
  { key: 'leader', label: t('reports.topClients.leader'), value: rows.value[0]?.clientName ?? '—', hint: rows.value[0] ? formatMoney(rows.value[0].balance) : undefined },
  { key: 'total', label: t('reports.topClients.totalIncome'), value: formatMoney(rows.value.reduce((sum, row) => sum + row.balance, 0)) },
  { key: 'clients', label: t('reports.topClients.rankedClients'), value: rows.value.length },
])

/** Table columns. */
const columns = computed<ReportColumn[]>(() => [
  { key: 'clientId', label: t('reports.creditsReviewer.clientId') },
  { key: 'clientName', label: t('shared.clientName') },
  { key: 'totalAmountIn', label: t('shared.amountIn'), align: UiAlign.Right, format: ReportCellFormat.Money },
  { key: 'totalFees', label: t('shared.fees'), align: UiAlign.Right, format: ReportCellFormat.Money },
  { key: 'totalAmountOut', label: t('shared.amountOut'), align: UiAlign.Right, format: ReportCellFormat.Money },
  { key: 'balance', label: t('shared.balance'), align: UiAlign.Right, format: ReportCellFormat.Money },
])

/** Pie labels: the client names. */
const labels = computed<string[]>(() => rows.value.map(row => row.clientName))

/** Pie data: the net income from each client. */
const series = computed<UiChartSeries[]>(() => [
  { label: t('shared.balance'), data: rows.value.map(row => row.balance) },
])

/** Downloads the table as a CSV file. */
const exportCsv = (): void => {
  downloadCsv(
    CSV_FILE_NAME,
    columns.value.map(column => column.label),
    rows.value.map(row => [row.clientId, row.clientName, row.totalAmountIn, row.totalFees, row.totalAmountOut, row.balance]),
  )
}
</script>

<template>
  <ReportPage :retry="load" :title="t('reports.topClients.top10ClientsByIncome')" :description="t('reports.topClients.thisReportShowsThe10')">
    <template #actions>
      <ReportActions exportable @export="exportCsv" />
    </template>

    <ReportStatRow :stats="stats" />

    <ReportChartCard :type="UiChartType.Pie" :labels="labels" :series="series" :value-formatter="formatMoney" height="lg" />
    <ReportTable :columns="columns" :rows="rows" row-key="clientId" :format-money="formatMoney" :empty-text="t('reports.topClients.noData')" />
    <UiText tone="muted" size="sm">
      {{ t('reports.common.convertedToBaseCurrency', { currency }) }}
    </UiText>
  </ReportPage>
</template>
