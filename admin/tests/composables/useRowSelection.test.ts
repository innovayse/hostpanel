/**
 * Unit tests for useRowSelection: toggling, Shift ranges, select all, counts and the pruning of rows that are gone.
 */
import { describe, expect, it } from 'vitest'
import { nextTick, ref } from 'vue'
import { useRowSelection } from '@/composables/useRowSelection'

/**
 * A selection over a list of rows the test can change.
 *
 * @param initial - The ids of the rows shown at first.
 * @returns The rows ref and the selection.
 */
const open = (initial: number[] = [1, 2, 3, 4, 5]) => {
  const rows = ref<number[]>(initial)
  return { rows, selection: useRowSelection(() => rows.value) }
}

describe('useRowSelection', () => {
  it('starts empty', () => {
    const { selection } = open()

    expect(selection.ids.value).toEqual([])
    expect(selection.count.value).toBe(0)
    expect(selection.allSelected.value).toBe(false)
    expect(selection.partiallySelected.value).toBe(false)
  })

  it('toggles one row on and off', () => {
    const { selection } = open()

    selection.toggle(2)
    expect(selection.isSelected(2)).toBe(true)
    expect(selection.count.value).toBe(1)
    expect(selection.partiallySelected.value).toBe(true)

    selection.toggle(2)
    expect(selection.isSelected(2)).toBe(false)
    expect(selection.count.value).toBe(0)
  })

  it('ticks the whole range from the last toggled row on a Shift toggle, in either direction', () => {
    const { selection } = open()

    selection.toggle(2)
    selection.toggle(4, true)
    expect([...selection.ids.value].sort()).toEqual([2, 3, 4])

    selection.toggle(5)
    selection.toggle(3, true)
    expect([...selection.ids.value].sort()).toEqual([2, 3, 4, 5])
  })

  it('toggles a single row on a Shift toggle when no row was toggled before', () => {
    const { selection } = open()

    selection.toggle(3, true)

    expect(selection.ids.value).toEqual([3])
  })

  it('selects every shown row and reports it, and clears again', () => {
    const { selection } = open()

    selection.selectAll()
    expect(selection.count.value).toBe(5)
    expect(selection.allSelected.value).toBe(true)
    expect(selection.partiallySelected.value).toBe(false)

    selection.clear()
    expect(selection.ids.value).toEqual([])
  })

  it('toggleAll ticks everything, then clears when everything was ticked', () => {
    const { selection } = open()

    selection.toggle(1)
    selection.toggleAll()
    expect(selection.allSelected.value).toBe(true)

    selection.toggleAll()
    expect(selection.count.value).toBe(0)
  })

  it('is never "all selected" over no rows', () => {
    const { selection } = open([])

    selection.selectAll()

    expect(selection.allSelected.value).toBe(false)
  })

  it('drops a row that is no longer shown', async () => {
    const { rows, selection } = open()

    selection.selectAll()
    rows.value = [4, 5, 6]
    await nextTick()

    expect([...selection.ids.value].sort()).toEqual([4, 5])
  })

  it('forgets the range start when the selection is cleared', () => {
    const { selection } = open()

    selection.toggle(1)
    selection.clear()
    selection.toggle(4, true)

    expect(selection.ids.value).toEqual([4])
  })
})
