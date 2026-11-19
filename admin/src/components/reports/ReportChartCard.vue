<script setup lang="ts">
/**
 * Card around a report chart, with an optional title and, beside it, the controls the screen puts in the `actions` slot.
 *
 * The chart itself is `UiChart`: this component only frames it, so a report never configures the
 * chart library. The kind and height come from the `chart` entry of the report's skeleton schema, the same entry the
 * skeleton is drawn from, so the loading placeholder always has the shape of the chart. While the report loads the card keeps its title and the chart draws the placeholder of its kind at its height.
 * A chart with nothing to plot never draws an empty plot area: the page shows its one empty state when
 * every part is empty, otherwise the card is replaced by the compact empty state in its place. On a phone the controls
 * wrap under the title and the chart takes the phone height of its kind.
 */
import { computed, inject } from 'vue'
import ReportEmptyState from '@/components/reports/ReportEmptyState.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiChart from '@/components/ui/UiChart.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { isReportChartEmpty, useReportPartEmpty } from '@/lib/reportEmpty'
import { NOT_LOADING, REPORT_LOADING } from '@/lib/reportLoading'
import { UiChartHeight } from '@/utils/ui/chart'
import type { UiChartSeries } from '@/utils/ui/chart'
import type { SkeletonChart } from '@/utils/ui/skeletonSchema'

/** Props for ReportChartCard. */
const props = withDefaults(defineProps<{
  /** Caption above the chart (already translated). */
  title?: string
  /** The chart of the report's schema: its kind and plot height. */
  chart: SkeletonChart
  /** Category labels (or slice names for a donut). */
  labels: string[]
  /** Data series. */
  series: UiChartSeries[]
  /** Formats numbers in tooltips and on the value axis. */
  valueFormatter?: (value: number) => string
  /** Shows the legend; by default only when it says something. */
  legend?: boolean
}>(), {
  title: undefined,
  valueFormatter: undefined,
  legend: undefined,
})

/** Slots of the card. */
defineSlots<{
  /** Controls beside the title, at the end of the header (a button that clears a selection on the chart). */
  actions?: () => unknown
}>()

/** True while the report loads: the chart draws the placeholder of its kind at its plot height. */
const loading = inject(REPORT_LOADING, NOT_LOADING)

/** Whether there is nothing to plot; never while the report loads, when the chart draws its skeleton. */
const empty = computed<boolean>(() => !loading.value && isReportChartEmpty(props.labels, props.series))

/** True when the page shows the single empty state for the whole report, so this card draws nothing. */
const collapsed = useReportPartEmpty(empty)
</script>

<template>
  <template v-if="collapsed" />
  <ReportEmptyState v-else-if="empty" testid="report-chart-empty" />
  <UiCard v-else padding="md" :aria-busy="loading ? 'true' : undefined" data-testid="report-chart">
    <template v-if="title" #header>
      <UiStack direction="row" justify="between" align="center" wrap class="mb-4">
        <UiHeading :level="2" variant="card">
          {{ title }}
        </UiHeading>
        <slot name="actions" />
      </UiStack>
    </template>
    <UiChart
      :kind="chart.kind"
      :labels="labels"
      :series="series"
      :value-formatter="valueFormatter"
      :legend="legend"
      :height="chart.height ?? UiChartHeight.Md"
      :label="title"
      :loading="loading"
    />
  </UiCard>
</template>
