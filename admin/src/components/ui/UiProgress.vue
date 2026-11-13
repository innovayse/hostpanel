<script setup lang="ts">
/**
 * A labelled progress bar: the native `progress` element, so assistive technology reads its value,
 * styled from the theme tokens: an 8px track with 6px corners, filled with the blue to violet gradient, which
 * turns to the amber to red one once it reaches `warnAt` percent (a capacity bar nearly full). `loading`
 * draws a label bar and a track bar of the same sizes in place of the numbers. The percentage beside the label
 * (or the caller's `valueText`) is always shown, so the state is never carried by colour alone. The value is a percentage clamped to 0..100, so a caller can never draw
 * past the bar; the label sits above it with the rounded percentage beside it.
 */
import { computed } from 'vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'

/** Props for UiProgress. */
const props = withDefaults(defineProps<{
  /** The share done, in percent; clamped to 0..100. */
  value: number
  /** What the bar measures, already translated. */
  label: string
  /** Caption shown on the right instead of the percentage, for example "12 / 100"; the bar still draws `value`. */
  valueText?: string
  /** The percentage from which the bar turns to the warning colour. */
  warnAt?: number
  /** Shows placeholder bars in place of the label, the percentage and the fill. */
  loading?: boolean
}>(), {
  warnAt: 85,
  loading: false,
})

/** The full bar, in percent. */
const FULL = 100

/** The value clamped to the bar; 0 for anything that is not a finite number. */
const percent = computed<number>(() => (Number.isFinite(props.value) ? Math.min(FULL, Math.max(0, props.value)) : 0))

/** True once the bar has reached the warning threshold. */
const warning = computed<boolean>(() => percent.value >= props.warnAt)

/** Fill classes of the bar: the gradient normally, the warning colour past the threshold. */
const fillClasses = computed<string>(() => (warning.value
  ? '[&::-moz-progress-bar]:bg-[image:var(--gradient-bar-warn)] [&::-webkit-progress-value]:bg-[image:var(--gradient-bar-warn)]'
  : '[&::-moz-progress-bar]:bg-[image:var(--gradient-bar)] [&::-webkit-progress-value]:bg-[image:var(--gradient-bar)]'))
</script>

<template>
  <div v-if="loading" class="flex flex-col gap-1.5" aria-busy="true" data-testid="ui-progress-skeleton">
    <div class="flex items-center justify-between gap-2" aria-hidden="true">
      <UiSkeletonBar class="h-3.5 w-24 rounded-md" />
      <UiSkeletonBar class="h-3.5 w-8 rounded-md" />
    </div>
    <UiSkeletonBar class="h-2 w-full rounded-md" aria-hidden="true" />
  </div>
  <div v-else class="flex flex-col gap-1.5">
    <div class="flex items-center justify-between gap-2 text-[0.8125rem]">
      <span class="text-text-primary">{{ label }}</span>
      <span class="tabular-nums text-text-muted" data-testid="ui-progress-value">{{ valueText ?? `${Math.round(percent)}%` }}</span>
    </div>
    <progress
      class="h-2 w-full overflow-hidden rounded-md bg-surface-hover [&::-webkit-progress-bar]:rounded-md [&::-webkit-progress-bar]:bg-surface-hover [&::-webkit-progress-value]:rounded-md"
      :class="fillClasses"
      :data-state="warning ? 'warning' : 'normal'"
      :value="percent"
      :max="FULL"
      :aria-label="label"
    />
  </div>
</template>
