/**
 * Tests for useFilterDateRange: the picker's `[start, end]` pair is read from and written to two fields of the
 * filter draft.
 */
import { describe, expect, it } from 'vitest'
import { ref } from 'vue'
import type { Ref } from 'vue'
import { useFilterDateRange } from '@/composables/useFilterDateRange'
import type { FilterValues } from '@/composables/useFilterQuery'

/** The two draft fields the range maps onto. */
type Key = 'from' | 'to'

/**
 * Builds a draft with both ends set to the given texts.
 *
 * @param from - Start of the period, empty for none.
 * @param to - End of the period, empty for none.
 * @returns The draft as a ref.
 */
const draftOf = (from: string, to: string): Ref<FilterValues<Key>> => ref<FilterValues<Key>>({ from, to })

describe('useFilterDateRange', () => {
  it('reads the pair while both ends are set', () => {
    const range = useFilterDateRange(draftOf('2026-10-01', '2026-10-31'), 'from', 'to')

    expect(range.value).toEqual(['2026-10-01', '2026-10-31'])
  })

  it('reads null while one end is missing', () => {
    expect(useFilterDateRange(draftOf('2026-10-01', ''), 'from', 'to').value).toBeNull()
    expect(useFilterDateRange(draftOf('', ''), 'from', 'to').value).toBeNull()
  })

  it('writes both ends of a picked pair into the draft', () => {
    const draft = draftOf('', '')
    const range = useFilterDateRange(draft, 'from', 'to')

    range.value = ['2026-11-01', '2026-11-30']

    expect(draft.value).toEqual({ from: '2026-11-01', to: '2026-11-30' })
  })

  it('clears both ends when the pair is cleared', () => {
    const draft = draftOf('2026-10-01', '2026-10-31')
    const range = useFilterDateRange(draft, 'from', 'to')

    range.value = null

    expect(draft.value).toEqual({ from: '', to: '' })
  })
})
