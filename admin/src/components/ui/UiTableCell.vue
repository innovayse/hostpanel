<script setup lang="ts">
/**
 * Table data cell.
 *
 * Padding comes from the table's `density`; `align` positions the content (numbers usually
 * `right`) and `muted` greys secondary values.
 */
import { computed, inject } from 'vue'
import { UI_TABLE_CONTEXT, UiTableDensity } from '@/utils/ui/injection'
import { UiAlign, alignClasses, cellDensityClasses } from '@/utils/ui/table'

/** Props for UiTableCell. */
const props = withDefaults(defineProps<{
  /** Horizontal alignment, the same source as the header (left when omitted). */
  align?: UiAlign
  /** Greys the value. */
  muted?: boolean
}>(), {
  align: undefined,
  muted: false,
})

/** Density shared by the parent table (plain when used outside one). */
const context = inject(UI_TABLE_CONTEXT, { density: UiTableDensity.Plain })

/** Padding, alignment and tone classes. */
const classes = computed<string[]>(() => [
  cellDensityClasses[context.density],
  alignClasses[props.align ?? UiAlign.Left],
  props.muted ? 'text-text-muted' : '',
])
</script>

<template>
  <td :class="classes"><slot /></td>
</template>
