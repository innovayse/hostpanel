<script setup lang="ts">
/**
 * Screen of a filterable query report (clients, domains, invoices, services, transactions).
 *
 * Draws the shared layout: the header with export and print, the column picker and date ranges, the advanced filters, the actions, the
 * found-count line, the table of the chosen columns and the pager. The state and the loading live
 * in `useQueryReport`, which the screen calls and passes in as `report`.
 */
import { useI18n } from 'vue-i18n'
import ReportActions from '@/components/reports/ReportActions.vue'
import ReportFilterBar from '@/components/reports/ReportFilterBar.vue'
import ReportField from '@/components/reports/ReportField.vue'
import ReportPage from '@/components/reports/ReportPage.vue'
import ReportTable from '@/components/reports/ReportTable.vue'
import UiAdvancedFilters from '@/components/ui/UiAdvancedFilters.vue'
import type { FieldOption } from '@/components/ui/UiAdvancedFilters.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiFieldSelector from '@/components/ui/UiFieldSelector.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiText from '@/components/ui/UiText.vue'
import { useReportsStore } from '@/stores/reports'
import type { ReportColumn } from '@/types/reportcolumn'
import type { ReportQueryRange, ReportQueryView } from '@/types/reportquery'

const { t } = useI18n()

/** The reports store, read for the loading flag the pager follows. */
const reports = useReportsStore()

/** Props for ReportQueryPage. */
defineProps<{
  /** Report title (already translated). */
  title: string
  /** Line under the title (already translated). */
  description: string
  /** State and actions from `useQueryReport`. */
  report: ReportQueryView
  /** Every column the operator can choose from. */
  columns: readonly ReportColumn[]
  /** Date-range filters in display order. */
  ranges: readonly ReportQueryRange[]
  /** Fields the advanced filters can target. */
  filterFields: FieldOption[]
  /** The "N found" line (already translated). */
  foundText: string
  /** Text of the table when there are no rows (already translated). */
  emptyText: string
  /** Whether the report has a CSV export. */
  exportable?: boolean
}>()
</script>

<template>
  <ReportPage :title="title" :description="description" :retry="report.load">
    <template #actions>
      <ReportActions printable :exportable="exportable" @print="report.print()" @export="report.exportCsv()" />
    </template>

    <template #filters>
      <ReportFilterBar @generate="report.generate()">
        <template #fields>
          <ReportField :label="t('shared.fieldsToInclude')" width="md">
            <UiFieldSelector
              :fields="columns"
              :selected="report.visibleKeys"
              @toggle="report.toggleColumn"
              @select-all="report.selectAllColumns()"
              @clear-all="report.clearColumns()"
            />
          </ReportField>
          <ReportField v-for="range in ranges" :key="range.key" :label="range.label" width="grow">
            <UiDateRangePicker
              :model-value="report.ranges[range.key] ?? null"
              :placeholder="t('shared.selectDateRange')"
              :data-testid="`report-range-${range.key}`"
              @update:model-value="report.setRange(range.key, $event)"
            />
          </ReportField>
        </template>

        <UiAdvancedFilters :fields="filterFields" @update:filters="report.setFilters($event)" />
      </ReportFilterBar>
    </template>

    <UiText tone="secondary" size="sm" data-testid="report-found">
      {{ foundText }}
    </UiText>

    <ReportTable :columns="report.activeColumns" :rows="report.rows" row-key="id" :empty-text="emptyText" />

    <UiPagination
      :page="report.page"
      :total="report.totalCount"
      :page-size="report.pageSize"
      :loading="reports.loading"
      testid="report-pagination"
      @update:page="report.goToPage"
    />
  </ReportPage>
</template>
