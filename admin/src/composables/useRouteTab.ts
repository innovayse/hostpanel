/**
 * The selected tab of a screen, kept in the `tab` query key of the address.
 *
 * The address is the one source of truth: a reload, a shared link and the back button restore the same tab.
 * The default tab leaves no key in the address, so the plain screen URL stays the canonical one.
 */
import { computed } from 'vue'
import type { ComputedRef } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { LocationQueryRaw } from 'vue-router'
import { readQueryString } from '@/lib/query'

/** Query key the tab is stored under. */
const TAB_KEY = 'tab'

/** What {@link useRouteTab} hands the screen. */
export interface RouteTab<T extends string> {
  /** The tab in the address, or the default when the address holds none or an unknown one. */
  tab: ComputedRef<T>
  /** Moves to a tab (a new history entry); the default tab removes the key. */
  select: (next: T) => Promise<void>
}

/**
 * Keeps the selected tab of a screen in the address.
 *
 * Must run in a component's setup, because it reads the router.
 *
 * @param tabs - The enum of the screen's tabs; its values are the accepted query values.
 * @param fallback - The tab shown when the address names none.
 * @returns The current tab and the action that selects another.
 */
export const useRouteTab = <T extends string>(tabs: Record<string, T>, fallback: T): RouteTab<T> => {
  /** The current route, whose query holds the tab. */
  const route = useRoute()
  /** The router the tab is written back through. */
  const router = useRouter()
  /** Every value the enum accepts. */
  const accepted: readonly string[] = Object.values(tabs)

  /** The tab in the address, falling back for a missing or unknown value. */
  const tab = computed<T>(() => {
    const raw = readQueryString(route.query[TAB_KEY])
    // The value was just checked against the enum's own values, so the cast only restores its type.
    return accepted.includes(raw) ? (raw as T) : fallback
  })

  /**
   * Moves to a tab as a new history entry; other query keys stay.
   *
   * @param next - The tab to show.
   * @returns A promise that settles when the navigation has finished.
   */
  const select = async (next: T): Promise<void> => {
    const query: LocationQueryRaw = { ...route.query }
    if (next === fallback) {
      delete query[TAB_KEY]
    } else {
      query[TAB_KEY] = next
    }
    await router.push({ query })
  }

  return { tab, select }
}
