<script setup lang="ts">
/**
 * Clients report: a custom export of clients by chosen columns, a creation-date range and up to
 * five filters.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportQueryPage from '@/components/reports/ReportQueryPage.vue'
import { reportApiPath } from '@/lib/apiPaths'
import type { FieldOption } from '@/components/ui/UiAdvancedFilters.vue'
import { useQueryReport } from '@/composables/useQueryReport'
import { QUERY_REPORT_DEFAULT_PRESET } from '@/lib/reportsQuery'
import { reportColumnKeys, reportFilterFields, reportQueryRanges } from '@/lib/reportsSchema'
import type { ReportClientRow } from '@/types/reportclientrow'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportColumn } from '@/types/reportcolumn'
import type { ReportQueryConfig, ReportQueryRange } from '@/types/reportquery'
import { REPORT_PAGE_SIZE } from '@/lib/limits'
import schema from '@/views/ClientsReportView.schema'

const { t } = useI18n()

/** Every column the report can show. */
const columns = computed<ReportColumn[]>(() => [
  { key: 'id', label: t('shared.id'), format: ReportCellFormat.Id },
  { key: 'firstName', label: t('shared.firstName') },
  { key: 'lastName', label: t('shared.lastName') },
  { key: 'companyName', label: t('shared.companyName') },
  { key: 'email', label: t('shared.email') },
  { key: 'address.address1', label: t('shared.address1') },
  { key: 'address.address2', label: t('shared.address2') },
  { key: 'address.city', label: t('shared.city') },
  { key: 'address.state', label: t('shared.state') },
  { key: 'address.postCode', label: t('shared.postcode') },
  { key: 'address.country', label: t('shared.country') },
  { key: 'phone', label: t('shared.phoneNumber') },
  { key: 'currency', label: t('shared.currency') },
  { key: 'creditBalance', label: t('reports.clientsReport.creditBalance'), format: ReportCellFormat.Money, currencyKey: 'currency' },
  { key: 'status', label: t('shared.status'), format: ReportCellFormat.Status },
  { key: 'createdAt', label: t('reports.invoicesReport.creationDate'), format: ReportCellFormat.Date },
  { key: 'notes', label: t('shared.notes') },
])

/** Date-range filters: the schema's filters besides the column picker. */
const ranges = computed<ReportQueryRange[]>(() => reportQueryRanges(schema.filters, t))

/** Keys of the columns the advanced filters can target. */
const FILTER_KEYS: readonly string[] = [
  'firstName',
  'lastName',
  'companyName',
  'email',
  'address.country',
  'address.city',
  'phone',
  'currency',
  'status',
]

/** Fields the advanced filters can target. */
const filterFields = computed<FieldOption[]>(() => reportFilterFields(columns.value, FILTER_KEYS))

/** Preset each date range starts on, so the first result is not unbounded. */
const DEFAULT_RANGES = { created: QUERY_REPORT_DEFAULT_PRESET } as const

/** Field the first advanced filter row starts on. */
const DEFAULT_FILTER_FIELD = 'status'

/** What the report is. */
const config: ReportQueryConfig = {
  path: reportApiPath('clients'),
  serverFields: ['status', 'country'],
  pageSize: REPORT_PAGE_SIZE,
  defaultRanges: DEFAULT_RANGES,
  defaultFilterField: DEFAULT_FILTER_FIELD,
  defaultColumns: reportColumnKeys(schema.table),
}


const report = useQueryReport<ReportClientRow>(config, { columns, ranges })
</script>

<template>
  <ReportQueryPage
    :schema="schema"
    :report="report"
    :columns="columns"
    :ranges="ranges"
    :filter-fields="filterFields"
    :default-filter-field="config.defaultFilterField"
    :found-text="t('reports.clientsReport.totalcountClientSFound', { totalCount: report.totalCount })"
  />
</template>
