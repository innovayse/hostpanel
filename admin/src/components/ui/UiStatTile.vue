<script setup lang="ts">
/**
 * KPI tile: a caption, a large value and an optional hint.
 *
 * `tone` colours the value (e.g. red for an overdue total). The `icon` slot sits at the right of
 * the caption and the `trend` slot under the value. The `value` slot replaces the plain value, for example with a badge.
 */
import { computed } from 'vue'
import UiCard from '@/components/ui/UiCard.vue'
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
}>(), {
  hint: undefined,
  tone: 'neutral',
})

/** Colour class of the value. */
const valueClasses = computed<string>(() => toneTextClasses[props.tone])
</script>

<template>
  <UiCard padding="md">
    <div class="flex items-start justify-between gap-2">
      <UiText tone="secondary" size="xs" class="uppercase tracking-[0.08em] font-semibold">
        {{ label }}
      </UiText>
      <slot name="icon" />
    </div>
    <p class="mt-2 font-display text-[1.6rem] font-bold leading-none tabular-nums" :class="valueClasses">
      <slot name="value">{{ value }}</slot>
    </p>
    <UiText v-if="hint" tone="secondary" size="sm" class="mt-1.5">
      {{ hint }}
    </UiText>
    <slot name="trend" />
  </UiCard>
</template>
