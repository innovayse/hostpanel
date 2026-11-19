<script setup lang="ts">
/**
 * Data table root.
 *
 * Draws the `<table>` and shares its `density` with the header and data cells (set the padding
 * once here instead of on every cell). `loading` keeps the screen's real header, makes `UiTableBody` and `UiTableFoot`
 * render nothing (a screen needs no `v-if` on its rows) and draws `skeletonRows`
 * placeholder rows under it, one bar per header column (the headers register themselves, so a column hidden below a
 * breakpoint or fixed in width is drawn the same way) at the real row height and cell
 * padding, so the page does not change shape when the data arrives (`skeletonGroup` opens them with the placeholder of a group heading row, for a
 * table whose rows come in groups); `empty` appends a full-width status row, so a
 * screen needs no separate "no rows" markup. The row is a compact `UiEmptyState` (icon and centred message) with `emptyText` as its message, or the
 * default "No data"; the column captions are hidden meanwhile (they stay registered, so the row is still as wide as the header), because a header over
 * a single message reads as a broken table. The `empty` slot replaces the whole block for a screen that needs a description or an action, and brings its
 * own spacing (a `UiEmptyState` fits as it is).
 *
 * The table is drawn inside the glass card of its screen, which supplies the surface. It sits in a box that
 * scrolls sideways inside the card at every width, so a wide table never breaks out of it. With `maxHeight` the
 * box also scrolls vertically past that height, and the header (see `UiTableHead`) sticks to the top of the box;
 * a box that only scrolls sideways has nothing to stick to. The edge of the box that still hides columns fades out (`useScrollEdges`, as the tabs do), so a clipped table says it scrolls. A page-level list, which must not scroll inside
 * the page, sets `scrollable="false"` instead: it fits the card by hiding secondary columns (`hideBelow`), and its
 * header sticks to the page's scroll container. A box that scrolls also clips anything that opens below a cell, so
 * an editable table whose cells hold a dropdown turns it off the same way (which also drops `maxHeight`).
 *
 * `selection` (from `useRowSelection`) makes the table selectable: the `UiTableRow` of the header draws a select-all checkbox
 * (indeterminate over a partial selection) and every body row that has a `rowId` draws its own, so a screen writes no checkbox column.
 * The rows toggle through the selection (Shift extends a range); the bulk actions live in a `UiBulkBar`.
 *
 * Below the `sm` breakpoint the table is drawn as cards: each body row is a card, the column
 * whose header says `card-role="title"` its heading (by default the `Grow` column) and the `badge` one a chip at the top right, the `subtitle` one a muted
 * line under the heading, every other column a label/value pair under the caption its header registered (`card-role="hidden"` leaves a column out, a
 * `hideBelow` column is a field like any other), the actions column the "…" trigger at the end of the first line and the checkbox the start of it. The loading placeholders take the same card shape, and the empty row, selection and a row's
 * click work as they do in the table. The width of a column is the `size` of its header: `Compact`, `Auto` (the default, one line) or the one `Grow`
 * column, which takes the rest of the width and truncates its content (a second `Grow` is a screen mistake, warned about in development).  A column whose cells hold a form control says `card-role="control"`: the caption goes over the control,
 * which takes the full width of the card.
 *
 * The phone layout is a `UiCardList` of `UiRecordCard`s, not the table markup restyled: below `sm` (a media query read from script) the table renders the
 * list in place of its `<table>`, and its head, body, foot, rows and cells draw card parts from the very same header columns and row slots (see
 * `UiTableRow`). The header captions that hold the sort buttons are not drawn there, so when a header is `sortable` the list's header offers a
 * select of the sortable columns and a direction button, which ask the screen to sort through the same `sort` event the header's button emits, and a
 * selectable table gets its select-all checkbox there too. A screen writes nothing for any of it.
 *
 * The table gives the semantics of its parts as explicit roles (table, rowgroup, row, columnheader, cell), because a part
 * drawn as a block would otherwise lose them for assistive technology.
 */
import { computed, provide, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiCardList from '@/components/ui/UiCardList.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import { useMediaQuery } from '@/composables/useMediaQuery'
import { useScrollEdges } from '@/composables/useScrollEdges'
import { Icon } from '@/lib/icons'
import { UI_BELOW_SM_QUERY } from '@/utils/ui/breakpoints'
import { UI_TABLE_CONTEXT, UiTableDensity } from '@/utils/ui/injection'
import type { UiTableColumn } from '@/utils/ui/injection'
import type { RowSelection } from '@/composables/useRowSelection'
import type { UiSelectOption } from '@/utils/ui/select'
import { UiSize } from '@/utils/ui/size'
import { cardSkeletonOf, cellDensityClasses, columnClasses, columnSizeOf, scrollBoxClasses, stickyHeadFill, tableMaxHeightClasses, UiColumnSize, UiSortDirection, withCardTitle } from '@/utils/ui/table'
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
  /** Message of the empty state; the translated "No data" when omitted. */
  emptyText?: string
  /** Number of placeholder rows drawn while `loading`. */
  skeletonRows?: number
  /** Opens the placeholder rows with the placeholder of a group heading row, for a table whose rows come in groups. */
  skeletonGroup?: boolean
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
  emptyText: undefined,
  skeletonRows: 5,
  skeletonGroup: false,
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

/** The registered header columns, in header order, with the card heading picked among them (see `withCardTitle`). */
const columns = computed<PositionedColumn[]>(() => {
  const declared = columnSources.value.flatMap((source, index) => (source ? [{ index, column: source() }] : []))
  const resolved = withCardTitle(declared.map(({ column }) => column))
  return declared.map(({ index, column }, position) => ({ index, column: resolved[position] ?? column }))
})

/** One placeholder cell: the same width and visibility as its column. */
interface PlaceholderCell {
  /** Position of the column. */
  index: number
  /** Classes of the cell. */
  classes: string[]
}

/** The placeholder cells of one skeleton row, one per header column. */
const placeholderCells = computed<PlaceholderCell[]>(() =>
  columns.value.map(({ index, column }) => ({ index, classes: [columnSizeOf(column), ...columnClasses(column)] })),
)

/** True below `sm`, where the table is a list of cards. */
const phone = useMediaQuery(UI_BELOW_SM_QUERY)

/** The shape of the placeholder cards the phone layout draws while loading. */
const cardSkeleton = computed(() => cardSkeletonOf(columns.value.map(({ column }) => column)))

/** How many header columns are `Grow`: more than one fight for the same remaining width. */
const growCount = computed<number>(() => columns.value.filter(({ column }) => column.size === UiColumnSize.Grow).length)

// Development guard: two columns cannot both take the rest of the width, so the second is a screen mistake to fix, not to ship.
if (import.meta.env.DEV) {
  watch(
    growCount,
    (count) => {
      if (count > 1) {
        console.warn(`UiTable: ${count} columns are sized UiColumnSize.Grow, a table has at most one.`)
      }
    },
    { immediate: true },
  )
}

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
const columnAt = (index: number): UiTableColumn | undefined => columns.value.find((positioned) => positioned.index === index)?.column

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

/** The scrolling box around the table, bound with `ref="box"`. */
const box = ref<HTMLElement | null>(null)

/** The fade of the side of the box that still hides columns. */
const { fadeClass } = useScrollEdges(box)

/** Classes of the scrolling box: sideways always, and vertically too under a height cap, with the fade of the hidden edge. */
const boxClasses = computed<string>(() => {
  if (!props.scrollable) {
    return ''
  }
  const base = props.maxHeight ? `${scrollBoxClasses} ${tableMaxHeightClasses[props.maxHeight]}` : scrollBoxClasses
  return `${base} ${fadeClass.value}`
})

// A getter keeps the shared density and loading flag reactive without handing parts a ref they must unwrap.
provide(UI_TABLE_CONTEXT, {
  get density() {
    return props.density
  },
  get phone() {
    return phone.value
  },
  get loading() {
    return props.loading
  },
  get empty() {
    return props.empty && !props.loading
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
  <UiCardList
    v-if="phone"
    v-bind="$attrs"
    :loading="loading"
    :skeleton-rows="skeletonRows"
    :skeleton="cardSkeleton"
    :empty="empty && !loading"
    :empty-text="emptyText"
    :sort-options="sortOptions"
    :sort-value="activeSort?.index ?? null"
    :sort-direction="activeSort?.column.direction"
    :selection="selection"
    :select-all-testid="selectAllTestid"
    @sort="sortBy"
    @reverse="reverseSort"
  >
    <slot />
    <template v-if="$slots.empty" #empty>
      <slot name="empty" />
    </template>
  </UiCardList>
  <div v-else ref="box" :class="boxClasses">
    <table v-bind="$attrs" role="table" class="[:where(&)]:w-full" :class="scrollable ? '' : stickyHeadFill">
      <slot />
      <tbody v-if="loading" role="rowgroup" aria-busy="true" data-testid="ui-table-skeleton">
        <tr v-if="skeletonGroup" role="row" class="border-b border-border-subtle" aria-hidden="true" data-testid="ui-table-skeleton-group">
          <td role="cell" :colspan="columnCount" :class="skeletonCellClasses">
            <UiSkeletonBar class="h-4 w-40 rounded" />
          </td>
        </tr>
        <tr v-for="row in rowIndexes" :key="row" role="row" class="border-b border-border-subtle last:border-0" aria-hidden="true">
          <td v-for="cell in placeholderCells" :key="cell.index" role="cell" :class="[skeletonCellClasses, cell.classes]">
            <UiSkeletonBar class="h-3 w-3/4 rounded" />
          </td>
        </tr>
      </tbody>
      <tbody v-else-if="empty" role="rowgroup">
        <tr role="row">
          <td role="cell" :colspan="columnCount" class="text-center text-sm text-text-muted">
            <slot name="empty">
              <UiEmptyState compact :title="emptyText ?? t('ui.table.empty')" data-testid="ui-table-empty">
                <template #icon>
                  <UiIcon :markup="Icon.Inbox" :size="UiSize.Md" />
                </template>
              </UiEmptyState>
            </slot>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
