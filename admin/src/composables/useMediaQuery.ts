/**
 * Follows a CSS media query from script, for the few places where the surface itself changes with the screen
 * (a filter dialog on a desktop, a bottom sheet on a phone) and a class alone cannot do it.
 */
import { onBeforeUnmount, ref, toValue, watch } from 'vue'
import type { MaybeRefOrGetter, Ref } from 'vue'

/**
 * Tracks whether a media query matches, and keeps the answer current while the screen changes.
 *
 * The answer is read at once when the browser can answer, so the first render already uses it; without
 * `matchMedia` (a test document) the query never matches. A query given as a ref or a getter is followed: when it
 * changes the old listener goes and the new query is read. The listener lives as long as the component.
 *
 * @param query - A media query, for example `(max-width: 639.98px)`, or a ref or getter of one.
 * @returns A ref that is true while the query matches.
 */
export const useMediaQuery = (query: MaybeRefOrGetter<string>): Ref<boolean> => {
  /** The live query, or null when the browser cannot answer queries. */
  let list: MediaQueryList | null = null

  /** Whether the query matches right now. */
  const matches = ref(false)

  /**
   * Stores the new answer when the screen crosses the query's edge.
   *
   * @param event - The change the browser reports.
   * @returns Nothing; the ref is updated.
   */
  const onChange = (event: MediaQueryListEvent): void => {
    matches.value = event.matches
  }

  /**
   * Stops listening to the current query.
   *
   * @returns Nothing; the listener is removed.
   */
  const unbind = (): void => {
    list?.removeEventListener('change', onChange)
    list = null
  }

  /**
   * Switches to a query: reads its answer and listens to its changes.
   *
   * @param text - The media query.
   * @returns Nothing; the answer is stored.
   */
  const bind = (text: string): void => {
    unbind()
    list = typeof window.matchMedia === 'function' ? window.matchMedia(text) : null
    matches.value = list?.matches ?? false
    list?.addEventListener('change', onChange)
  }

  watch(() => toValue(query), bind, { immediate: true })

  onBeforeUnmount(unbind)

  return matches
}
