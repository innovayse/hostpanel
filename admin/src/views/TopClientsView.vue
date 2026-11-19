<script setup lang="ts">
/**
 * Top clients report: the ten clients with the highest net income, as a pie chart and a table,
 * with a refresh button and a CSV download of the table. The title, the stat captions, the table columns and the
 * note are the ones of its schema, which the skeleton draws too.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
import { useReport } from '@/composables/useReport'
import { EMPTY_VALUE } from '@/lib/format'
import { baseCurrencyFormatter } from '@/lib/money'
import { downloadCsv } from '@/lib/reportsCsv'
import { reportColumns } from '@/lib/reportsSchema'
import type { ReportChartData } from '@/types/reportchartdata'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportColumnExtra } from '@/types/reportcolumn'
import type { ReportStatValue } from '@/types/reportstat'
import type { ReportTableData } from '@/types/reporttabledata'
import type { ReportTopClient } from '@/types/reporttopclient'
import schema from '@/views/TopClientsView.schema'

const { t } = useI18n()

/** Name of the downloaded file. */
const CSV_FILE_NAME = 'top-clients-by-income.csv'

/** How the cells of the schema columns are written: every amount in the base currency. */
const COLUMN_EXTRAS: Readonly<Record<string, ReportColumnExtra>> = {
  clientId: { format: ReportCellFormat.Id },
  totalAmountIn: { format: ReportCellFormat.Money },
  totalFees: { format: ReportCellFormat.Money },
  totalAmountOut: { format: ReportCellFormat.Money },
  balance: { format: ReportCellFormat.Money },
}

const { data, load } = useReport<ReportTopClient[]>(() => '/reports/top-clients-by-income?take=10')

/** Clients ranked by income; empty until loaded. */
const rows = computed<ReportTopClient[]>(() => data.value ?? [])

/** Base currency the amounts are converted to; empty until loaded. */
const currency = computed<string>(() => rows.value[0]?.currency ?? '')

/** Formats an amount in the base currency the API converted the report to. */
const formatMoney = baseCurrencyFormatter(() => currency.value)

/** Figures of the schema tiles: the leader's income, the total of the ranked clients and how many were ranked. */
const stats = computed<Record<string, ReportStatValue>>(() => ({
  leader: {
    value: rows.value[0]?.clientName ?? EMPTY_VALUE,
    hint: rows.value[0] ? formatMoney(rows.value[0].balance) : undefined,
    tooltip: t('reports.topClients.leaderHint'),
  },
  total: { value: formatMoney(rows.value.reduce((sum, row) => sum + row.balance, 0)), tooltip: t('reports.topClients.totalIncomeHint') },
  clients: { value: rows.value.length, tooltip: t('reports.topClients.rankedClientsHint') },
}))

/** The pie: the net income from each client. */
const chart = computed<ReportChartData>(() => ({
  labels: rows.value.map(row => row.clientName),
  series: [{ label: t('shared.balance'), data: rows.value.map(row => row.balance) }],
  valueFormatter: formatMoney,
}))

/** The table of the ranked clients, every amount in the base currency. */
const table = computed<ReportTableData<ReportTopClient>>(() => ({
  rows: rows.value,
  rowKey: 'clientId',
  extras: COLUMN_EXTRAS,
  formatMoney,
}))

/** Downloads the table as a CSV file. */
const exportCsv = (): void => {
  downloadCsv(
    CSV_FILE_NAME,
    reportColumns(schema.table, t).map(column => column.label),
    rows.value.map(row => [row.clientId, row.clientName, row.totalAmountIn, row.totalFees, row.totalAmountOut, row.balance]),
  )
}
</script>

<template>
  <ReportPage :retry="load" :schema="schema" :stats="stats" :chart="chart" :table="table" :base-currency="currency" @export="exportCsv" />
</template>
