<script setup lang="ts">
/**
 * Table data cell.
 *
 * Padding comes from the table's `density`; `align` positions the content and `muted` greys
 * secondary values and `strong` stresses a group heading or key figure. `numeric` is the one shape of a number or money cell (right aligned, figures of one
 * width) and `hideBelow` hides the column under a breakpoint (a secondary column of a wide list), and `actions` the one shape of the row actions cell (right aligned, the same fixed width on
 * every table), so no screen picks those classes itself. The width of the cell is its column's `size`, read from the header; a cell of a column that truncates shows its full text as a tooltip when it is cut. `select` marks the checkbox cell of a
 * selectable table (drawn by `UiTableRow`, not set by a screen).
 *
 * On a phone (see `UiTable`) the cell is a part of a `UiRecordCard` and draws a `<div>`, not a `<td>`: the row hands the cell its position (`columnIndex`, counted over the row's
 * cells at render time, so a cell that is not rendered or one that spans columns does not shift the others), the cell takes the role of the
 * header column at that position, and a field shows the column's caption before its value, as real text.
 * A cell with no column of its own (one that spans the row) is a full width line of the card.
 */
import { computed, inject } from 'vue'
import { UI_TABLE_CONTEXT, UiTableDensity } from '@/utils/ui/injection'
import type { UiTableColumn } from '@/utils/ui/injection'
import { cardCellClasses, cardLabelClasses, cellDensityClasses, columnClasses, columnSizeOf, truncatesColumn } from '@/utils/ui/table'
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
  select: false,
})

/** Density shared by the parent table (plain when used outside one). */
const context = inject(UI_TABLE_CONTEXT, { density: UiTableDensity.Plain })

/** The header column of this cell, absent outside a table or for a cell that spans the row. */
const column = computed<UiTableColumn | undefined>(() =>
  props.columnIndex === undefined ? undefined : context.columnAt?.(props.columnIndex),
)

/** Classes of the caption element, empty when the cell draws none. */
const labelClasses = computed<string>(() => cardLabelClasses(column.value))

/**
 * Shows the full text of a cell the column cuts, as its tooltip, the moment the pointer reaches it (the text is cut only
 * once the table is laid out, so it cannot be known before).
 *
 * @param event - The pointer entering the cell.
 */
const revealCutText = (event: MouseEvent): void => {
  if (!truncatesColumn(column.value)) {
    return
  }
  const cell = event.currentTarget as HTMLElement
  cell.title = cell.scrollWidth > cell.clientWidth ? (cell.textContent ?? '').trim() : ''
}

/** Padding, alignment and tone classes. */
const classes = computed<string[]>(() => [
  cellDensityClasses[context.density],
  ...columnClasses(props),
  props.muted ? 'text-text-muted' : '',
  props.strong ? 'font-semibold text-text-primary' : '',
  column.value ? columnSizeOf(column.value) : '',
])

/** Classes of the box the cell is drawn in on a card: its role's line, and the tone it asks for. */
const cardClasses = computed<string[]>(() => [
  cardCellClasses(column.value),
  props.muted ? 'text-text-muted' : '',
  props.strong ? 'font-semibold text-text-primary' : '',
])
</script>

<template>
  <div v-if="context.phone" :class="cardClasses">
    <span v-if="labelClasses" :class="labelClasses">{{ column?.label }}</span>
    <div v-if="labelClasses" class="min-w-0"><slot /></div>
    <slot v-else />
  </div>
  <td v-else role="cell" :class="classes" @mouseenter="revealCutText">
    <slot />
  </td>
</template>
