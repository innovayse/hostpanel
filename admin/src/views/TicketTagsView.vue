<script setup lang="ts">
/**
 * Ticket tags report: how many tickets carry each tag in a date range, as a bar chart and a table. The title, the
 * filter captions and the table columns are the ones of its schema, which the skeleton draws too.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import { useReport } from '@/composables/useReport'
import { reportPath } from '@/lib/reportsQuery'
import { DateRangePreset, presetIsoRange } from '@/utils/ui/datePicker'
import type { ReportChartData } from '@/types/reportchartdata'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportTagRow, ReportTags } from '@/types/reporttags'
import type { ReportTableData } from '@/types/reporttabledata'
import { ReportFieldWidth } from '@/utils/ui/reportField'
import schema from '@/views/TicketTagsView.schema'

const { t } = useI18n()

/** Width of the date range filter. */
const FILTER_WIDTHS = { dateRange: ReportFieldWidth.Grow }

/** The period to report; the current year at first. */
const dateRange = ref<[string, string] | null>(presetIsoRange(DateRangePreset.ThisYear))

const { data, load } = useReport<ReportTags>(() =>
  reportPath('/reports/ticket-tags', { from: dateRange.value?.[0], to: dateRange.value?.[1] }),
)

/** The tags with their ticket counts; none until the report is read. */
const rows = computed<ReportTagRow[]>(() => data.value?.rows ?? [])

/** The table of the tags, with the tooltip that explains the count. */
const table = computed<ReportTableData<ReportTagRow>>(() => ({
  rows: rows.value,
  rowKey: 'tag',
  extras: { count: { format: ReportCellFormat.Integer, hint: t('reports.ticketTags.countHint') } },
}))

/** The bar chart: the ticket count per tag. */
const chart = computed<ReportChartData>(() => ({
  labels: rows.value.map(row => row.tag),
  series: [{ label: t('reports.ticketTags.count'), data: rows.value.map(row => row.count) }],
}))
</script>

<template>
  <ReportPage :retry="load" :schema="schema" :widths="FILTER_WIDTHS" :chart="chart" :table="table" @generate="load">
    <template #field-dateRange>
      <UiDateRangePicker v-model="dateRange" :placeholder="t('shared.selectDateRange')" />
    </template>
  </ReportPage>
</template>
