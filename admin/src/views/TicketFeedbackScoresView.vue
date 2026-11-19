<script setup lang="ts">
/**
 * Ticket feedback scores report: how the ratings (1 to 10) of every staff member in a date range split into poor,
 * fair and good as one stacked bar, with the number of ratings, the average and a chart of the averages. The title,
 * the filter captions and the table columns are the ones of its schema, which the skeleton draws too.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
import UiChartDistribution from '@/components/ui/UiChartDistribution.vue'
import UiChartLegend from '@/components/ui/UiChartLegend.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import { useReport } from '@/composables/useReport'
import { formatDecimal } from '@/lib/format'
import { bandCounts, RATING_BANDS } from '@/lib/reportsRating'
import { reportPath } from '@/lib/reportsQuery'
import type { ReportChartData } from '@/types/reportchartdata'
import type { ReportFeedbackScoreRow, ReportFeedbackScores } from '@/types/reportfeedback'
import type { ReportTableData } from '@/types/reporttabledata'
import type { UiChartPart } from '@/utils/ui/chart'
import { DateRangePreset, presetIsoRange } from '@/utils/ui/datePicker'
import { ReportFieldWidth } from '@/utils/ui/reportField'
import schema from '@/views/TicketFeedbackScoresView.schema'

const { t, locale } = useI18n()

/** Width of the date range filter. */
const FILTER_WIDTHS = { dateRange: ReportFieldWidth.Grow }

/** Digits shown after the separator of an average rating. */
const AVERAGE_DIGITS = 2

/** The period to report; the current year at first. */
const dateRange = ref<[string, string] | null>(presetIsoRange(DateRangePreset.ThisYear))

const { data, load } = useReport<ReportFeedbackScores>(() =>
  reportPath('/reports/ticket-feedback-scores', { from: dateRange.value?.[0], to: dateRange.value?.[1] }),
)

/** Caption of each rating band, in the order the bar draws them, with the ratings it holds. */
const bandLabels = computed<string[]>(() =>
  RATING_BANDS.map(({ band, from, to }) => t(`reports.ticketFeedbackScores.bands.${band}`, { from, to })),
)

/** The key of the bar: one entry per band. */
const legend = computed(() => RATING_BANDS.map(({ tone }, index) => ({ tone, label: bandLabels.value[index] ?? '' })))

/**
 * Lists how many ratings of each score a staff member received.
 *
 * @param row - The row of the staff member.
 * @returns The counters of ratings 1 to 10, rating 1 first.
 */
const ratingCountsOf = (row: ReportFeedbackScoreRow): number[] =>
  [row.r1, row.r2, row.r3, row.r4, row.r5, row.r6, row.r7, row.r8, row.r9, row.r10]

/**
 * Builds the parts of the distribution bar of one staff member.
 *
 * @param row - The row of the staff member.
 * @returns One part per rating band with its count and tooltip.
 */
const partsOf = (row: ReportFeedbackScoreRow): UiChartPart[] => {
  const counts = bandCounts(ratingCountsOf(row))
  return RATING_BANDS.map(({ tone }, index) => ({
    count: counts[index] ?? 0,
    tone,
    tooltip: `${bandLabels.value[index]}: ${counts[index] ?? 0}`,
  }))
}

/** Staff members with their ratings; none until loaded. */
const rows = computed<ReportFeedbackScoreRow[]>(() => data.value?.rows ?? [])

/** The table of the staff members, with the tooltips that explain the metrics. */
const table = computed<ReportTableData<ReportFeedbackScoreRow>>(() => ({
  rows: rows.value,
  rowKey: 'staffName',
  extras: {
    distribution: { hint: t('reports.ticketFeedbackScores.distributionHint') },
    totalRatings: { hint: t('reports.ticketFeedbackScores.totalRatingsHint'), numeric: true },
    averageRating: { hint: t('reports.ticketFeedbackScores.averageRatingHint'), numeric: true },
  },
}))

/** The bar chart of the average rating of each staff member. */
const chart = computed<ReportChartData>(() => ({
  labels: rows.value.map(row => row.staffName),
  series: [{ label: t('reports.ticketFeedbackScores.averageRating'), data: rows.value.map(row => row.averageRating) }],
}))
</script>

<template>
  <ReportPage :retry="load" :schema="schema" :widths="FILTER_WIDTHS" :chart="chart" :table="table" @generate="load">
    <template #field-dateRange>
      <UiDateRangePicker v-model="dateRange" :placeholder="t('shared.selectDateRange')" />
    </template>

    <template #legend>
      <UiChartLegend v-if="rows.length > 0" :items="legend" />
    </template>

    <template #cell-distribution="{ row }">
      <UiChartDistribution :parts="partsOf(row)" />
    </template>
    <template #cell-averageRating="{ row }">
      {{ formatDecimal(row.averageRating, locale, AVERAGE_DIGITS) }}
    </template>
  </ReportPage>
</template>
