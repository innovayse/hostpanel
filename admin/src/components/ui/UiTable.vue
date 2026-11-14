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
 *
 * Below the `sm` breakpoint the table is drawn as cards with the same markup: each body row is a card, the column
 * whose header says `card-role="title"` its heading and the `badge` one a chip at the top right, the other columns
 * label/value pairs under the caption their header registered (`card-role="hidden"` leaves a column out), and the
 * actions column the footer. The loading placeholders take the same card shape, and the empty row, selection and a row's
 * click work as they do in the table. A column that is hidden below a breakpoint stays out of the card too, unless its header
 * sets `card-role` itself. A column whose cells hold a form control says `card-role="control"`: the caption goes over the control,
 * which takes the full width of the card.
 *
 * The header captions that hold the sort buttons are not drawn on a card, so when a header is `sortable` the table draws one compact
 * control above the cards (a select of the sortable columns and a button for the direction). It asks the screen to sort through the
 * same `sort` event the header's button emits, so a screen writes nothing for it.
 *
 * The table gives the semantics of its parts as explicit roles (table, rowgroup, row, columnheader, cell), because a part
 * drawn as a block would otherwise lose them for assistive technology.
 */
import { computed, provide, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import { Icon } from '@/lib/icons'
import { UI_TABLE_CONTEXT, UiTableDensity } from '@/utils/ui/injection'
import type { UiTableColumn } from '@/utils/ui/injection'
import type { RowSelection } from '@/composables/useRowSelection'
import type { UiSelectOption } from '@/utils/ui/select'
import { UiSize } from '@/utils/ui/size'
import { cardCellClasses, cardLabelClasses, cardRowClasses, cardTableClasses, cellDensityClasses, columnClasses, scrollBoxClasses, stickyHeadFill, tableMaxHeightClasses, UiSortDirection } from '@/utils/ui/table'
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

/** Getters of the registered header columns, by their position in the header row (a position nobody holds is empty). */
const columnSources = ref<Array<(() => UiTableColumn) | undefined>>([])

/** One registered header column with its position in the header row. */
interface PositionedColumn {
  /** Position of the header in its row. */
  index: number
  /** The column the header declared. */
  column: UiTableColumn
}

/** The registered header columns, in header order. */
const columns = computed<PositionedColumn[]>(() =>
  columnSources.value.flatMap((source, index) => (source ? [{ index, column: source() }] : [])),
)

/** One placeholder cell: the same width and visibility as its column, its card role below `sm` and the caption a card shows before the bar. */
interface PlaceholderCell {
  /** Position of the column. */
  index: number
  /** Classes of the cell. */
  classes: string[]
  /** Classes of the caption element, empty when the cell draws none. */
  labelClasses: string
  /** Caption of the column. */
  label: string | undefined
}

/** The placeholder cells of one skeleton row, one per header column. */
const placeholderCells = computed<PlaceholderCell[]>(() =>
  columns.value.map(({ index, column }) => ({
    index,
    classes: [...columnClasses(column), cardCellClasses(column)],
    labelClasses: cardLabelClasses(column),
    label: column.label,
  })),
)

/** Width of the status row: every header column (at least one). */
const columnCount = computed<number>(() => Math.max(columns.value.length, 1))

/**
 * Registers a header column at its position and returns the function that removes it.
 *
 * @param index Position of the header in its row.
 * @param column Getter of the header's current shape.
 * @returns Removes the column again, unless another header has taken the position since.
 */
const registerColumn = (index: number, column: () => UiTableColumn): (() => void) => {
  columnSources.value[index] = column
  return () => {
    if (columnSources.value[index] === column) {
      columnSources.value[index] = undefined
    }
  }
}

/**
 * Finds the registered column at a position.
 *
 * @param index - Position of a cell in its row.
 * @returns The column the header at that position declared, or `undefined` when there is none.
 */
const columnAt = (index: number): UiTableColumn | undefined => columnSources.value[index]?.()

/** The sortable columns: what the sort control of the card layout offers. */
const sortableColumns = computed<PositionedColumn[]>(() => columns.value.filter(({ column }) => column.sortable))

/** The options of the sort control: one per sortable column, valued by its position. */
const sortOptions = computed<UiSelectOption<number>[]>(() =>
  sortableColumns.value.map(({ index, column }) => ({ value: index, label: column.label ?? '' })),
)

/** The sortable column the rows are ordered by now, absent while none is. */
const activeSort = computed<PositionedColumn | undefined>(() =>
  sortableColumns.value.find(({ column }) => column.direction !== undefined && column.direction !== UiSortDirection.None),
)

/** Icon of the direction button: the arrow of the active order. */
const directionIcon = computed<string>(() =>
  activeSort.value?.column.direction === UiSortDirection.Descending ? Icon.ArrowDown : Icon.ArrowUp,
)

/**
 * Orders by the column the user picked in the sort control.
 *
 * @param index - Position of the picked column, or `null` when the pick was cleared.
 */
const sortBy = (index: number | null): void => {
  if (index !== null) {
    columnAt(index)?.sort?.()
  }
}

/** Asks the active column to sort again, which the screen answers by reversing the order, as a second click on its header would. */
const reverseSort = (): void => {
  activeSort.value?.column.sort?.()
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
  columnAt,
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
    <!-- The header captions are not drawn on a card, so ordering is offered here, on phones only. -->
    <div v-if="sortableColumns.length > 0" class="mb-2 flex items-center gap-2 sm:hidden" data-testid="ui-table-sort">
      <UiSelect
        class="min-w-0 flex-1"
        compact
        :model-value="activeSort?.index ?? null"
        :options="sortOptions"
        :placeholder="t('ui.table.sortBy')"
        :aria-label="t('ui.table.sortBy')"
        data-testid="ui-table-sort-column"
        @update:model-value="sortBy"
      />
      <UiIconButton
        :label="t('ui.table.sortDirection')"
        :size="UiSize.Sm"
        :disabled="activeSort === undefined"
        data-testid="ui-table-sort-direction"
        @click="reverseSort"
      >
        <UiIcon :markup="directionIcon" :size="UiSize.Sm" />
      </UiIconButton>
    </div>
    <table v-bind="$attrs" role="table" class="[:where(&)]:w-full" :class="[cardTableClasses, scrollable ? '' : stickyHeadFill]">
      <slot />
      <tbody v-if="loading" role="rowgroup" :class="cardTableClasses" aria-busy="true" data-testid="ui-table-skeleton">
        <tr v-for="row in rowIndexes" :key="row" role="row" class="border-b border-border-subtle last:border-0" :class="cardRowClasses" aria-hidden="true">
          <td v-for="cell in placeholderCells" :key="cell.index" role="cell" :class="[skeletonCellClasses, cell.classes]">
            <span v-if="cell.labelClasses" :class="cell.labelClasses">{{ cell.label }}</span>
            <UiSkeletonBar class="h-3 w-3/4 rounded" />
          </td>
        </tr>
      </tbody>
      <tbody v-else-if="empty" role="rowgroup" :class="cardTableClasses">
        <tr role="row">
          <td role="cell" :colspan="columnCount" class="text-center text-sm text-text-muted" :class="$slots.empty ? '' : 'px-4 py-8'">
            <slot name="empty">
              {{ t('ui.table.empty') }}
            </slot>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
