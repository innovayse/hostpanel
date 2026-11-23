<script setup lang="ts">
/**
 * Flex layout primitive: children in a column or a row with a chosen gap.
 *
 * Replaces hand-written `flex flex-col gap-4` wrappers so spacing comes from one scale.
 */
import { computed } from 'vue'
import { alignItemsClasses, gapClasses, hiddenFromClasses, justifyClasses, minWidthClasses, reflowClasses, UiDirection, UiStackTag, visibleFromClasses } from '@/utils/ui/layout'
import type { UiAlignItems, UiBreakpoint, UiGap, UiJustify, UiWidth } from '@/utils/ui/layout'

/** Props for UiStack. */
const props = withDefaults(defineProps<{
  /** Element the stack is drawn as; a `div` by default, a landmark or list element where the markup calls for one (a `li` inside a `ul` stack). */
  as?: UiStackTag
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
  /** Keeps the stack from shrinking below this width, so stacks side by side (a column of amounts) line up. */
  minWidth?: UiWidth
  /** Shows the stack only from this breakpoint up; below it the stack and its children take no space. */
  visibleFrom?: UiBreakpoint
  /** Shows the stack only below this breakpoint; from it up the stack and its children take no space. */
  hiddenFrom?: UiBreakpoint
}>(), {
  as: UiStackTag.Div,
  direction: UiDirection.Column,
  gap: 'md',
  align: 'stretch',
  justify: 'start',
  wrap: false,
  reflowBelowMd: false,
  minWidth: undefined,
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
  props.minWidth === undefined ? '' : minWidthClasses[props.minWidth],
])
</script>

<template>
  <component :is="as" :class="classes">
    <slot />
  </component>
</template>
