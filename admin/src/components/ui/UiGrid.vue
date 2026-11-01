<script setup lang="ts">
/**
 * Grid layout primitive: equal columns that collapse to one on narrow screens.
 */
import { computed } from 'vue'
import { containerGridColumnClasses, gapClasses, gridColumnClasses } from '@/utils/ui/layout'
import type { UiGap, UiGridColumns } from '@/utils/ui/layout'

/** Props for UiGrid. */
const props = withDefaults(defineProps<{
  /** Column count on wide screens. */
  columns?: UiGridColumns
  /** Space between cells. */
  gap?: UiGap
  /** Follows the width of the nearest `@container` ancestor, not the viewport (filters inside a card). */
  container?: boolean
}>(), {
  columns: 2,
  gap: 'md',
  container: false,
})

/** Layout classes. */
const classes = computed<string[]>(() => [
  (props.container ? containerGridColumnClasses : gridColumnClasses)[props.columns],
  gapClasses[props.gap],
])
</script>

<template>
  <div class="grid" :class="classes">
    <slot />
  </div>
</template>
