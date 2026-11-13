<template>
  <component :is="as" :class="classes" :data-columns="columns">
    <slot />
  </component>
</template>

<script setup lang="ts">
/**
 * UiBalancedGrid: a grid of cards that stays balanced for any item count.
 *
 * Columns come from the item count ({@link balancedColumns}) and every row, the incomplete last
 * one included, is centred, so one, two, five or seven cards never leave an orphan against the
 * left edge. Direct children are sized to a column; they are one column on a phone and at most
 * two on a tablet. Children stretch to the tallest in their row.
 */
import { computed } from 'vue'
import { BALANCED_MAX_COLUMNS, BALANCED_TABLET_COLUMNS, balancedColumns, tabletColumns } from '~/utils/balancedGrid'

/** Props of UiBalancedGrid. */
interface Props {
  /** Number of items the slot renders; decides the column count. */
  count: number
  /** Tag to render (use `ul` for a list of cards). */
  as?: string
  /**
   * The fewest columns the card width is computed for. A grid of one or two cards then keeps the
   * width of a card in a fuller grid and is centred, instead of stretching across the container.
   */
  minColumns?: number
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), { as: 'div', minColumns: 1 })

/** Columns on a desktop screen, never fewer than the `minColumns` the card width is kept for (capped at the widest row). */
const columns = computed(() => Math.min(BALANCED_MAX_COLUMNS, Math.max(balancedColumns(props.count), props.minColumns)))

/** Columns on a tablet-width screen, held at `minColumns` too, so a lone card is not stretched there either. */
const tablet = computed(() => Math.min(BALANCED_TABLET_COLUMNS, Math.max(tabletColumns(props.count), props.minColumns)))

/**
 * Classes that size a child to one column on a tablet-width screen, keyed by column count.
 *
 * Each size is written twice: for a direct child, and for the child of a `contents` wrapper such
 * as `UiAccent`. A `display: contents` element is not a flex item, so a width set on it would
 * never reach the card inside it and the card would size to its content instead.
 */
const TABLET_CHILD_CLASSES: Record<number, string> = {
  1: 'sm:[&>*]:basis-full sm:[&>.contents>*]:basis-full',
  2: 'sm:[&>*]:basis-[calc((100%-1.5rem)/2)] sm:[&>.contents>*]:basis-[calc((100%-1.5rem)/2)]',
}

/** Classes that size a child to one column on a desktop screen, keyed by column count (wrappers included, as above). */
const DESKTOP_CHILD_CLASSES: Record<number, string> = {
  1: 'lg:[&>*]:basis-full lg:[&>.contents>*]:basis-full',
  2: 'lg:[&>*]:basis-[calc((100%-1.5rem)/2)] lg:[&>.contents>*]:basis-[calc((100%-1.5rem)/2)]',
  3: 'lg:[&>*]:basis-[calc((100%-3rem)/3)] lg:[&>.contents>*]:basis-[calc((100%-3rem)/3)]',
  4: 'lg:[&>*]:basis-[calc((100%-4.5rem)/4)] lg:[&>.contents>*]:basis-[calc((100%-4.5rem)/4)]',
}

/** Classes of the root: a centred wrapping row whose children are sized to the column count. */
const classes = computed(() => [
  'ui-balanced-grid flex flex-wrap justify-center gap-6 [&>*]:min-w-0 [&>*]:flex-none [&>*]:basis-full',
  '[&>.contents>*]:min-w-0 [&>.contents>*]:flex-none [&>.contents>*]:basis-full',
  TABLET_CHILD_CLASSES[tablet.value],
  DESKTOP_CHILD_CLASSES[columns.value],
])
</script>
