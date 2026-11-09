<script setup lang="ts">
/**
 * Table data cell.
 *
 * Padding comes from the table's `density`; `align` positions the content and `muted` greys
 * secondary values. `numeric` is the one shape of a number or money cell (right aligned, figures of one
 * width) and `actions` the one shape of the row actions cell (right aligned, the same fixed width on
 * every table), so no screen picks those classes itself.
 */
import { computed, inject } from 'vue'
import { UI_TABLE_CONTEXT, UiTableDensity } from '@/utils/ui/injection'
import { UiAlign, actionsColumnClasses, alignClasses, cellDensityClasses, numericCellClasses } from '@/utils/ui/table'

/** Props for UiTableCell. */
const props = withDefaults(defineProps<{
  /** Horizontal alignment, the same source as the header (left when omitted). */
  align?: UiAlign
  /** Greys the value. */
  muted?: boolean
  /** Renders a number or money value: right aligned with tabular figures. */
  numeric?: boolean
  /** Renders the row actions cell: right aligned at the shared actions column width. */
  actions?: boolean
}>(), {
  align: undefined,
  muted: false,
  numeric: false,
  actions: false,
})

/** Density shared by the parent table (plain when used outside one). */
const context = inject(UI_TABLE_CONTEXT, { density: UiTableDensity.Plain })

/** Alignment classes: the actions and numeric shapes win over `align`. */
const shapeClasses = computed<string>(() => {
  if (props.actions) {
    return actionsColumnClasses
  }
  if (props.numeric) {
    return numericCellClasses
  }
  return alignClasses[props.align ?? UiAlign.Left]
})

/** Padding, alignment and tone classes. */
const classes = computed<string[]>(() => [
  cellDensityClasses[context.density],
  shapeClasses.value,
  props.muted ? 'text-text-muted' : '',
])
</script>

<template>
  <td :class="classes"><slot /></td>
</template>
