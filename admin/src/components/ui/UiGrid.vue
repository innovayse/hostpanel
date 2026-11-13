<script setup lang="ts">
/**
 * Grid layout primitive: equal columns that collapse to one on narrow screens. `minItemWidth` replaces the fixed
 * column count with as many columns, at least that wide, as fit the row, so the cards always fill the width.
 */
import { computed } from 'vue'
import { gapClasses, gridColumnClasses, gridMinWidthClasses } from '@/utils/ui/layout'
import type { UiGap, UiGridColumns, UiGridMinWidth } from '@/utils/ui/layout'

/** Props for UiGrid. */
const props = withDefaults(defineProps<{
  /** Column count on wide screens. */
  columns?: UiGridColumns
  /** Fills the row with as many columns of at least this width as fit; replaces `columns`. */
  minItemWidth?: UiGridMinWidth
  /** Space between cells. */
  gap?: UiGap
}>(), {
  columns: 2,
  minItemWidth: undefined,
  gap: 'md',
})

/** Layout classes. */
const classes = computed<string[]>(() => [props.minItemWidth ? gridMinWidthClasses[props.minItemWidth] : gridColumnClasses[props.columns], gapClasses[props.gap]])
</script>

<template>
  <div class="grid" :class="classes">
    <slot />
  </div>
</template>
