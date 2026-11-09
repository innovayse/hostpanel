<script setup lang="ts">
/**
 * Customer retention report: how long services of each product stay active, grouped by product
 * group, optionally counting active services as ending on their next due date.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportGroupRow from '@/components/reports/ReportGroupRow.vue'
import ReportPage from '@/components/reports/ReportPage.vue'
import ReportTable from '@/components/reports/ReportTable.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { useReport } from '@/composables/useReport'
import { reportPath } from '@/lib/reportsQuery'
import type { ReportColumn } from '@/types/reportcolumn'
import { ReportRetentionGroupKind } from '@/types/reportretention'
import type { ReportRetention, ReportRetentionGroup, ReportRetentionRow } from '@/types/reportretention'

const { t } = useI18n()

/** Whether active services count, as ending on their next due date. */
const includeActive = ref(true)

const { data, load } = useReport<ReportRetention>(() =>
  reportPath('/reports/customer-retention', { includeActive: includeActive.value }),
)

// Ticking the box reloads at once.
watch(includeActive, load)

/** Table columns. */
const columns = computed<ReportColumn[]>(() => [
  { key: 'productName', label: t('shared.productName') },
  { key: 'billingCycle', label: t('shared.billingCycle') },
  { key: 'productCount', label: t('reports.customerRetention.productCount'), numeric: true },
  { key: 'avgDaysActive', label: t('reports.customerRetention.averageDaysActive'), numeric: true },
  { key: 'avgYearsMonthsActive', label: t('reports.customerRetention.averageYearsMonthsActive') },
])

/**
 * The heading of a retention group.
 *
 * @param group - The group.
 * @returns The product group's name, or the translated word for the domains group.
 */
const groupLabel = (group: ReportRetentionGroup): string =>
  group.kind === ReportRetentionGroupKind.Domains ? t('shared.domains') : (group.groupName ?? '')

/**
 * The billing cycle cell of a row.
 *
 * @param row - The row.
 * @returns A service's billing cycle, or a domain's registration term in years.
 */
const cycleLabel = (row: ReportRetentionRow): string =>
  row.termYears === null ? (row.billingCycle ?? '') : t('reports.customerRetention.activeYears', { count: row.termYears }, row.termYears)

/**
 * The average active time of a row as years and months, or days when it is under a month.
 *
 * @param row - The row.
 * @returns For example `2 years 3 months`, `1 year` or `12 days`, in the interface language's plural forms.
 */
const activeLabel = (row: ReportRetentionRow): string => {
  const parts: string[] = []
  if (row.avgYears > 0) {
    parts.push(t('reports.customerRetention.activeYears', { count: row.avgYears }, row.avgYears))
  }
  if (row.avgMonths > 0) {
    parts.push(t('reports.customerRetention.activeMonths', { count: row.avgMonths }, row.avgMonths))
  }
  return parts.length > 0
    ? parts.join(' ')
    : t('reports.customerRetention.activeDays', { count: row.avgDaysActive }, row.avgDaysActive)
}
</script>

<template>
  <ReportPage :retry="load"
    :title="t('reports.customerRetention.averageCustomerRetentionTime')"
    :description="t('reports.customerRetention.thisReportCalculatesAndProvides')"
  >
    <template v-if="data">
      <UiLabel variant="inline">
        <UiCheckbox v-model="includeActive" data-testid="report-include-active" />
        {{ t('reports.customerRetention.includeActiveProductsServicesAssuming') }}
      </UiLabel>
      <ReportTable :columns="columns" :empty="data.groups.length === 0" :empty-text="t('reports.customerRetention.noDataFound')">
        <template #body>
          <template v-for="group in data.groups" :key="group.kind + group.groupName">
            <ReportGroupRow :label="groupLabel(group)" :colspan="columns.length" />
            <UiTableRow v-for="row in group.rows" :key="row.productName + cycleLabel(row)" hoverable>
              <UiTableCell>{{ row.productName }}</UiTableCell>
              <UiTableCell>{{ cycleLabel(row) }}</UiTableCell>
              <UiTableCell numeric>{{ row.productCount }}</UiTableCell>
              <UiTableCell numeric>{{ row.avgDaysActive }}</UiTableCell>
              <UiTableCell>{{ activeLabel(row) }}</UiTableCell>
            </UiTableRow>
          </template>
        </template>
      </ReportTable>
    </template>
  </ReportPage>
</template>
