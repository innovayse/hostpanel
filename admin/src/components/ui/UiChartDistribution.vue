<script setup lang="ts">
/**
 * One stacked bar showing how a total splits into toned parts, each part as wide as its share. A part
 * with nothing in it is not drawn; a bar with nothing at all shows a dash. Each part has a tooltip
 * written by the caller. In a table cell it is a fixed 10rem; `wide` lets it fill the width of a chart card, and on a
 * phone it keeps a width the parts stay readable at, scrolling sideways inside the card when the card is narrower.
 */
import { computed } from 'vue'
import UiTooltip from '@/components/ui/UiTooltip.vue'
import { EMPTY_VALUE } from '@/lib/format'
import type { UiChartPart } from '@/utils/ui/chart'
import { toneDotClasses } from '@/utils/ui/tone'

/** Props for UiChartDistribution. */
const props = withDefaults(defineProps<{
  /** The parts in drawing order. */
  parts: readonly UiChartPart[]
  /** Fills the width of the parent instead of the fixed width of a table cell. */
  wide?: boolean
}>(), {
  wide: false,
})

/** Sum of every part. */
const total = computed<number>(() => props.parts.reduce((sum, part) => sum + part.count, 0))
</script>

<template>
  <div v-if="total > 0" :class="wide ? 'w-full max-sm:overflow-x-auto' : 'w-40'" data-testid="ui-chart-distribution">
    <div class="flex h-3 w-full overflow-hidden rounded-full bg-surface-track" :class="wide ? 'max-sm:min-w-72' : ''">
      <template v-for="(part, index) in parts" :key="index">
        <UiTooltip v-if="part.count > 0" :text="part.tooltip" class="h-full" :style="{ width: `${(part.count / total) * 100}%` }">
          <span class="block h-full w-full" :class="toneDotClasses[part.tone]" data-testid="ui-chart-distribution-part" />
        </UiTooltip>
      </template>
    </div>
  </div>
  <span v-else class="text-text-muted">{{ EMPTY_VALUE }}</span>
</template>
