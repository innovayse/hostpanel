<script setup lang="ts">
/**
 * Transactions report: a custom export of transactions by chosen columns, a date range and up to
 * five filters, with a CSV export. The title, the date range caption and the columns shown at first are the ones of
 * its schema, which the skeleton draws too.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportQueryPage from '@/components/reports/ReportQueryPage.vue'
import { reportApiPath } from '@/lib/apiPaths'
import { QUERY_REPORT_DEFAULT_PRESET } from '@/lib/reportsQuery'
import { reportColumnKeys, reportQueryRanges } from '@/lib/reportsSchema'
import type { FieldOption } from '@/components/ui/UiAdvancedFilters.vue'
import { useQueryReport } from '@/composables/useQueryReport'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportColumn } from '@/types/reportcolumn'
import type { ReportQueryConfig } from '@/types/reportquery'
import type { ReportTransactionRow } from '@/types/reporttransactionrow'
import { REPORT_PAGE_SIZE } from '@/lib/limits'
import schema from '@/views/TransactionsReportView.schema'

const { t } = useI18n()

/** Every column the report can show. */
const columns = computed<ReportColumn[]>(() => [
  { key: 'id', label: t('shared.id'), format: ReportCellFormat.Id },
  { key: 'clientId', label: t('reports.transactionsReport.userId') },
  { key: 'clientName', label: t('shared.clientName') },
  { key: 'currency', label: t('shared.currency') },
  { key: 'paymentMethod', label: t('shared.paymentMethod') },
  { key: 'date', label: t('shared.date'), format: ReportCellFormat.Date },
  { key: 'description', label: t('shared.description') },
  { key: 'invoiceId', label: t('shared.invoiceId') },
  { key: 'transactionId', label: t('shared.transactionId') },
  { key: 'amountIn', label: t('shared.amountIn'), format: ReportCellFormat.Money, currencyKey: 'currency' },
  { key: 'fees', label: t('shared.fees'), format: ReportCellFormat.Money, currencyKey: 'currency' },
  { key: 'amountOut', label: t('shared.amountOut'), format: ReportCellFormat.Money, currencyKey: 'currency' },
])


/** Date-range filters, the schema's. */
const ranges = computed(() => reportQueryRanges(schema.filters, t))

/** Fields the advanced filters can target: every column. */
const filterFields = computed<FieldOption[]>(() =>
  columns.value.map(column => ({ value: column.key, label: column.label })),
)

/** Preset each date range starts on, so the first result is not unbounded. */
const DEFAULT_RANGES = { date: QUERY_REPORT_DEFAULT_PRESET } as const

/** Field the first advanced filter row starts on. */
const DEFAULT_FILTER_FIELD = 'paymentMethod'

/** What the report is. */
const config: ReportQueryConfig = {
  path: reportApiPath('transactions'),
  serverFields: ['paymentMethod'],
  exportPath: reportApiPath('transactions', 'export'),
  pageSize: REPORT_PAGE_SIZE,
  defaultRanges: DEFAULT_RANGES,
  defaultFilterField: DEFAULT_FILTER_FIELD,
  defaultColumns: reportColumnKeys(schema.table),
}

const report = useQueryReport<ReportTransactionRow>(config, { columns, ranges })
</script>

<template>
  <ReportQueryPage
    :schema="schema"
    :report="report"
    :columns="columns"
    :ranges="ranges"
    :filter-fields="filterFields"
    :default-filter-field="config.defaultFilterField"
    :found-text="t('reports.transactionsReport.totalcountTransactionSFound', { totalCount: report.totalCount })"
  />
</template>
