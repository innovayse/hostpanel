/**
 * Multi-row selection of a list: the ticked ids, a toggle that extends to a range with Shift, select all and
 * clear, and the counts a header checkbox and a bulk bar read.
 *
 * Every list with bulk actions shares this one implementation. The list passes the ids of the rows it shows
 * (in display order, which is what a Shift range follows); `UiTable` takes the returned selection through its
 * `selection` prop and draws the checkbox column, and `UiBulkBar` shows the count and the actions. When the
 * rows change (a new page, a reload, a filter) the ids that are no longer shown drop out of the selection.
 */
import { computed, ref, watch } from 'vue'
import type { ComputedRef, Ref } from 'vue'

/** What identifies a row: a number or a string id. */
export type RowId = number | string

/** What {@link useRowSelection} hands back. Members are declared as methods so a `RowSelection<number>` fits a `RowSelection`. */
export interface RowSelection<Id extends RowId = RowId> {
  /** The ticked ids, in the order they were ticked. */
  ids: Readonly<Ref<readonly Id[]>>
  /** How many rows are ticked. */
  count: ComputedRef<number>
  /** True when every shown row is ticked (and there is at least one). */
  allSelected: ComputedRef<boolean>
  /** True when some, but not all, shown rows are ticked: the header checkbox is indeterminate. */
  partiallySelected: ComputedRef<boolean>
  /** Tells whether a row is ticked. */
  isSelected(id: Id): boolean
  /** Ticks or unticks one row; with `extend` every row between the last toggled row and this one is ticked. */
  toggle(id: Id, extend?: boolean): void
  /** Ticks every shown row, or clears the selection when all were ticked. */
  toggleAll(): void
  /** Ticks every shown row. */
  selectAll(): void
  /** Unticks everything. */
  clear(): void
}

/**
 * Creates the selection state of one list.
 *
 * @param rowIds - Getter of the ids of the shown rows, in display order.
 * @returns The selection: its ids, counts and the actions that change them.
 */
export const useRowSelection = <Id extends RowId = number>(rowIds: () => readonly Id[]): RowSelection<Id> => {
  /** The ticked ids. */
  const ids = ref<Id[]>([]) as Ref<Id[]>

  /** The row toggled last, where a Shift range starts; null before the first toggle. */
  let anchor: Id | null = null

  /** How many rows are ticked. */
  const count = computed<number>(() => ids.value.length)

  /** True when every shown row is ticked (and there is at least one). */
  const allSelected = computed<boolean>(() => {
    const rows = rowIds()
    return rows.length > 0 && rows.every((id) => ids.value.includes(id))
  })

  /** True when some, but not all, shown rows are ticked. */
  const partiallySelected = computed<boolean>(() => count.value > 0 && !allSelected.value)

  /**
   * Tells whether a row is ticked.
   *
   * @param id - The row id.
   * @returns True when the row is in the selection.
   */
  const isSelected = (id: Id): boolean => ids.value.includes(id)

  /**
   * Ticks every row between the anchor and a row, both included.
   *
   * @param id - The row the range ends at.
   * @param from - The row the range starts at.
   */
  const selectRange = (id: Id, from: Id): void => {
    const rows = rowIds()
    const start = rows.indexOf(from)
    const end = rows.indexOf(id)
    const span = rows.slice(Math.min(start, end), Math.max(start, end) + 1)
    ids.value = [...ids.value, ...span.filter((rowId) => !ids.value.includes(rowId))]
  }

  /**
   * Ticks or unticks one row; with `extend` and a row toggled before, the whole range is ticked.
   *
   * @param id - The row id.
   * @param extend - True while Shift is held.
   */
  const toggle = (id: Id, extend = false): void => {
    const rows = rowIds()
    if (extend && anchor !== null && rows.includes(anchor) && rows.includes(id)) {
      selectRange(id, anchor)
    } else {
      ids.value = isSelected(id) ? ids.value.filter((rowId) => rowId !== id) : [...ids.value, id]
    }
    anchor = id
  }

  /** Ticks every shown row. */
  const selectAll = (): void => {
    ids.value = [...rowIds()]
  }

  /** Unticks everything. */
  const clear = (): void => {
    ids.value = []
    anchor = null
  }

  /** Ticks every shown row, or clears the selection when all were ticked. */
  const toggleAll = (): void => {
    if (allSelected.value) {
      clear()
    } else {
      selectAll()
    }
  }

  // A row that is no longer shown (another page, a filter, a deleted record) cannot stay ticked.
  watch(rowIds, (rows) => {
    ids.value = ids.value.filter((id) => rows.includes(id))
  })

  return { ids, count, allSelected, partiallySelected, isSelected, toggle, toggleAll, selectAll, clear }
}
