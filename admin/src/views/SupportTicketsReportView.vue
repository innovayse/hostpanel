<script setup lang="ts">
/**
 * Support ticket replies report: how many tickets each staff member replied to in a month, as a total, an
 * average per day and a strip of the days with one shaded square each. The filter captions and the table columns are
 * the ones of its schema, which the skeleton draws too.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
import UiChartHeatStrip from '@/components/ui/UiChartHeatStrip.vue'
import { useReport } from '@/composables/useReport'
import { formatDecimal } from '@/lib/format'
import { currentMonthNumber, currentYear, monthName } from '@/lib/reportsPeriod'
import { reportPath } from '@/lib/reportsQuery'
import type { ReportTicketReplies, ReportTicketReplyRow } from '@/types/reportticketreplies'
import type { ReportTableData } from '@/types/reporttabledata'
import schema from '@/views/SupportTicketsReportView.schema'

const { t, locale } = useI18n()

/** Year being reported; the current one at first. */
const year = ref(currentYear())

/** Month being reported, 1 to 12; the current one at first. */
const month = ref(currentMonthNumber())

const { data, load } = useReport<ReportTicketReplies>(() =>
  reportPath('/reports/support-ticket-replies', { year: year.value, month: month.value }),
)

/** Title naming the month once a report is loaded; the schema title until then. */
const title = computed<string | undefined>(() =>
  data.value ? t('reports.titles.supportTicketRepliesFor', { month: monthName(data.value.month), year: data.value.year }) : undefined,
)

/** Days in the reported month. */
const daysInMonth = computed<number>(() => data.value?.daysInMonth ?? 0)

/**
 * Replies per day of the month, over every day of it.
 *
 * @param total - Replies in the month.
 * @returns The average, zero for a month with no days yet.
 */
const averageOf = (total: number): number => (daysInMonth.value > 0 ? total / daysInMonth.value : 0)

/** One row per staff member with the average per day added. */
const rows = computed(() => (data.value?.rows ?? []).map(row => ({ ...row, averagePerDay: averageOf(row.total) })))

/** The table of the staff members, with the tooltips that explain the two figures and a footer holding the replies of everyone and their average per day. */
const table = computed<ReportTableData<ReportTicketReplyRow & { averagePerDay: number }>>(() => {
  /** Replies of every staff member in the month. */
  const total = (data.value?.rows ?? []).reduce((acc, row) => acc + row.total, 0)
  return {
    rows: rows.value,
    rowKey: 'adminName',
    extras: {
      total: { hint: t('reports.supportTicketsReport.totalHint') },
      averagePerDay: { hint: t('reports.supportTicketsReport.averagePerDayHint') },
    },
    totals: { total, averagePerDay: formatDecimal(averageOf(total), locale.value) },
  }
})

/**
 * Writes the tooltip of one square of the strip of a row.
 *
 * @param index - Zero based index of the day.
 * @param count - Replies made that day.
 * @returns The day and its replies.
 */
const describeDay = (index: number, count: number): string => t('reports.supportTicketsReport.dayReplies', { day: index + 1, count })
</script>

<template>
  <ReportPage v-model:year="year" v-model:month="month" :retry="load" :schema="schema" :title="title" :years-back="2" :years-ahead="2" :table="table" @generate="load">
    <template #cell-averagePerDay="{ value }">
      {{ formatDecimal(Number(value), locale) }}
    </template>
    <template #cell-dailyCounts="{ row }">
      <UiChartHeatStrip :values="row.dailyCounts" :describe="describeDay" />
    </template>
  </ReportPage>
</template>
