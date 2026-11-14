/**
 * What every searchable, filterable, paged list screen does with its address, written once.
 *
 * `useFilterQuery` keeps the filters, the search text and the page in the URL. This builds on it the parts each
 * list screen needs next and used to copy: loading whenever the address names another list, knowing whether the
 * list is narrowed, reading a filter as the enum member it stands for, and turning the applied filters into chips.
 * A screen names its filter keys, reads the result, and hands over its `load` function once it has defined it
 * (`loadOnChange`); it keeps only what is its own: the table, the filter fields and the labels.
 */
import { computed, onMounted, watch } from 'vue'
import type { ComputedRef } from 'vue'
import { useFilterQuery } from '@/composables/useFilterQuery'
import type { FilterQuery } from '@/composables/useFilterQuery'
import type { UiFilterChip } from '@/utils/ui/filter'

/** One filter a screen shows as a chip: its key, the caption of its field and the value as the operator reads it. */
export interface ListChipSource<K extends string> {
  /** Key of the filter; what removing the chip reports. */
  key: K
  /** Caption of the filter field (already translated), for example "Status". */
  label: string
  /** The applied value as the operator reads it (already translated); undefined or empty when the filter is not set. */
  value: string | undefined
}

/** What {@link useListQuery} hands the list screen, on top of everything `useFilterQuery` gives. */
export interface ListQuery<K extends string> extends FilterQuery<K> {
  /** Whether the list is narrowed by the search or by a filter. */
  narrowed: ComputedRef<boolean>
  /**
   * Loads the list now and again whenever a filter, the search or the page in the address changes.
   *
   * @param load - Loads the list the address names; its result is not used.
   */
  loadOnChange: (load: () => unknown) => void
  /**
   * Reads an applied filter as a member of a closed set.
   *
   * @param key - The filter field in the address.
   * @param members - The enum (or const object) whose values the field may hold.
   * @returns A computed holding the member, undefined when the filter is not set or the address holds a value that is not a member.
   */
  enumOf: <E extends string>(key: K, members: Record<string, E>) => ComputedRef<E | undefined>
  /**
   * Reads an applied filter as the id of a record.
   *
   * @param key - The filter field in the address.
   * @returns A computed holding the id, undefined when the filter is not set or the address holds something that is not a positive whole number.
   */
  idOf: (key: K) => ComputedRef<number | undefined>
  /**
   * Builds the chips of the applied filters.
   *
   * @param describe - Lists every filter with the value it has now; the ones with no value are left out.
   * @returns A computed holding one chip per applied filter, in the order described.
   */
  chipsOf: (describe: () => ListChipSource<K>[]) => ComputedRef<UiFilterChip<K>[]>
}

/**
 * Keeps a list's filters, search and page in the address and offers the shared parts of a list screen on top.
 *
 * Must run in a component's setup, because it reads the router and registers hooks.
 *
 * @param keys - The filter fields, each stored in the query under its own name.
 * @returns Everything `useFilterQuery` returns, plus `narrowed`, `loadOnChange`, `enumOf`, `idOf` and `chipsOf`.
 */
export const useListQuery = <K extends string>(keys: readonly K[]): ListQuery<K> => {
  /** The address-backed filters, search and page. */
  const query = useFilterQuery(keys)

  /** Whether the list is narrowed by the search or by a filter. */
  const narrowed = computed<boolean>(() => query.activeCount.value > 0 || query.appliedSearch.value !== '')

  /**
   * The part of the address the list is loaded from. The router replaces the query object on every navigation, so
   * watching the values themselves would reload the list when the reader leaves for another screen; the key only
   * changes when a filter, the search or the page does.
   */
  const listKey = computed<string>(() => JSON.stringify([query.applied.value, query.appliedSearch.value, query.page.value]))

  return {
    ...query,
    narrowed,
    loadOnChange: (load) => {
      // Every change of a filter, the search or the page loads the list the address names.
      watch(listKey, () => load())
      onMounted(() => load())
    },
    enumOf: (key, members) => computed(() => Object.values(members).find(value => value === query.applied.value[key])),
    idOf: key => computed(() => {
      const id = Number(query.applied.value[key])
      return Number.isInteger(id) && id > 0 ? id : undefined
    }),
    chipsOf: describe => computed(() =>
      describe()
        .filter(source => source.value !== undefined && source.value !== '')
        .map(source => ({ key: source.key, label: source.label, value: source.value as string })),
    ),
  }
}
