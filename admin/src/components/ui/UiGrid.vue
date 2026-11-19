<script setup lang="ts">
/**
 * Grid layout primitive: equal columns that collapse to one on narrow screens. `minItemWidth` replaces the fixed
 * column count with as many columns, at least that wide, as fit the row, so the cards always fill the width. `paired`
 * keeps two columns on a phone instead of one, for small tiles (a row of key figures). `container` takes the column
 * count from the width of the nearest `@container` ancestor rather than the screen, for a grid inside a card.
 */
import { computed } from 'vue'
import { containerGridColumnClasses, gapClasses, gridColumnClasses, gridMinWidthClasses, pairedGridColumnClasses } from '@/utils/ui/layout'
import type { UiGap, UiGridColumns, UiGridMinWidth } from '@/utils/ui/layout'

/** Props for UiGrid. */
const props = withDefaults(defineProps<{
  /** Column count on wide screens. */
  columns?: UiGridColumns
  /** Fills the row with as many columns of at least this width as fit; replaces `columns`. */
  minItemWidth?: UiGridMinWidth
  /** Keeps two columns on a phone instead of one; for small tiles. */
  paired?: boolean
  /** Counts the columns against the width of the nearest `@container` ancestor instead of the screen. */
  container?: boolean
  /** Space between cells. */
  gap?: UiGap
}>(), {
  columns: 2,
  minItemWidth: undefined,
  paired: false,
  container: false,
  gap: 'md',
})

/**
 * The column set the grid counts its columns by: the screen's, a paired phone grid's or the container's.
 *
 * @returns The class record to read the column count from.
 */
const columnClasses = computed<Record<UiGridColumns, string>>(() => {
  if (props.container) {
    return containerGridColumnClasses
  }
  return props.paired ? pairedGridColumnClasses : gridColumnClasses
})

/** Layout classes. */
const classes = computed<string[]>(() => [
  props.minItemWidth ? gridMinWidthClasses[props.minItemWidth] : columnClasses.value[props.columns],
  gapClasses[props.gap],
])
</script>

<template>
  <div class="grid" :class="classes">
    <slot />
  </div>
</template>
