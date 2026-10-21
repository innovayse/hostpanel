<template>
  <component :is="as" class="ui-balanced-grid" :data-columns="columns" :data-columns-md="tabletCount">
    <slot />
  </component>
</template>

<script setup lang="ts">
/**
 * UiBalancedGrid - a grid of cards that stays balanced for any item count.
 *
 * Columns come from the item count ({@link balancedColumns}) and every row, the incomplete last
 * one included, is centred, so one, two, five or seven cards never leave an orphan against the
 * left edge. Direct children are sized to a column; they are one column on a phone and at most
 * two on a tablet. Children stretch to the tallest in their row. The column counts reach the stylesheet
 * (`assets/css/ui.css`) as `data-columns` and `data-columns-md` attributes, so the component carries
 * no inline style.
 */
import { computed } from 'vue'
import { balancedColumns, tabletColumns } from '~/utils/balancedGrid'

/** Props of UiBalancedGrid. */
interface Props {
  /** Number of items the slot renders; decides the column count. */
  count: number
  /** Tag to render (use `ul` for a list of cards). */
  as?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), { as: 'div' })

/** Columns on a desktop screen. */
const columns = computed(() => balancedColumns(props.count))

/** Columns on a tablet screen. */
const tabletCount = computed(() => tabletColumns(props.count))
</script>
