<script setup lang="ts">
/**
 * Table row.
 *
 * `hoverable` tints the row under the pointer, `selected` marks the chosen row and `bordered`
 * draws the bottom hairline (none after the last row). All colours come from theme tokens.
 *
 * In a selectable table (`UiTable` with `selection`) the row draws the checkbox column itself: inside the header a
 * select-all cell (indeterminate over a partial selection), in the body a row checkbox for a row with a `rowId`. A click
 * toggles the row and Shift extends the range from the last toggled row. A ticked row gets the selected look (soft accent
 * background, accent bar on the left edge) and `aria-selected`.
 */
import { computed, inject } from 'vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import type { RowId } from '@/composables/useRowSelection'
import { UI_TABLE_CONTEXT, UI_TABLE_HEAD } from '@/utils/ui/injection'
import { selectedRowClasses } from '@/utils/ui/table'

/** Props for UiTableRow. */
const props = withDefaults(defineProps<{
  /** Tints the row on hover. */
  hoverable?: boolean
  /** Marks the row as selected. */
  selected?: boolean
  /** Draws a bottom border, except on the last row. */
  bordered?: boolean
  /** The id the row has in the table's selection; without it a body row draws no checkbox. */
  rowId?: RowId
  /** `data-testid` of the row's checkbox. */
  selectTestid?: string
}>(), {
  hoverable: false,
  selected: false,
  bordered: false,
  rowId: undefined,
  selectTestid: 'ui-table-select-row',
})

/** The table around this row, absent when the row is mounted alone. */
const table = inject(UI_TABLE_CONTEXT, null)

/** True inside a `UiTableHead`. */
const inHead = inject(UI_TABLE_HEAD, false)

/** True when the row draws the select-all cell. */
const showsSelectAll = computed<boolean>(() => table?.selection !== undefined && inHead)

/** True when the row draws its own checkbox. */
const showsCheckbox = computed<boolean>(() => table?.selection !== undefined && !inHead && props.rowId !== undefined)

/** True when the row is marked, by the `selected` prop or by the table's selection. */
const isSelected = computed<boolean>(() =>
  props.selected || (showsCheckbox.value && props.rowId !== undefined && table?.selection?.isSelected(props.rowId) === true),
)

/** State classes. */
const classes = computed<string[]>(() => [
  props.hoverable ? 'hover:bg-surface-hover transition-colors' : '',
  isSelected.value ? selectedRowClasses : '',
  props.bordered ? 'border-b border-border-subtle last:border-0' : '',
])

/**
 * Toggles this row in the table's selection.
 *
 * @param event - The click on the checkbox; Shift extends the range.
 */
const toggleRow = (event: MouseEvent): void => {
  if (props.rowId !== undefined) {
    table?.selection?.toggle(props.rowId, event.shiftKey)
  }
}
</script>

<template>
  <tr :class="classes" :aria-selected="showsCheckbox ? isSelected : undefined">
    <UiTableHeader v-if="showsSelectAll" select>
      <UiCheckbox
        :model-value="table?.selection?.allSelected.value === true"
        :indeterminate="table?.selection?.partiallySelected.value === true"
        :aria-label="table?.selectAllLabel"
:data-testid="table?.selectAllTestid"
        @change="table?.selection?.toggleAll()"
      />
    </UiTableHeader>
    <UiTableCell v-else-if="showsCheckbox" select>
      <UiCheckbox
        :model-value="isSelected"
        :aria-label="table?.selectRowLabel"
:data-testid="selectTestid"
        @change="toggleRow"
      />
    </UiTableCell>
    <slot />
  </tr>
</template>
