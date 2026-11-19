<script setup lang="ts">
/**
 * Ticket feedback comments report: the comments customers left on support tickets, with the
 * rating, filtered by staff member and date range. The title, the filter captions and the table columns are the
 * ones of its schema, which the skeleton draws too.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiInput from '@/components/ui/UiInput.vue'
import { useReport } from '@/composables/useReport'
import { formatDate } from '@/lib/format'
import { ratingTone } from '@/lib/reportsRating'
import { reportPath } from '@/lib/reportsQuery'
import { DateRangePreset, presetIsoRange } from '@/utils/ui/datePicker'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportFeedbackCommentRow, ReportFeedbackComments } from '@/types/reportfeedback'
import type { ReportTableData } from '@/types/reporttabledata'
import { ReportFieldWidth } from '@/utils/ui/reportField'
import schema from '@/views/TicketFeedbackCommentsView.schema'

const { t } = useI18n()

/** Width of the date range filter. */
const FILTER_WIDTHS = { dateRange: ReportFieldWidth.Grow }

/** Staff member to look for; empty for any. */
const staffName = ref('')

/** The period to report; the current year at first. */
const dateRange = ref<[string, string] | null>(presetIsoRange(DateRangePreset.ThisYear))

const { data, load } = useReport<ReportFeedbackComments>(() =>
  reportPath('/reports/ticket-feedback-comments', {
    staffName: staffName.value.trim(),
    from: dateRange.value?.[0],
    to: dateRange.value?.[1],
  }),
)

/** The comments: the ticket as an id, and the tooltip that explains the rating. */
const table = computed<ReportTableData<ReportFeedbackCommentRow>>(() => ({
  rows: data.value?.rows ?? [],
  rowKey: 'ticketId',
  extras: {
    ticketId: { format: ReportCellFormat.Id },
    rating: { hint: t('reports.ticketFeedbackComments.ratingHint') },
  },
}))
</script>

<template>
  <ReportPage :retry="load" :schema="schema" :widths="FILTER_WIDTHS" :table="table" @generate="load">
    <template #field-staffName>
      <UiInput v-model="staffName" :placeholder="t('shared.any')" data-testid="report-staff-name" />
    </template>
    <template #field-dateRange>
      <UiDateRangePicker v-model="dateRange" :placeholder="t('shared.selectDateRange')" />
    </template>

    <template #cell-feedbackLeftBy="{ row }">
      {{ row.feedbackLeftBy }} · {{ formatDate(row.feedbackAt) }}
    </template>
    <template #cell-rating="{ row }">
      <UiBadge :tone="ratingTone(row.rating)" small>
        {{ row.rating }}
      </UiBadge>
    </template>
  </ReportPage>
</template>
