<template>
  <div :class="rootClasses">
    <div :class="startClasses" data-testid="ui-split-start">
      <slot name="start" />
    </div>
    <div :class="endClasses" data-testid="ui-split-end">
      <slot name="end" />
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * UiSplit: a two-part layout (content and a side panel) that stacks on small screens.
 *
 * Slots `start` and `end` are the two halves; `ratio` decides which one is twice as wide, and
 * `reverse` swaps their order from `lg` up (the DOM order stays start, end for reading order).
 */
import { computed } from 'vue'
import { UiGap, UiSplitRatio } from '~/utils/ui/enums'

/** Props of UiSplit. */
interface Props {
  /** Width ratio of the two halves. */
  ratio?: `${UiSplitRatio}`
  /** Space between the halves. */
  gap?: `${UiGap}`
  /** Shows `end` before `start` from `lg` up. */
  reverse?: boolean
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  ratio: UiSplitRatio.Equal,
  gap: UiGap.Lg,
  reverse: false,
})

/** Gap utility per step. */
const GAP_CLASSES: Record<UiGap, string> = {
  [UiGap.None]: 'gap-0',
  [UiGap.Xs]: 'gap-2',
  [UiGap.Sm]: 'gap-3',
  [UiGap.Md]: 'gap-4',
  [UiGap.Lg]: 'gap-6',
  [UiGap.Xl]: 'gap-10',
}

/** Class list of the grid. */
const rootClasses = computed(() => [
  'grid grid-cols-1',
  props.ratio === UiSplitRatio.Equal ? 'lg:grid-cols-2' : 'lg:grid-cols-3',
  GAP_CLASSES[props.gap as UiGap],
].join(' '))

/** Class list of the first half. */
const startClasses = computed(() => [
  'min-w-0',
  props.ratio === UiSplitRatio.StartWide ? 'lg:col-span-2' : '',
  props.reverse ? 'lg:order-2' : '',
].filter(Boolean).join(' '))

/** Class list of the second half. */
const endClasses = computed(() => [
  'min-w-0',
  props.ratio === UiSplitRatio.EndWide ? 'lg:col-span-2' : '',
  props.reverse ? 'lg:order-1' : '',
].filter(Boolean).join(' '))
</script>
