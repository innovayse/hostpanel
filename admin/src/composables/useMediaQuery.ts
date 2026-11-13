/**
 * Follows a CSS media query from script, for the few places where the surface itself changes with the screen
 * (a filter dialog on a desktop, a bottom sheet on a phone) and a class alone cannot do it.
 */
import { onBeforeUnmount, onMounted, ref } from 'vue'
import type { Ref } from 'vue'

/**
 * Tracks whether a media query matches, and keeps the answer current while the screen changes.
 *
 * The answer is read at once when the browser can answer, so the first render already uses it; without
 * `matchMedia` (a test document) the query never matches. The listener lives as long as the component.
 *
 * @param query - A media query, for example `(max-width: 639.98px)`.
 * @returns A ref that is true while the query matches.
 */
export const useMediaQuery = (query: string): Ref<boolean> => {
  /** The live query, or null when the browser cannot answer queries. */
  const list: MediaQueryList | null = typeof window.matchMedia === 'function' ? window.matchMedia(query) : null

  /** Whether the query matches right now. */
  const matches = ref(list?.matches ?? false)

  /**
   * Stores the new answer when the screen crosses the query's edge.
   *
   * @param event - The change the browser reports.
   * @returns Nothing; the ref is updated.
   */
  const onChange = (event: MediaQueryListEvent): void => {
    matches.value = event.matches
  }

  onMounted(() => {
    list?.addEventListener('change', onChange)
  })

  onBeforeUnmount(() => {
    list?.removeEventListener('change', onChange)
  })

  return matches
}
