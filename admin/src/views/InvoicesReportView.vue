<script setup lang="ts">
/**
 * Invoices report: a custom export of invoices by chosen columns, three date ranges and up to five
 * filters, with a CSV export.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportQueryPage from '@/components/reports/ReportQueryPage.vue'
import { reportApiPath } from '@/lib/apiPaths'
import type { FieldOption } from '@/components/ui/UiAdvancedFilters.vue'
import { useQueryReport } from '@/composables/useQueryReport'
import { QUERY_REPORT_DEFAULT_PRESET } from '@/lib/reportsQuery'
import { reportColumnKeys, reportQueryRanges } from '@/lib/reportsSchema'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportColumn } from '@/types/reportcolumn'
import type { ReportInvoiceRow } from '@/types/reportinvoicerow'
import type { ReportQueryConfig, ReportQueryRange } from '@/types/reportquery'
import { REPORT_PAGE_SIZE } from '@/lib/limits'
import schema from '@/views/InvoicesReportView.schema'

const { t } = useI18n()

/** Every column the report can show. */
const columns = computed<ReportColumn[]>(() => [
  { key: 'id', label: t('shared.id'), format: ReportCellFormat.Id },
  { key: 'clientId', label: t('reports.invoicesReport.userId') },
  { key: 'clientName', label: t('shared.clientName') },
  { key: 'dates.created', label: t('reports.invoicesReport.creationDate'), format: ReportCellFormat.Date },
  { key: 'dates.due', label: t('shared.dueDate'), format: ReportCellFormat.Date },
  { key: 'dates.paid', label: t('shared.datePaid'), format: ReportCellFormat.Date },
  { key: 'dates.refunded', label: t('billing.invoicesList.dateRefunded'), format: ReportCellFormat.Date },
  { key: 'currency', label: t('shared.currency') },
  { key: 'amounts.subTotal', label: t('reports.directDebit.subtotal'), format: ReportCellFormat.Money, currencyKey: 'currency' },
  { key: 'amounts.credit', label: t('reports.directDebit.credit'), format: ReportCellFormat.Money, currencyKey: 'currency' },
  { key: 'amounts.tax', label: t('reports.directDebit.tax'), format: ReportCellFormat.Money, currencyKey: 'currency' },
  { key: 'amounts.total', label: t('shared.total'), format: ReportCellFormat.Money, currencyKey: 'currency' },
  { key: 'amounts.taxRate', label: t('billing.invoiceOptionsTab.taxRate'), format: ReportCellFormat.Percent },
  { key: 'status', label: t('shared.status'), format: ReportCellFormat.Status },
  { key: 'paymentMethod', label: t('shared.paymentMethod') },
  { key: 'notes', label: t('shared.notes') },
])

/**
 * Date-range filters: the schema's filters besides the column picker. There is no refunded or cancelled range: an
 * invoice stores no such date, so the endpoint could not filter by it.
 */
const ranges = computed<ReportQueryRange[]>(() => reportQueryRanges(schema.filters, t))

/** Fields the advanced filters can target: every column. */
const filterFields = computed<FieldOption[]>(() =>
  columns.value.map(column => ({ value: column.key, label: column.label })),
)

/** Preset each date range starts on, so the first result is not unbounded. */
const DEFAULT_RANGES = { created: QUERY_REPORT_DEFAULT_PRESET } as const

/** Field the first advanced filter row starts on. */
const DEFAULT_FILTER_FIELD = 'status'

/** What the report is. */
const config: ReportQueryConfig = {
  path: reportApiPath('invoices'),
  serverFields: ['status'],
  exportPath: reportApiPath('invoices', 'export'),
  pageSize: REPORT_PAGE_SIZE,
  defaultRanges: DEFAULT_RANGES,
  defaultFilterField: DEFAULT_FILTER_FIELD,
  defaultColumns: reportColumnKeys(schema.table),
}


const report = useQueryReport<ReportInvoiceRow>(config, { columns, ranges })
</script>

<template>
  <ReportQueryPage
    :schema="schema"
    :report="report"
    :columns="columns"
    :ranges="ranges"
    :filter-fields="filterFields"
    :default-filter-field="config.defaultFilterField"
    :found-text="t('reports.invoicesReport.totalcountInvoiceSFound', { totalCount: report.totalCount })"
  />
</template>
