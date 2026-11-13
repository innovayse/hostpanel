<script setup lang="ts">
/**
 * Table data cell.
 *
 * Padding comes from the table's `density`; `align` positions the content and `muted` greys
 * secondary values and `strong` stresses a group heading or key figure. `numeric` is the one shape of a number or money cell (right aligned, figures of one
 * width) and `hideBelow` hides the column under a breakpoint (a secondary column of a wide list), and `actions` the one shape of the row actions cell (right aligned, the same fixed width on
 * every table), so no screen picks those classes itself. `truncate` makes the cell the one that gives way when the row is too wide. `select` marks the checkbox cell of a
 * selectable table (drawn by `UiTableRow`, not set by a screen).
 */
import { computed, inject } from 'vue'
import { UI_TABLE_CONTEXT, UiTableDensity } from '@/utils/ui/injection'
import { cellDensityClasses, columnClasses, truncateCellClasses } from '@/utils/ui/table'
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
}>(), {
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

/** Padding, alignment and tone classes. */
const classes = computed<string[]>(() => [
  cellDensityClasses[context.density],
  ...columnClasses(props),
  props.muted ? 'text-text-muted' : '',
  props.strong ? 'font-semibold text-text-primary' : '',
  props.truncate ? truncateCellClasses : '',
])
</script>

<template>
  <td :class="classes"><slot /></td>
</template>
