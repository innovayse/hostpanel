<script setup lang="ts">
/**
 * Screen of a filterable query report (clients, domains, invoices, services, transactions).
 *
 * Draws the shared layout: the header with the buttons of its schema (export, print), the column picker and date ranges, the advanced filters, the actions, the
 * found-count line and the table of the chosen columns, whose card footer holds the pager. The state and the loading live
 * in `useQueryReport`, which the screen calls and passes in as `report`.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportFilterBar from '@/components/reports/ReportFilterBar.vue'
import ReportField from '@/components/reports/ReportField.vue'
import ReportPage from '@/components/reports/ReportPage.vue'
import ReportTable from '@/components/reports/ReportTable.vue'
import UiAdvancedFilters from '@/components/ui/UiAdvancedFilters.vue'
import type { FieldOption } from '@/components/ui/UiAdvancedFilters.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiFieldSelector from '@/components/ui/UiFieldSelector.vue'
import UiText from '@/components/ui/UiText.vue'
import type { ReportColumn } from '@/types/reportcolumn'
import type { ReportQueryRange, ReportQueryView } from '@/types/reportquery'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

const { t } = useI18n()

/** Props for ReportQueryPage. */
const props = defineProps<{
  /** The schema of the report, which names its title and description. */
  schema: ReportSkeletonSchema
  /** State and actions from `useQueryReport`. */
  report: ReportQueryView
  /** Every column the operator can choose from. */
  columns: readonly ReportColumn[]
  /** Date-range filters in display order. */
  ranges: readonly ReportQueryRange[]
  /** Fields the advanced filters can target. */
  filterFields: FieldOption[]
  /** Key of the field the first advanced filter row starts on. */
  defaultFilterField: string
  /** The "N found" line (already translated). */
  foundText: string
}>()

/** How many filters are in force: the date ranges that are set plus the complete advanced rows; the phone's Filters button shows it. */
const activeCount = computed<number>(() => Object.values(props.report.ranges).filter((range) => range !== null).length + props.report.filters.length)
</script>

<template>
  <ReportPage :schema="schema" :retry="report.load" @print="report.print()" @export="report.exportCsv()">
    <template #filters>
      <ReportFilterBar :active-count="activeCount" @generate="report.generate()">
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

        <UiAdvancedFilters :fields="filterFields" :default-field="defaultFilterField" :initial="report.filters" @update:filters="report.setFilters($event)" />
      </ReportFilterBar>
    </template>

    <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Sm" data-testid="report-found">
      {{ foundText }}
    </UiText>

    <ReportTable
      :columns="report.activeColumns"
      :rows="report.rows"
      row-key="id"
      :pagination="{ page: report.page, total: report.totalCount, pageSize: report.pageSize, testid: 'report-pagination' }"
      @page="report.goToPage"
    />
  </ReportPage>
</template>
