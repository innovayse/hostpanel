<script setup lang="ts">
/**
 * A half ring showing one value against its scale, with the figure and its caption under the arc.
 *
 * The arc is drawn from the theme tokens (`--chart-1` for the value, the hover surface for the track), so a theme
 * switch recolours it without a redraw. The value is clamped to the scale, and the figure is always written, so the
 * state is never carried by colour alone; assistive technology reads it as a meter.
 */
import { computed } from 'vue'

/** Props for UiChartGauge. */
const props = defineProps<{
  /** The measured value. */
  value: number
  /** The full scale; the value is drawn as its share. */
  max: number
  /** What the gauge measures (already translated). */
  label: string
  /** The figure written under the arc (already formatted). */
  text: string
}>()

/** The arc is drawn in a path of length 100 so a share maps straight onto the dash. */
const ARC_LENGTH = 100

/** The share of the scale the value fills, 0 to 100; 0 for a scale that is not a positive number. */
const percent = computed<number>(() => {
  if (!Number.isFinite(props.value) || !Number.isFinite(props.max) || props.max <= 0) {
    return 0
  }
  return Math.min(ARC_LENGTH, Math.max(0, (props.value / props.max) * ARC_LENGTH))
})

/** The dash pattern that fills the arc up to the share. */
const dash = computed<string>(() => `${percent.value} ${ARC_LENGTH}`)
</script>

<template>
  <div
    class="flex h-full flex-col items-center justify-center gap-1"
    role="meter"
    :aria-label="label"
    aria-valuemin="0"
    :aria-valuemax="max"
    :aria-valuenow="value"
    :aria-valuetext="text"
    data-testid="ui-chart-gauge"
  >
    <svg viewBox="0 0 100 56" class="h-3/4 max-h-full w-auto max-w-full" aria-hidden="true">
      <path d="M 10 50 A 40 40 0 0 1 90 50" pathLength="100" fill="none" stroke-width="9" stroke-linecap="round" class="stroke-surface-track" />
      <path
        d="M 10 50 A 40 40 0 0 1 90 50"
        pathLength="100"
        fill="none"
        stroke-width="9"
        stroke-linecap="round"
        class="stroke-(--chart-1)"
        :stroke-dasharray="dash"
        data-testid="ui-chart-gauge-arc"
      />
    </svg>
    <span class="text-lg font-semibold tabular-nums text-text-primary" data-testid="ui-chart-gauge-text">{{ text }}</span>
    <span class="text-xs text-text-secondary">{{ label }}</span>
  </div>
</template>
