<script setup lang="ts">
/**
 * Ticket feedback scores report: how many of each rating (1 to 10) every staff member received in
 * a date range, with the average as a bar chart.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportChartCard from '@/components/reports/ReportChartCard.vue'
import ReportField from '@/components/reports/ReportField.vue'
import ReportFilterBar from '@/components/reports/ReportFilterBar.vue'
import ReportPage from '@/components/reports/ReportPage.vue'
import ReportTable from '@/components/reports/ReportTable.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import { useReport } from '@/composables/useReport'
import { reportPath } from '@/lib/reportsQuery'
import { currentYearRange } from '@/lib/reportsPeriod'
import type { ReportColumn } from '@/types/reportcolumn'
import type { ReportFeedbackScores } from '@/types/reportfeedback'
import { UiChartType } from '@/utils/ui/chart'
import type { UiChartSeries } from '@/utils/ui/chart'
import { UiAlign } from '@/utils/ui/table'
import { EMPTY_VALUE } from '@/lib/format'

const { t } = useI18n()

/** The ratings a customer can give, 1 to 10. */
const RATINGS = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]

/** The period to report; the current year at first. */
const dateRange = ref<[string, string] | null>(currentYearRange())

const { data, load } = useReport<ReportFeedbackScores>(() =>
  reportPath('/reports/ticket-feedback-scores', { from: dateRange.value?.[0], to: dateRange.value?.[1] }),
)

/** Table columns: the staff member, one per rating, the count and the average. */
const columns = computed<ReportColumn[]>(() => [
  { key: 'staffName', label: t('reports.ticketFeedbackScores.staffName') },
  ...RATINGS.map(rating => ({ key: `r${rating}`, label: String(rating), align: UiAlign.Center })),
  { key: 'totalRatings', label: t('reports.ticketFeedbackScores.totalRatings'), align: UiAlign.Right },
  { key: 'averageRating', label: t('reports.ticketFeedbackScores.averageRating'), align: UiAlign.Right },
])

/** Chart labels: the staff members. */
const labels = computed<string[]>(() => (data.value?.rows ?? []).map(row => row.staffName))

/** Chart series: the average rating of each. */
const series = computed<UiChartSeries[]>(() => [
  { label: t('reports.ticketFeedbackScores.averageRating'), data: (data.value?.rows ?? []).map(row => row.averageRating) },
])
</script>

<template>
  <ReportPage :retry="load"
    :title="t('reports.ticketFeedbackScores.ticketFeedbackScores')"
    :description="t('reports.ticketFeedbackScores.thisReportProvidesASummary')"
  >
    <template #filters>
      <ReportFilterBar :generate-label="t('shared.generateReport')" @generate="load">
        <template #fields>
          <ReportField :label="t('shared.dateRange')" width="grow">
            <UiDateRangePicker v-model="dateRange" :placeholder="t('shared.selectDateRange')" />
          </ReportField>
        </template>
      </ReportFilterBar>
    </template>

    <template v-if="data">
      <ReportChartCard :type="UiChartType.Bar" :labels="labels" :series="series" height="lg" />
      <ReportTable
        :columns="columns"
        :rows="data.rows"
        row-key="staffName"
        :empty-text="t('shared.noDataFoundForThis2')"
      >
        <template v-for="rating in RATINGS" #[`cell-r${rating}`]="{ value }">
          {{ value || EMPTY_VALUE }}
        </template>
        <template #cell-averageRating="{ row }">
          {{ row.averageRating.toFixed(2) }}
        </template>
      </ReportTable>
    </template>
  </ReportPage>
</template>
