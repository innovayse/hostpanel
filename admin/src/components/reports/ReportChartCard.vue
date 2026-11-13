<script setup lang="ts">
/**
 * Card around a report chart, with an optional title.
 *
 * The chart itself is `UiChart`: this component only frames it, so a report never configures the
 * chart library.
 */
import { inject } from 'vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiChart from '@/components/ui/UiChart.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import { NOT_LOADING, REPORT_LOADING } from '@/lib/reportLoading'
import type { UiChartHeight, UiChartSeries, UiChartType } from '@/utils/ui/chart'

/** Props for ReportChartCard. */
withDefaults(defineProps<{
  /** Caption above the chart (already translated). */
  title?: string
  /** Kind of chart. */
  type: UiChartType
  /** Category labels (or slice names for a pie). */
  labels: string[]
  /** Data series. */
  series: UiChartSeries[]
  /** Formats numbers in tooltips and on the value axis. */
  valueFormatter?: (value: number) => string
  /** Shows the legend; by default only when it says something. */
  legend?: boolean
  /** Draws bars horizontally. */
  horizontal?: boolean
  /** Height of the plot area. */
  height?: UiChartHeight
}>(), {
  title: undefined,
  valueFormatter: undefined,
  legend: undefined,
  horizontal: false,
  height: 'md',
})

/** True while the report loads: the card keeps its frame and draws a skeleton. */
const loading = inject(REPORT_LOADING, NOT_LOADING)
</script>

<template>
  <UiCard padding="md" :loading="loading" :skeleton-lines="8" data-testid="report-chart">
    <template v-if="title" #header>
      <UiHeading :level="2" variant="card" class="mb-4">
        {{ title }}
      </UiHeading>
    </template>
    <UiChart
      :type="type"
      :labels="labels"
      :series="series"
      :value-formatter="valueFormatter"
      :legend="legend"
      :horizontal="horizontal"
      :height="height"
    />
  </UiCard>
</template>
