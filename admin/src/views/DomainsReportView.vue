<script setup lang="ts">
/**
 * Domains report: a custom export of domains by chosen columns, three date ranges and up to five
 * filters.
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
import type { ReportDomainRow } from '@/types/reportdomainrow'
import type { ReportQueryConfig, ReportQueryRange } from '@/types/reportquery'
import { REPORT_PAGE_SIZE } from '@/lib/limits'
import schema from '@/views/DomainsReportView.schema'

const { t } = useI18n()

/** Every column the report can show. */
const columns = computed<ReportColumn[]>(() => [
  { key: 'id', label: t('shared.id'), format: ReportCellFormat.Id },
  { key: 'clientId', label: t('reports.domainsReport.userId') },
  { key: 'clientName', label: t('shared.clientName') },
  { key: 'orderId', label: t('reports.domainsReport.orderId') },
  { key: 'orderType', label: t('reports.domainsReport.regType') },
  { key: 'domainName', label: t('shared.domainName') },
  { key: 'amounts.firstPayment', label: t('clients.clientDomainDetail.firstPaymentAmount'), format: ReportCellFormat.Money },
  { key: 'amounts.recurring', label: t('clients.clientDomainDetail.recurringAmount'), format: ReportCellFormat.Money },
  { key: 'registrationPeriod', label: t('clients.clientDomainDetail.registrationPeriod'), format: ReportCellFormat.Years },
  { key: 'dates.registration', label: t('shared.registrationDate') },
  { key: 'dates.expiry', label: t('shared.expiryDate') },
  { key: 'dates.nextDue', label: t('shared.nextDueDate') },
  { key: 'registrar', label: t('shared.registrar') },
  { key: 'paymentMethod', label: t('shared.paymentMethod') },
  { key: 'status', label: t('shared.status'), format: ReportCellFormat.Status },
  { key: 'notes', label: t('shared.notes') },
])

/** Date-range filters: the schema's filters besides the column picker; each key is the prefix of its two query parameters. */
const ranges = computed<ReportQueryRange[]>(() => reportQueryRanges(schema.filters, t))

/** Fields the advanced filters can target. */
const filterFields = computed<FieldOption[]>(() => [
  { value: 'domainName', label: t('shared.domainName') },
  { value: 'clientName', label: t('shared.clientName') },
  { value: 'registrar', label: t('shared.registrar') },
  { value: 'paymentMethod', label: t('shared.paymentMethod') },
  { value: 'status', label: t('shared.status') },
  { value: 'orderType', label: t('reports.domainsReport.regType') },
])

/** Preset each date range starts on, so the first result is not unbounded. */
const DEFAULT_RANGES = { registered: QUERY_REPORT_DEFAULT_PRESET } as const

/** Field the first advanced filter row starts on. */
const DEFAULT_FILTER_FIELD = 'status'

/** What the report is. */
const config: ReportQueryConfig = {
  path: reportApiPath('domains'),
  serverFields: ['status', 'registrar'],
  pageSize: REPORT_PAGE_SIZE,
  defaultRanges: DEFAULT_RANGES,
  defaultFilterField: DEFAULT_FILTER_FIELD,
  defaultColumns: reportColumnKeys(schema.table),
}


const report = useQueryReport<ReportDomainRow>(config, { columns, ranges })
</script>

<template>
  <ReportQueryPage
    :schema="schema"
    :report="report"
    :columns="columns"
    :ranges="ranges"
    :filter-fields="filterFields"
    :default-filter-field="config.defaultFilterField"
    :found-text="t('reports.domainsReport.totalcountDomainSFound', { totalCount: report.totalCount })"
  />
</template>
