<template>
  <component :is="as" :class="classes">
    <slot />
  </component>
</template>

<script setup lang="ts">
/**
 * UiGrid: a responsive grid with preset column counts.
 *
 * The presets carry their own breakpoints (`3` is one column on phones, two from `sm`, three
 * from `lg`), so a screen never repeats `grid-cols-1 sm:grid-cols-2 lg:grid-cols-3`.
 */
import { computed } from 'vue'
import { UiGap, UiGridColumns } from '~/utils/ui/enums'

/** Props of UiGrid. */
interface Props {
  /** Tag to render (use `ul` for a list of cards). */
  as?: string
  /** Responsive column preset. */
  cols?: `${UiGridColumns}`
  /** Space between cells. */
  gap?: `${UiGap}`
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  as: 'div',
  cols: UiGridColumns.Two,
  gap: UiGap.Md,
})

/** Column utilities per preset. */
const COLUMN_CLASSES: Record<UiGridColumns, string> = {
  [UiGridColumns.One]: 'grid-cols-1',
  [UiGridColumns.Two]: 'grid-cols-1 sm:grid-cols-2',
  [UiGridColumns.Three]: 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3',
  [UiGridColumns.Four]: 'grid-cols-2 lg:grid-cols-4',
  [UiGridColumns.Auto]: 'grid-cols-[repeat(auto-fit,minmax(min(100%,16rem),1fr))]',
}

/** Gap utility per step. */
const GAP_CLASSES: Record<UiGap, string> = {
  [UiGap.None]: 'gap-0',
  [UiGap.Xs]: 'gap-2',
  [UiGap.Sm]: 'gap-3',
  [UiGap.Md]: 'gap-4',
  [UiGap.Lg]: 'gap-6',
  [UiGap.Xl]: 'gap-10',
}

/** Class list of the root element. */
const classes = computed(() => [
  'grid',
  COLUMN_CLASSES[props.cols as UiGridColumns],
  GAP_CLASSES[props.gap as UiGap],
].join(' '))
</script>
