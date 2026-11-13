<script setup lang="ts">
/**
 * KPI tile: a caption, a large value in the display font, an optional change and an optional hint.
 *
 * Sized as the concept tile: a 13px muted caption, a 30px semibold value (drawn by `UiMetric`) and a 12px change, stacked 10px
 * apart in a card of 20px padding. `tone` colours the value (e.g. red for an overdue total). `delta` is the
 * change since the last period, already formatted with its sign ("+12%"), coloured by `deltaTone` (green for
 * good, red for bad); the sign keeps it readable without the colour. The `icon` slot sits at the right of
 * the caption and the `trend` slot under the value. The `value` slot replaces the plain value, for example
 * with a badge. `loading` keeps the caption and the hint, which are page text, and draws bars of the value and
 * change sizes in place of the numbers, so the tile keeps its height while they load.
 */
import { computed } from 'vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiMetric from '@/components/ui/UiMetric.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiText from '@/components/ui/UiText.vue'
import { toneTextClasses } from '@/utils/ui/tone'
import type { UiTone } from '@/utils/ui/tone'

/** Props for UiStatTile. */
const props = withDefaults(defineProps<{
  /** What the number measures (already translated). */
  label: string
  /** The number or text to show, already formatted. */
  value: string | number
  /** Line under the value. */
  hint?: string
  /** Colour of the value. */
  tone?: UiTone
  /** Change since the last period, already formatted with its sign. */
  delta?: string
  /** Colour of the change. */
  deltaTone?: UiTone
  /** Shows bars in place of the value and the change while the numbers load; the caption and hint stay. */
  loading?: boolean
}>(), {
  hint: undefined,
  tone: 'neutral',
  delta: undefined,
  deltaTone: 'neutral',
  loading: false,
})

/** Colour class of the change. */
const deltaClasses = computed<string>(() => toneTextClasses[props.deltaTone])
</script>

<template>
  <UiCard padding="md" :aria-busy="loading ? 'true' : undefined" class="flex flex-col gap-2.5">
    <div class="flex items-start justify-between gap-2">
      <UiText tone="muted" size="sm">
        {{ label }}
      </UiText>
      <slot name="icon" />
    </div>
    <div v-if="loading" class="flex flex-col gap-2.5" aria-hidden="true" data-testid="ui-stat-tile-skeleton">
      <UiMetric :value="value" loading />
      <UiSkeletonBar class="h-3 w-24 rounded-md" />
    </div>
    <template v-else>
      <UiMetric :value="value" :tone="tone">
        <slot name="value">{{ value }}</slot>
      </UiMetric>
      <span v-if="delta" class="text-xs tabular-nums" :class="deltaClasses" data-testid="ui-stat-tile-delta">{{ delta }}</span>
    </template>
    <UiText v-if="hint" tone="muted" size="sm">
      {{ hint }}
    </UiText>
    <slot v-if="!loading" name="trend" />
  </UiCard>
</template>
