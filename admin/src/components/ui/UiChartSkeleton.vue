<script setup lang="ts">
/**
 * Placeholder of a chart, drawn in the shape of its {@link ChartKind}: bars standing on the axis line for the vertical
 * bar kinds, bars of different lengths for the horizontal one, a wavy line (filled for an area), a ring, a row of
 * squares, one wide bar over its key, or a half ring.
 *
 * `UiChart` draws it in place of the plot while its data loads, inside its own box, so it fills that box. A screen
 * skeleton that has no chart to host (the skeleton of a report whose chunk is still downloading, a dashboard panel)
 * passes `height` and gets a box of the same plot height, without loading the chart library. The shape comes from the
 * same kind the real chart is drawn with, so the placeholder and the chart are the same shape by construction. The box
 * takes the same phone height as the chart (`chartHeightClassOf`), and the ring has its legend bars under it on a phone,
 * where the real legend sits.
 */
import { computed } from 'vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import { ChartKind, chartHeightClassOf, chartSkeletonBars, chartSkeletonRows, chartSkeletonSquares } from '@/utils/ui/chart'
import type { UiChartHeight } from '@/utils/ui/chart'

/** Props for UiChartSkeleton. */
const props = withDefaults(defineProps<{
  /** Shape of the chart the placeholder stands in for; vertical bars when omitted. */
  kind?: ChartKind
  /** Plot height of the box; fills the parent box when omitted. */
  height?: UiChartHeight
}>(), {
  kind: ChartKind.GroupedBar,
  height: undefined,
})

/** The box height: the chart's plot height step, or the whole parent box. */
const heightClass = computed<string>(() => (props.height === undefined ? 'h-full' : chartHeightClassOf(props.kind, props.height)))

/** Whether the kind is drawn as bars on a vertical axis. */
const isVerticalBars = computed<boolean>(() => props.kind === ChartKind.GroupedBar || props.kind === ChartKind.StackedBar)

/** Whether the kind is drawn as a line. */
const isLine = computed<boolean>(() => props.kind === ChartKind.Line || props.kind === ChartKind.Area)
</script>

<template>
  <div
    v-if="isVerticalBars"
    class="flex items-end gap-2 border-b border-border"
    :class="heightClass"
    aria-hidden="true"
    data-testid="chart-skeleton"
    :data-kind="kind"
  >
    <UiSkeletonBar v-for="(barHeight, index) in chartSkeletonBars" :key="index" class="flex-1 rounded-t-lg rounded-b-[3px]" :class="barHeight" />
  </div>
  <div
    v-else-if="kind === ChartKind.HorizontalBar"
    class="flex flex-col justify-around gap-2 border-l border-border"
    :class="heightClass"
    aria-hidden="true"
    data-testid="chart-skeleton"
    :data-kind="kind"
  >
    <UiSkeletonBar v-for="(barWidth, index) in chartSkeletonRows" :key="index" class="h-4 rounded-r-lg rounded-l-[3px]" :class="barWidth" />
  </div>
  <div
    v-else-if="isLine"
    class="flex items-center justify-center border-b border-border"
    :class="heightClass"
    aria-hidden="true"
    data-testid="chart-skeleton"
    :data-kind="kind"
  >
    <svg viewBox="0 0 100 40" preserveAspectRatio="none" class="h-full w-full motion-safe:animate-pulse">
      <path
        v-if="kind === ChartKind.Area"
        d="M0 40 L0 28 C12 22 20 30 32 20 S52 12 64 18 S86 6 100 10 L100 40 Z"
        class="fill-surface-track"
        data-testid="chart-skeleton-area"
      />
      <path
        d="M0 28 C12 22 20 30 32 20 S52 12 64 18 S86 6 100 10"
        fill="none"
        stroke-width="2"
        vector-effect="non-scaling-stroke"
        class="stroke-border-strong"
      />
    </svg>
  </div>
  <div
    v-else-if="kind === ChartKind.Donut"
    class="flex flex-col items-center justify-center gap-3"
    :class="heightClass"
    aria-hidden="true"
    data-testid="chart-skeleton"
    :data-kind="kind"
  >
    <svg viewBox="0 0 100 100" class="min-h-0 w-auto flex-1 motion-safe:animate-pulse">
      <circle cx="50" cy="50" r="38" fill="none" stroke-width="14" class="stroke-surface-track" />
    </svg>
    <!-- On a phone the real legend sits under the ring -->
    <div class="flex items-center gap-4 sm:hidden" data-testid="chart-skeleton-legend">
      <UiSkeletonBar v-for="index in 3" :key="index" class="h-3 w-16 rounded-md" />
    </div>
  </div>
  <div
    v-else-if="kind === ChartKind.HeatStrip"
    class="flex items-center gap-0.5"
    :class="heightClass"
    aria-hidden="true"
    data-testid="chart-skeleton"
    :data-kind="kind"
  >
    <UiSkeletonBar v-for="index in chartSkeletonSquares" :key="index" class="h-4 flex-1 rounded-sm" />
  </div>
  <div
    v-else-if="kind === ChartKind.Distribution"
    class="flex flex-col justify-center gap-3"
    :class="heightClass"
    aria-hidden="true"
    data-testid="chart-skeleton"
    :data-kind="kind"
  >
    <UiSkeletonBar class="h-3 w-full rounded-full" />
    <div class="flex flex-wrap items-center gap-x-4 gap-y-2">
      <UiSkeletonBar v-for="index in 3" :key="index" class="h-3 w-20 rounded-md" />
    </div>
  </div>
  <div
    v-else
    class="flex items-center justify-center"
    :class="heightClass"
    aria-hidden="true"
    data-testid="chart-skeleton"
    :data-kind="kind"
  >
    <svg viewBox="0 0 100 56" class="h-3/4 max-h-full w-auto motion-safe:animate-pulse">
      <path d="M 10 50 A 40 40 0 0 1 90 50" fill="none" stroke-width="9" stroke-linecap="round" class="stroke-surface-track" />
    </svg>
  </div>
</template>
