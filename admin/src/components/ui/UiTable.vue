<script setup lang="ts">
/**
 * Data table root.
 *
 * Draws the `<table>` and shares its `density` with the header and data cells (set the padding
 * once here instead of on every cell). `loading` keeps the screen's real header, makes `UiTableBody` and `UiTableFoot`
 * render nothing (a screen needs no `v-if` on its rows) and draws `skeletonRows`
 * placeholder rows under it, one bar per header column (the headers register themselves, so a column hidden below a
 * breakpoint or fixed in width is drawn the same way) at the real row height and cell
 * padding, so the page does not change shape when the data arrives; `empty` appends a full-width status row, so a
 * screen needs no separate "no rows" markup; the `empty` slot replaces the default message and brings its own spacing (a `UiEmptyState` fits as it is, without
 * the cell padding doubling its own).
 *
 * The table is drawn inside the glass card of its screen, which supplies the surface. It sits in a box that
 * scrolls sideways inside the card at every width, so a wide table never breaks out of it. With `maxHeight` the
 * box also scrolls vertically past that height, and the header (see `UiTableHead`) sticks to the top of the box;
 * a box that only scrolls sideways has nothing to stick to. A page-level list, which must not scroll inside
 * the page, sets `scrollable="false"` instead: it fits the card by hiding secondary columns (`hideBelow`), and its
 * header sticks to the page's scroll container. A box that scrolls also clips anything that opens below a cell, so
 * an editable table whose cells hold a dropdown turns it off the same way (which also drops `maxHeight`).
 *
 * `selection` (from `useRowSelection`) makes the table selectable: the `UiTableRow` of the header draws a select-all checkbox
 * (indeterminate over a partial selection) and every body row that has a `rowId` draws its own, so a screen writes no checkbox column.
 * The rows toggle through the selection (Shift extends a range); the bulk actions live in a `UiBulkBar`.
 */
import { computed, provide, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import { UI_TABLE_CONTEXT, UiTableDensity } from '@/utils/ui/injection'
import type { UiTableColumn } from '@/utils/ui/injection'
import type { RowSelection } from '@/composables/useRowSelection'
import { cellDensityClasses, columnClasses, scrollBoxClasses, stickyHeadFill, tableMaxHeightClasses } from '@/utils/ui/table'
import type { UiTableMaxHeight } from '@/utils/ui/table'

/** The attributes a screen sets (classes, test ids) belong on the table; the box around it only scrolls. */
defineOptions({ inheritAttrs: false })

const { t } = useI18n()

/** Props for UiTable. */
const props = withDefaults(defineProps<{
  /** Padding of the cells (comfortable by default so header and cells always share one padding); `plain` leaves it to the screen's classes. */
  density?: UiTableDensity
  /** Draws placeholder rows under the real header while data loads. */
  loading?: boolean
  /** Shows the empty message under the header (set when there are no rows). */
  empty?: boolean
  /** Number of placeholder rows drawn while `loading`. */
  skeletonRows?: number
  /** Whether the table scrolls inside its own box; off for a table whose cells open dropdowns. */
  scrollable?: boolean
  /** Height past which the box scrolls vertically and the header sticks to it; without it the table grows with its rows. */
  maxHeight?: UiTableMaxHeight
  /** Makes the table selectable: the selection its checkbox column reads and changes. */
  selection?: RowSelection
  /** `data-testid` of the select-all checkbox of a selectable table. */
  selectAllTestid?: string
}>(), {
  density: UiTableDensity.Comfortable,
  loading: false,
  empty: false,
  skeletonRows: 5,
  scrollable: true,
  maxHeight: undefined,
  selection: undefined,
  selectAllTestid: 'ui-table-select-all',
})

/** Indexes of the placeholder rows. */
const rowIndexes = computed<number[]>(() => Array.from({ length: props.skeletonRows }, (_, index) => index))

/** Getters of the registered header columns, in render order. */
const columnSources = ref<Array<() => UiTableColumn>>([])

/** Classes of each placeholder cell, one per header column: the same width and visibility as the column. */
const columnCellClasses = computed<string[][]>(() => columnSources.value.map((source) => columnClasses(source())))

/** Width of the status row: every header column (at least one). */
const columnCount = computed<number>(() => Math.max(columnSources.value.length, 1))

/**
 * Registers a header column and returns the function that removes it.
 *
 * @param column Getter of the header's current shape.
 * @returns Removes the column again.
 */
const registerColumn = (column: () => UiTableColumn): (() => void) => {
  columnSources.value.push(column)
  return () => {
    columnSources.value = columnSources.value.filter((source) => source !== column)
  }
}

/** Box of a placeholder cell: the same height and padding as a real data cell of this density. */
const skeletonCellClasses = computed<string>(() => cellDensityClasses[props.density])

/** Classes of the scrolling box: sideways always, and vertically too under a height cap. */
const boxClasses = computed<string>(() => {
  if (!props.scrollable) {
    return ''
  }
  return props.maxHeight ? `${scrollBoxClasses} ${tableMaxHeightClasses[props.maxHeight]}` : scrollBoxClasses
})

// A getter keeps the shared density and loading flag reactive without handing parts a ref they must unwrap.
provide(UI_TABLE_CONTEXT, {
  get density() {
    return props.density
  },
  get loading() {
    return props.loading
  },
  registerColumn,
  get selection() {
    return props.selection
  },
  get selectAllTestid() {
    return props.selectAllTestid
  },
  get selectAllLabel() {
    return t('shared.selectAll')
  },
  get selectRowLabel() {
    return t('shared.selectRow')
  },
})
</script>

<template>
  <div :class="boxClasses">
    <table v-bind="$attrs" class="[:where(&)]:w-full" :class="scrollable ? '' : stickyHeadFill">
      <slot />
      <tbody v-if="loading" aria-busy="true" data-testid="ui-table-skeleton">
        <tr v-for="row in rowIndexes" :key="row" class="border-b border-border-subtle last:border-0" aria-hidden="true">
          <td v-for="(classes, column) in columnCellClasses" :key="column" :class="[skeletonCellClasses, classes]">
            <UiSkeletonBar class="h-3 w-3/4 rounded" />
          </td>
        </tr>
      </tbody>
      <tbody v-else-if="empty">
        <tr>
          <td :colspan="columnCount" class="text-center text-sm text-text-muted" :class="$slots.empty ? '' : 'px-4 py-8'">
            <slot name="empty">
              {{ t('ui.table.empty') }}
            </slot>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
