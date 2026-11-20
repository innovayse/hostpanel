<script setup lang="ts">
/**
 * Key of a distribution bar: one coloured dot and caption per toned part, in the order the bar draws them.
 */
import { toneDotClasses } from '@/utils/ui/tone'
import type { UiTone } from '@/utils/ui/tone'

/** Props for UiChartLegend. */
defineProps<{
  /** The entries: a tone and its caption (already translated). */
  items: readonly { tone: UiTone; label: string }[]
}>()
</script>

<template>
  <ul class="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-text-secondary" data-testid="ui-chart-legend">
    <li v-for="item in items" :key="item.label" class="inline-flex items-center gap-1.5">
      <span class="h-2.5 w-2.5 rounded-full" :class="toneDotClasses[item.tone]" aria-hidden="true" />
      {{ item.label }}
    </li>
  </ul>
</template>
