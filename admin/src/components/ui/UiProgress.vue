<script setup lang="ts">
/**
 * A labelled progress bar: the native `progress` element, so assistive technology reads its value,
 * styled from the theme tokens. The value is a percentage clamped to 0..100, so a caller can never draw
 * past the bar; the label sits above it with the rounded percentage beside it.
 */
import { computed } from 'vue'

/** Props for UiProgress. */
const props = defineProps<{
  /** The share done, in percent; clamped to 0..100. */
  value: number
  /** What the bar measures, already translated. */
  label: string
}>()

/** The full bar, in percent. */
const FULL = 100

/** The value clamped to the bar; 0 for anything that is not a finite number. */
const percent = computed<number>(() => (Number.isFinite(props.value) ? Math.min(FULL, Math.max(0, props.value)) : 0))
</script>

<template>
  <div class="flex flex-col gap-1.5">
    <div class="flex items-center justify-between gap-2 text-sm">
      <span class="text-text-secondary">{{ label }}</span>
      <span class="font-medium tabular-nums text-text-primary" data-testid="ui-progress-value">{{ Math.round(percent) }}%</span>
    </div>
    <progress
      class="h-2 w-full overflow-hidden rounded-full bg-surface-hover [&::-moz-progress-bar]:bg-primary-500 [&::-webkit-progress-bar]:bg-surface-hover [&::-webkit-progress-value]:rounded-full [&::-webkit-progress-value]:bg-primary-500"
      :value="percent"
      :max="FULL"
      :aria-label="label"
    />
  </div>
</template>
