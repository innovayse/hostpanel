/**
 * Lets a date-range picker edit two separate filter fields of a list.
 *
 * `useFilterQuery` keeps every filter as one text under its own query key, while `UiDateRangePicker` edits a
 * `[start, end]` pair. This maps the pair onto the draft's two keys, so a list that filters by a period keeps
 * `from` and `to` in the address and still uses the kit's picker.
 */
import { computed } from 'vue'
import type { Ref, WritableComputedRef } from 'vue'
import type { FilterValues } from '@/composables/useFilterQuery'

/**
 * Creates the pair the range picker binds to, over two fields of the filter draft.
 *
 * @param draft - The draft of the screen's `useFilterQuery`.
 * @param fromKey - The draft field holding the start of the period.
 * @param toKey - The draft field holding the end of the period.
 * @returns A writable pair: `[start, end]` while both ends are set, `null` otherwise; writing it sets or clears both ends.
 */
export const useFilterDateRange = <K extends string>(
  draft: Ref<FilterValues<K>>,
  fromKey: K,
  toKey: K,
): WritableComputedRef<[string, string] | null> =>
  computed<[string, string] | null>({
    /**
     * Reads the draft's two keys as a range.
     *
     * @returns The from/to pair, or null while either end is empty.
     */
    get: (): [string, string] | null => {
      const from: string = draft.value[fromKey]
      const to: string = draft.value[toKey]
      return from !== '' && to !== '' ? [from, to] : null
    },
    /**
     * Writes a picked range back into the draft's two keys.
     *
     * @param range - The picked from/to pair, or null when the range is cleared.
     */
    set: (range) => {
      draft.value[fromKey] = range?.[0] ?? ''
      draft.value[toKey] = range?.[1] ?? ''
    },
  })
