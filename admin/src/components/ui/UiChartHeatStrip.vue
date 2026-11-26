<script setup lang="ts">
/**
 * One row of small squares, one per day (or any step), shaded by how large its value is against the
 * largest one of the row; a square with a zero value stays flat. Each square has a tooltip written by
 * the caller, so the strip never carries text of its own. On a phone a strip longer than its box scrolls sideways inside
 * it and the squares keep their size.
 */
import { computed } from 'vue'
import UiTooltip from '@/components/ui/UiTooltip.vue'

/** Props for UiChartHeatStrip. */
const props = defineProps<{
  /** The value of each step, the first step first. */
  values: readonly number[]
  /** Writes the tooltip of a step from its zero based index and value (already translated). */
  describe: (index: number, value: number) => string
}>()

/** Opacity classes from the faintest shade to the strongest. */
const SHADES = ['opacity-30', 'opacity-50', 'opacity-75', 'opacity-100'] as const

/** The largest value of the strip, at least 1 so an all zero strip divides by something. */
const peak = computed<number>(() => Math.max(1, ...props.values))

/**
 * Picks the classes of one square.
 *
 * @param value - The value of the square.
 * @returns A flat fill for zero, the accent in a shade by the value's share of the peak otherwise.
 */
const shadeOf = (value: number): string => {
  if (value <= 0) {
    return 'bg-surface-track'
  }
  const level = Math.min(SHADES.length - 1, Math.floor((value / peak.value) * SHADES.length))
  return `bg-text-accent ${SHADES[level]}`
}
</script>

<template>
  <div class="flex max-w-full items-center gap-0.5 max-sm:overflow-x-auto" data-testid="ui-chart-heat-strip">
    <UiTooltip v-for="(value, index) in values" :key="index" :text="describe(index, value)" class="shrink-0">
      <span class="block h-4 w-2 rounded-sm" :class="shadeOf(value)" data-testid="ui-chart-heat-cell" />
    </UiTooltip>
  </div>
</template>
