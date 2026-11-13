/**
 * The filters, the search text and the page of a list screen, kept in the URL query.
 *
 * The address is the one source of truth: a reload, a shared link and the back button all restore the same list.
 * The filter fields are edited as a draft (the filter dialog) and only reach the address on Apply; the search box
 * writes after a pause; any change of a filter or of the search takes the list back to its first page.
 *
 * It follows the address only while the list's own route is the current one. When the reader moves to another
 * screen the query becomes that screen's, so the values stay as they were instead of dropping to empty and making
 * the list reload on its way out.
 */
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import type { ComputedRef, Ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { LocationQueryRaw } from 'vue-router'
import { readQueryString } from '@/lib/query'
import { SEARCH_DEBOUNCE_MS } from '@/lib/limits'

/** Query key the search text is stored under unless the screen names another. */
const DEFAULT_SEARCH_KEY = 'search'

/** Query key the page number is stored under unless the screen names another. */
const DEFAULT_PAGE_KEY = 'page'

/** How a change is written to the browser history. */
const FilterHistory = {
  /** A new entry, so the back button returns to the list as it was (applied filters, a page). */
  Push: 'push',
  /** The current entry is replaced, so typing in the search box does not fill the history. */
  Replace: 'replace',
} as const

/** One of the {@link FilterHistory} values. */
type FilterHistory = (typeof FilterHistory)[keyof typeof FilterHistory]

/** The value of each filter field, by key; an empty string means the filter is not set. */
export type FilterValues<K extends string> = Record<K, string>

/** What a screen may rename about where its state is kept in the address. */
interface FilterQueryOptions {
  /** Query key of the search text; `search` by default. */
  searchKey?: string
  /** Query key of the page number; `page` by default. */
  pageKey?: string
}

/** What {@link useFilterQuery} hands the list screen. */
export interface FilterQuery<K extends string> {
  /** The filters in the address right now; what the list is loaded with. */
  applied: ComputedRef<FilterValues<K>>
  /** The filters being edited in the dialog; they reach the address only on {@link FilterQuery.apply}. */
  draft: Ref<FilterValues<K>>
  /** The text of the search box; written to the address after a pause. */
  search: Ref<string>
  /** The search text in the address right now; what the list is loaded with. */
  appliedSearch: ComputedRef<string>
  /** The current page, starting at 1. */
  page: ComputedRef<number>
  /** How many filter fields are set in the address (the search text is not counted). */
  activeCount: ComputedRef<number>
  /** Writes the draft to the address and returns to the first page; does nothing when nothing changed. */
  apply: () => Promise<void>
  /** Empties the draft; the address changes only on Apply. */
  reset: () => void
  /** Throws the draft away and returns it to what the address holds. */
  cancel: () => void
  /** Clears one filter in the address at once (a removed chip). */
  remove: (key: K) => Promise<void>
  /** Clears every filter field in the address at once; the search text stays. */
  clearAll: () => Promise<void>
  /** Moves to a page; the first page leaves no `page` in the address. */
  setPage: (next: number) => Promise<void>
}

/**
 * Keeps the filter fields, the search text and the page of a list in the address.
 *
 * Must run in a component's setup, because it reads the router.
 *
 * @param keys - The filter fields, each stored in the query under its own name.
 * @param options - Where the search text and the page are stored, when not the defaults.
 * @returns The applied filters, the editable draft, the search text, the page and the actions on them.
 */
export const useFilterQuery = <K extends string>(
  keys: readonly K[],
  options: FilterQueryOptions = {},
): FilterQuery<K> => {
  /** The current route, whose query holds the list's state. */
  const route = useRoute()
  /** The router the state is written back through. */
  const router = useRouter()
  /** Query key the search text lives under: the screen's own, or the default. */
  const searchKey = options.searchKey ?? DEFAULT_SEARCH_KEY
  /** Query key the page number lives under: the screen's own, or the default. */
  const pageKey = options.pageKey ?? DEFAULT_PAGE_KEY

  /** The path of the route the list was created for; its query is the only one this composable reads. */
  const ownPath = route.path

  /**
   * Reads a value from the address while the list's own route is current, and keeps the last value once the
   * router has moved on to another route.
   *
   * @param read - Reads the value from the route.
   * @returns A computed that does not change while another route is current.
   */
  const whileOnOwnRoute = <T>(read: () => T): ComputedRef<T> =>
    computed<T>(previous => (route.path === ownPath || previous === undefined ? read() : previous))

  /** The filters in the address right now. */
  const applied = whileOnOwnRoute<FilterValues<K>>(
    () => Object.fromEntries(keys.map(key => [key, readQueryString(route.query[key])])) as FilterValues<K>,
  )

  /** The filters being edited, starting from what the address holds. */
  const draft = ref<FilterValues<K>>({ ...applied.value }) as Ref<FilterValues<K>>

  /** The text of the search box. */
  const search = ref(readQueryString(route.query[searchKey]))

  /** The search text in the address right now. */
  const appliedSearch = whileOnOwnRoute<string>(() => readQueryString(route.query[searchKey]))

  /** The current page: a whole number from 1, else the first page. */
  const page = whileOnOwnRoute<number>(() => {
    const parsed = Number.parseInt(readQueryString(route.query[pageKey]), 10)
    return Number.isInteger(parsed) && parsed >= 1 ? parsed : 1
  })

  /** How many filter fields are set. */
  const activeCount = computed<number>(() => keys.filter(key => applied.value[key] !== '').length)

  /** The pending write of the search text. */
  let searchTimer: ReturnType<typeof setTimeout> | undefined

  /**
   * Writes values into the query and returns to the first page. Nothing is navigated when every value is
   * already in the address, so a repeated Apply or an unchanged search keeps the page the reader is on.
   *
   * @param patch - The new value of each key; an empty string removes the key.
   * @param history - Whether the change is a new history entry or replaces the current one.
   * @returns A promise that settles when the navigation has finished.
   */
  const commit = async (patch: Record<string, string>, history: FilterHistory): Promise<void> => {
    const next: LocationQueryRaw = { ...route.query }
    let changed = false
    for (const [key, value] of Object.entries(patch)) {
      if (readQueryString(route.query[key]) !== value) {
        changed = true
      }
      if (value === '') {
        delete next[key]
      } else {
        next[key] = value
      }
    }
    if (!changed) {
      return
    }
    delete next[pageKey]
    await (history === FilterHistory.Push ? router.push({ query: next }) : router.replace({ query: next }))
  }

  /**
   * Builds a patch that sets the given filter keys.
   *
   * @param values - The value of each filter key.
   * @returns The patch for {@link commit}.
   */
  const patchOf = (values: FilterValues<K>): Record<string, string> =>
    Object.fromEntries(keys.map(key => [key, values[key].trim()]))

  /**
   * Writes the draft to the address (a new history entry) and returns to the first page.
   *
   * @returns A promise that settles when the navigation has finished.
   */
  const apply = (): Promise<void> => commit(patchOf(draft.value), FilterHistory.Push)

  /**
   * Empties every field of the draft; the address changes only when the reader applies.
   *
   * @returns Nothing; the draft is replaced.
   */
  const reset = (): void => {
    draft.value = Object.fromEntries(keys.map(key => [key, ''])) as FilterValues<K>
  }

  /**
   * Throws the draft away and returns it to what the address holds.
   *
   * @returns Nothing; the draft is replaced.
   */
  const cancel = (): void => {
    draft.value = { ...applied.value }
  }

  /**
   * Clears one filter in the address at once, for a removed chip.
   *
   * @param key - The filter to clear.
   * @returns A promise that settles when the navigation has finished.
   */
  const remove = (key: K): Promise<void> => commit({ [key]: '' }, FilterHistory.Push)

  /**
   * Clears every filter field in the address at once; the search text stays.
   *
   * @returns A promise that settles when the navigation has finished.
   */
  const clearAll = (): Promise<void> =>
    commit(Object.fromEntries(keys.map(key => [key, ''])), FilterHistory.Push)

  /**
   * Moves to a page (a new history entry); the first page leaves no page key in the address.
   *
   * @param next - The page to show, starting at 1.
   * @returns A promise that settles when the navigation has finished.
   */
  const setPage = async (next: number): Promise<void> => {
    const query: LocationQueryRaw = { ...route.query }
    if (next <= 1) {
      delete query[pageKey]
    } else {
      query[pageKey] = String(next)
    }
    await router.push({ query })
  }

  // The address changed (a removed chip, the back button): the dialog starts from the new filters.
  watch(applied, (values) => {
    draft.value = { ...values }
  })

  // Typing waits for a pause, so a list is not reloaded on every keystroke.
  watch(search, (text) => {
    clearTimeout(searchTimer)
    searchTimer = setTimeout(() => {
      void commit({ [searchKey]: text.trim() }, FilterHistory.Replace)
    }, SEARCH_DEBOUNCE_MS)
  })

  // The address changed under the box (the back button): show what the list is filtered by.
  watch(
    appliedSearch,
    (fromAddress) => {
      if (fromAddress !== search.value.trim()) {
        search.value = fromAddress
      }
    },
  )

  onBeforeUnmount(() => {
    clearTimeout(searchTimer)
  })

  return { applied, draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage }
}
