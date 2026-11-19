<script setup lang="ts">
/**
 * Ticket ratings reviewer: the rated ticket replies of a period, optionally only those rated at or
 * above a chosen score. The title, the filter captions and the table columns are the ones of its schema, which the
 * skeleton draws too.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { useReport } from '@/composables/useReport'
import { ratingTone } from '@/lib/reportsRating'
import { reportPath } from '@/lib/reportsQuery'
import { DateRangePreset, presetIsoRange } from '@/utils/ui/datePicker'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportRatingRow, ReportRatings } from '@/types/reportrating'
import type { ReportTableData } from '@/types/reporttabledata'
import { ReportFieldWidth } from '@/utils/ui/reportField'
import schema from '@/views/TicketRatingsReviewerView.schema'

const { t } = useI18n()

/** Widths of the filters. */
const FILTER_WIDTHS = { minRating: ReportFieldWidth.Sm, dateRange: ReportFieldWidth.Grow }

/** The period to report; the current year at first. */
const dateRange = ref<[string, string] | null>(presetIsoRange(DateRangePreset.ThisYear))

/** Lowest rating to list, as text; empty for any. */
const minRating = ref('')

/** Rating choices: any, then every second score from 1. */
const ratingOptions = computed(() => [
  { value: '', label: t('shared.any') },
  ...[1, 3, 5, 7, 9].map(rating => ({ value: String(rating), label: t('reports.ticketRatingsReviewer.ratingAtLeast', { rating }) })),
])

const { data, load } = useReport<ReportRatings>(() =>
  reportPath('/reports/ticket-ratings-reviewer', {
    minRating: minRating.value,
    from: dateRange.value?.[0],
    to: dateRange.value?.[1],
  }),
)

/** The rated replies: the ticket as an id, the date as a date, and the tooltip that explains the rating. */
const table = computed<ReportTableData<ReportRatingRow>>(() => ({
  rows: data.value?.rows ?? [],
  rowKey: 'ticketId',
  extras: {
    ticketId: { format: ReportCellFormat.Id },
    date: { format: ReportCellFormat.Date },
    rating: { hint: t('reports.ticketRatingsReviewer.ratingHint') },
  },
}))
</script>

<template>
  <ReportPage :retry="load" :schema="schema" :widths="FILTER_WIDTHS" :table="table" @generate="load">
    <template #field-minRating>
      <UiSelect v-model="minRating" :options="ratingOptions" :placeholder="t('shared.any')" data-testid="report-min-rating" />
    </template>
    <template #field-dateRange>
      <UiDateRangePicker v-model="dateRange" :placeholder="t('shared.selectDateRange')" />
    </template>

    <template #cell-rating="{ row }">
      <UiBadge :tone="ratingTone(row.rating)" small>
        {{ row.rating }}
      </UiBadge>
    </template>
  </ReportPage>
</template>
