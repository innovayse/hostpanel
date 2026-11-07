<script setup lang="ts">
/**
 * Flex layout primitive: children in a column or a row with a chosen gap.
 *
 * Replaces hand-written `flex flex-col gap-4` wrappers so spacing comes from one scale.
 */
import { computed } from 'vue'
import { alignItemsClasses, gapClasses, hiddenFromClasses, justifyClasses, reflowClasses, UiDirection, visibleFromClasses } from '@/utils/ui/layout'
import type { UiAlignItems, UiBreakpoint, UiGap, UiJustify } from '@/utils/ui/layout'

/** Props for UiStack. */
const props = withDefaults(defineProps<{
  /** Main axis: `column` stacks vertically, `row` lays out horizontally. */
  direction?: UiDirection
  /** Space between children. */
  gap?: UiGap
  /** Cross-axis alignment. */
  align?: UiAlignItems
  /** Main-axis distribution. */
  justify?: UiJustify
  /** Lets a row wrap onto several lines. */
  wrap?: boolean
  /**
   * Turns the stack the other way below the medium breakpoint, so `direction` is what it is from `md` up.
   * A row becomes a column whose children keep their full width; a column becomes a line that wraps, its
   * children centred on the cross axis.
   */
  reflowBelowMd?: boolean
  /** Shows the stack only from this breakpoint up; below it the stack and its children take no space. */
  visibleFrom?: UiBreakpoint
  /** Shows the stack only below this breakpoint; from it up the stack and its children take no space. */
  hiddenFrom?: UiBreakpoint
}>(), {
  direction: UiDirection.Column,
  gap: 'md',
  align: 'stretch',
  justify: 'start',
  wrap: false,
  reflowBelowMd: false,
  visibleFrom: undefined,
  hiddenFrom: undefined,
})

/**
 * Display classes: plain `flex`, or the breakpoint pair that replaces it.
 *
 * @returns The one display class set the stack carries.
 */
const displayClasses = computed<string>(() => {
  if (props.visibleFrom !== undefined) {
    return visibleFromClasses[props.visibleFrom]
  }
  if (props.hiddenFrom !== undefined) {
    return hiddenFromClasses[props.hiddenFrom]
  }
  return 'flex'
})

/** Layout classes. */
const classes = computed<string[]>(() => [
  displayClasses.value,
  props.direction === UiDirection.Row ? 'flex-row' : 'flex-col',
  gapClasses[props.gap],
  alignItemsClasses[props.align],
  justifyClasses[props.justify],
  props.wrap ? 'flex-wrap' : '',
  props.reflowBelowMd ? reflowClasses[props.direction] : '',
])
</script>

<template>
  <div :class="classes">
    <slot />
  </div>
</template>
