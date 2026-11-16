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
 *
 * Below `sm` a body row is a card (see `UiTable`); the header row keeps only its select-all cell. The row counts its cells at render
 * time (the checkbox cell first, a `colspan` cell as many as it spans, a cell behind a false `v-if` not at all) and hands each header and
 * data cell its position, which is how a cell finds its column in the table. A cell that spans more than one column gets no position: it
 * has no column of its own and is a full width line of the card. Cells are found in the row's own slot, through fragments (`v-for`,
 * `v-if`, a slot of a wrapping component); a cell rendered inside another component of its own is not counted.
 */
import { cloneVNode, computed, Fragment, h, inject, isVNode } from 'vue'
import type { FunctionalComponent, VNodeArrayChildren } from 'vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import type { RowId } from '@/composables/useRowSelection'
import { UI_TABLE_CONTEXT, UI_TABLE_HEAD } from '@/utils/ui/injection'
import { cardHeadRowClasses, cardRowClasses, selectedRowClasses } from '@/utils/ui/table'

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

/** True when the row draws a checkbox cell of its own, which takes the first position. */
const drawsSelectCell = computed<boolean>(() => showsSelectAll.value || showsCheckbox.value)

/**
 * Gives every header and data cell of the slot its position in the row.
 *
 * @param nodes - The nodes the slot rendered.
 * @param counter - Counts the cells of the row.
 * @param counter.next - Position of the next cell; a cell advances it by the columns it spans.
 * @returns The same nodes with the cells carrying their `columnIndex`.
 */
const indexCells = (nodes: VNodeArrayChildren, counter: { next: number }): VNodeArrayChildren =>
  nodes.map((node) => {
    if (Array.isArray(node)) {
      return indexCells(node, counter)
    }
    if (!isVNode(node)) {
      return node
    }
    if (node.type === Fragment && Array.isArray(node.children)) {
      return h(Fragment, { key: node.key ?? undefined }, indexCells(node.children, counter))
    }
    if (node.type !== UiTableCell && node.type !== UiTableHeader) {
      return node
    }
    const span = Number(node.props?.colspan ?? 1)
    const position = counter.next
    counter.next += span
    return span > 1 ? node : cloneVNode(node, { columnIndex: position })
  })

/**
 * The row's cells, each knowing its position. It wraps the row's slot (`<IndexedCells><slot /></IndexedCells>`), which makes
 * it render again whenever the row does, so a cell that appears, goes or changes (an edit swapping a cell set) is counted anew.
 *
 * @param props - The position of the first cell of the slot.
 * @param context - The render context holding the wrapped slot.
 * @param context.slots - The slots of the wrapper.
 * @returns The nodes of the slot with the cells carrying their position.
 */
const IndexedCells: FunctionalComponent<{ start: number }> = (props, { slots }) =>
  indexCells(slots.default?.() ?? [], { next: props.start })

IndexedCells.props = ['start']

/** State classes. */
const classes = computed<string[]>(() => [
  props.hoverable ? 'hover:bg-surface-hover transition-colors' : '',
  isSelected.value ? selectedRowClasses : '',
  props.bordered ? 'border-b border-border-subtle last:border-0' : '',
  inHead ? cardHeadRowClasses : cardRowClasses,
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
  <tr role="row" :class="classes" :aria-selected="showsCheckbox ? isSelected : undefined">
    <UiTableHeader v-if="showsSelectAll" select :column-index="0">
      <UiCheckbox
        :model-value="table?.selection?.allSelected.value === true"
        :indeterminate="table?.selection?.partiallySelected.value === true"
        :aria-label="table?.selectAllLabel"
        :data-testid="table?.selectAllTestid"
        @change="table?.selection?.toggleAll()"
      />
    </UiTableHeader>
    <UiTableCell v-else-if="showsCheckbox" select :column-index="0">
      <UiCheckbox
        :model-value="isSelected"
        :aria-label="table?.selectRowLabel"
        :data-testid="selectTestid"
        @change="toggleRow"
      />
    </UiTableCell>
    <IndexedCells :start="drawsSelectCell ? 1 : 0">
      <slot />
    </IndexedCells>
  </tr>
</template>
