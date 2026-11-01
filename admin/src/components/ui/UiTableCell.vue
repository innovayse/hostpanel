<script setup lang="ts">
/**
 * Table data cell.
 *
 * Padding comes from the table's `density`; `align` positions the content (numbers usually
 * `right`) and `muted` greys secondary values.
 */
import { computed, inject } from 'vue'
import { UI_TABLE_CONTEXT, UiTableDensity } from '@/utils/ui/injection'
import { alignClasses, cellDensityClasses } from '@/utils/ui/table'
import type { UiAlign } from '@/utils/ui/table'

/** Props for UiTableCell. */
const props = withDefaults(defineProps<{
  /** Horizontal alignment; omitted leaves it to the browser/screen. */
  align?: UiAlign
  /** Greys the value. */
  muted?: boolean
}>(), {
  align: undefined,
  muted: false,
})

/** Density shared by the parent table (plain when used outside one). */
const context = inject(UI_TABLE_CONTEXT, { density: UiTableDensity.Plain })

/** Padding, alignment and tone classes (the table scrolls sideways, so a cell keeps a readable minimum width). */
const classes = computed<string[]>(() => [
  cellDensityClasses[context.density],
  props.align === undefined ? '' : alignClasses[props.align],
  props.muted ? 'text-text-muted' : '',
])
</script>

<template>
  <td class="[:where(&)]:min-w-24" :class="classes"><slot /></td>
</template>
