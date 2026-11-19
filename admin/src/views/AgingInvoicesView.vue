<script setup lang="ts">
/**
 * Aging invoices report: a pie of the overdue periods over the table of the overdue amount per period and
 * currency. Both blocks are drawn while the report loads (the table with the schema's single amount column), so the
 * screen has the same blocks as its skeleton; the currency columns replace that column once the report is read.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
import { useReport } from '@/composables/useReport'
import { reportApiPath } from '@/lib/apiPaths'
import { formatCurrencyCode, formatReportAmount } from '@/lib/money'
import { reportColumn } from '@/lib/reportsSchema'
import type { ReportAging } from '@/types/reportaging'
import type { ReportChartData } from '@/types/reportchartdata'
import type { ReportColumn } from '@/types/reportcolumn'
import type { ReportTableData } from '@/types/reporttabledata'
import schema from '@/views/AgingInvoicesView.schema'

const { t } = useI18n()

const { data, load } = useReport<ReportAging>(() => reportApiPath('aging-invoices-summary'))

/** Currency codes of the report, in column order; none until it is read. */
const currencies = computed<string[]>(() => data.value?.currencies ?? [])

/** Key of the schema's placeholder amount column, which the currency columns replace once the report is read. */
const AMOUNT_KEY = 'amount'

/** One amount column per currency of the report, in place of the schema's placeholder. */
const currencyColumns = computed<ReportColumn[]>(() => {
  const placeholder = schema.table?.columns.find(column => column.key === AMOUNT_KEY)
  return placeholder === undefined
    ? []
    : currencies.value.map(currency => ({
      ...reportColumn(placeholder, t),
      key: currency,
      label: t('reports.agingInvoices.curAmount', { cur: currency }),
    }))
})

/** One row per period with its amounts already formatted in their own currency. */
const rows = computed<Record<string, string>[]>(() =>
  (data.value?.periods ?? []).map(period => ({
    period: t(`reports.agingInvoices.buckets.${period.period}`),
    ...Object.fromEntries(
      currencies.value.map(currency => [currency, formatCurrencyCode(period.amountsByCurrency[currency] ?? 0, currency)]),
    ),
  })),
)

/** The footer figures, formatted per currency. */
const totals = computed<Record<string, string>>(() =>
  Object.fromEntries(
    currencies.value.map(currency => [currency, formatCurrencyCode(data.value?.totals[currency] ?? 0, currency)]),
  ),
)

/** The table: the periods with their amounts and the totals, the currency columns in place of the placeholder. */
const table = computed<ReportTableData<Record<string, string>>>(() => ({
  rows: rows.value,
  rowKey: 'period',
  totals: totals.value,
  expansions: { [AMOUNT_KEY]: currencyColumns.value },
}))

/** Periods with something overdue; the pie compares periods, so each currency counts at face value. */
const overduePeriods = computed(() =>
  (data.value?.periods ?? [])
    .map(period => ({ label: t(`reports.agingInvoices.buckets.${period.period}`), total: Object.values(period.amountsByCurrency).reduce((sum, amount) => sum + amount, 0) }))
    .filter(period => period.total > 0),
)

/** The pie: one slice per overdue period. */
const chart = computed<ReportChartData>(() => ({
  labels: overduePeriods.value.map(period => period.label),
  series: [{ label: t('shared.total'), data: overduePeriods.value.map(period => period.total) }],
  valueFormatter: formatReportAmount,
}))
</script>

<template>
  <ReportPage :schema="schema" :retry="load" :chart="chart" :table="table" />
</template>
