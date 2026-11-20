<script setup lang="ts">
/**
 * Services report: a custom export of client services by chosen columns, three date ranges and up
 * to five filters. The title, the date range captions and the columns shown at first are the ones of its schema,
 * which the skeleton draws too.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportQueryPage from '@/components/reports/ReportQueryPage.vue'
import { reportApiPath } from '@/lib/apiPaths'
import { QUERY_REPORT_DEFAULT_PRESET } from '@/lib/reportsQuery'
import { reportColumnKeys, reportFilterFields, reportQueryRanges } from '@/lib/reportsSchema'
import type { FieldOption } from '@/components/ui/UiAdvancedFilters.vue'
import { useQueryReport } from '@/composables/useQueryReport'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportColumn } from '@/types/reportcolumn'
import type { ReportQueryConfig } from '@/types/reportquery'
import type { ReportServiceRow } from '@/types/reportservicerow'
import { REPORT_PAGE_SIZE } from '@/lib/limits'
import schema from '@/views/ServicesReportView.schema'

const { t } = useI18n()

/** Every column the report can show. */
const columns = computed<ReportColumn[]>(() => [
  { key: 'id', label: t('shared.id'), format: ReportCellFormat.Id },
  { key: 'clientId', label: t('reports.servicesReport.userId') },
  { key: 'clientName', label: t('shared.clientName') },
  { key: 'productId', label: t('reports.servicesReport.productId') },
  { key: 'productName', label: t('orders.orderDetail.product') },
  { key: 'domain', label: t('shared.domain') },
  { key: 'billingCycle', label: t('shared.billingCycle') },
  { key: 'firstPaymentAmount', label: t('clients.clientDomainDetail.firstPaymentAmount'), format: ReportCellFormat.Money, currencyKey: 'currencyCode' },
  { key: 'recurringAmount', label: t('clients.clientDomainDetail.recurringAmount'), format: ReportCellFormat.Money, currencyKey: 'currencyCode' },
  { key: 'paymentMethod', label: t('shared.paymentMethod') },
  { key: 'registrationDate', label: t('shared.registrationDate'), format: ReportCellFormat.Date },
  { key: 'nextDueDate', label: t('shared.nextDueDate'), format: ReportCellFormat.Date },
  { key: 'terminatedAt', label: t('reports.servicesReport.terminatedAt'), format: ReportCellFormat.Date },
  { key: 'status', label: t('shared.status'), format: ReportCellFormat.Status },
  { key: 'notes', label: t('shared.notes') },
])


/** Date-range filters, the schema's. There is no completed range: a service stores no completion date. */
const ranges = computed(() => reportQueryRanges(schema.filters, t))

/** Keys of the columns the advanced filters can target. */
const FILTER_KEYS: readonly string[] = ['clientName', 'productName', 'domain', 'billingCycle', 'paymentMethod', 'status']

/** Fields the advanced filters can target. */
const filterFields = computed<FieldOption[]>(() => reportFilterFields(columns.value, FILTER_KEYS))

/** Preset each date range starts on, so the first result is not unbounded. */
const DEFAULT_RANGES = { created: QUERY_REPORT_DEFAULT_PRESET } as const

/** Field the first advanced filter row starts on. */
const DEFAULT_FILTER_FIELD = 'status'

/** What the report is. */
const config: ReportQueryConfig = {
  path: reportApiPath('services'),
  serverFields: ['status', 'billingCycle'],
  pageSize: REPORT_PAGE_SIZE,
  defaultRanges: DEFAULT_RANGES,
  defaultFilterField: DEFAULT_FILTER_FIELD,
  defaultColumns: reportColumnKeys(schema.table),
}

const report = useQueryReport<ReportServiceRow>(config, { columns, ranges })
</script>

<template>
  <ReportQueryPage
    :schema="schema"
    :report="report"
    :columns="columns"
    :ranges="ranges"
    :filter-fields="filterFields"
    :default-filter-field="config.defaultFilterField"
    :found-text="t('reports.servicesReport.totalcountServiceSFound', { totalCount: report.totalCount })"
  />
</template>
