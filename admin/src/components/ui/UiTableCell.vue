<script setup lang="ts">
/**
 * Table data cell.
 *
 * Padding comes from the table's `density`; `align` positions the content and `muted` greys
 * secondary values and `strong` stresses a group heading or key figure. `numeric` is the one shape of a number or money cell (right aligned, figures of one
 * width) and `hideBelow` hides the column under a breakpoint (a secondary column of a wide list), and `actions` the one shape of the row actions cell (right aligned, the same fixed width on
 * every table), so no screen picks those classes itself. `truncate` makes the cell the one that gives way when the row is too wide. `select` marks the checkbox cell of a
 * selectable table (drawn by `UiTableRow`, not set by a screen).
 *
 * Below `sm` the cell is a part of a card (see `UiTable`): the row hands the cell its position (`columnIndex`, counted over the row's
 * cells at render time, so a cell that is not rendered or one that spans columns does not shift the others), the cell takes the role of the
 * header column at that position, and a field shows the column's caption before its value, as real text that is hidden from `sm` up.
 * A cell with no column of its own (one that spans the row) is a full width line of the card.
 */
import { computed, inject } from 'vue'
import { UI_TABLE_CONTEXT, UiTableDensity } from '@/utils/ui/injection'
import type { UiTableColumn } from '@/utils/ui/injection'
import { cardCellClasses, cardLabelClasses, cellDensityClasses, columnClasses, truncateCellClasses } from '@/utils/ui/table'
import type { UiAlign, UiHideBelow } from '@/utils/ui/table'

/** Props for UiTableCell. */
const props = withDefaults(defineProps<{
  /** Horizontal alignment, the same source as the header (left when omitted). */
  align?: UiAlign
  /** Greys the value. */
  muted?: boolean
  /** Emphasised cell: semibold primary text (a group heading, a key figure). */
  strong?: boolean
  /** Renders a number or money value: right aligned with tabular figures. */
  numeric?: boolean
  /** Renders the row actions cell: right aligned at the shared actions column width. */
  actions?: boolean
  /** Hides the column below this breakpoint; set it on the header and on every cell of the column. */
  hideBelow?: UiHideBelow
  /** Takes the width the other columns leave and truncates its content (give the text `truncate` and `min-w-0`). */
  truncate?: boolean
  /** The checkbox cell of a selectable table: narrow, like its header. */
  select?: boolean
  /** Position of the cell among the cells of its row; set by `UiTableRow`, never by a screen. */
  columnIndex?: number
}>(), {
  columnIndex: undefined,
  hideBelow: undefined,
  align: undefined,
  muted: false,
  strong: false,
  numeric: false,
  actions: false,
  truncate: false,
  select: false,
})

/** Density shared by the parent table (plain when used outside one). */
const context = inject(UI_TABLE_CONTEXT, { density: UiTableDensity.Plain })

/** The header column of this cell, absent outside a table or for a cell that spans the row. */
const column = computed<UiTableColumn | undefined>(() =>
  props.columnIndex === undefined ? undefined : context.columnAt?.(props.columnIndex),
)

/** True inside a table, where a cell without a column of its own is a card line; a lone cell has no card layout. */
const inTable = computed<boolean>(() => context.columnAt !== undefined)

/** Classes of the caption element, empty when the cell draws none. */
const labelClasses = computed<string>(() => cardLabelClasses(column.value))

/** Padding, alignment and tone classes. */
const classes = computed<string[]>(() => [
  cellDensityClasses[context.density],
  ...columnClasses(props),
  props.muted ? 'text-text-muted' : '',
  props.strong ? 'font-semibold text-text-primary' : '',
  props.truncate ? truncateCellClasses : '',
  inTable.value ? cardCellClasses(column.value) : '',
])
</script>

<template>
  <td role="cell" :class="classes">
    <span v-if="labelClasses" :class="labelClasses">{{ column?.label }}</span>
    <slot />
  </td>
</template>
