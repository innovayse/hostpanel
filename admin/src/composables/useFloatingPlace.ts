/**
 * Keeps a teleported, `fixed` panel next to its trigger: the place is worked out when the panel opens and again
 * when the page scrolls or the window resizes, so a panel never stays behind its trigger. Every floating list of
 * the kit (select, menu, field selector, pickers) sits on it, so the placement is written once.
 */
import { nextTick, onUnmounted, watch } from 'vue'
import type { Ref } from 'vue'
import { applyFloatingPlace, computeFloatingPlace } from '@/utils/ui/floating'
import type { FloatingOptions } from '@/utils/ui/floating'

/**
 * Places the panel under (or above) the trigger whenever it is open.
 *
 * @param trigger - The control the panel hangs from.
 * @param panel - The panel element; it exists only while open.
 * @param open - Whether the panel is open.
 * @param options - How the panel hangs from the trigger.
 */
export const useFloatingPlace = (
  trigger: Ref<HTMLElement | null>,
  panel: Ref<HTMLElement | null>,
  open: Ref<boolean>,
  options: FloatingOptions,
): void => {
  /** Measures the trigger and writes the panel's place. */
  const place = (): void => {
    if (!trigger.value || !panel.value) {
      return
    }
    // `clientWidth` and `clientHeight` are the viewport without the page scrollbars, which a `fixed` box's
    // `right` and `bottom` are measured from (`innerWidth` and `innerHeight` include them).
    const doc = document.documentElement
    const viewport = { width: doc.clientWidth, height: doc.clientHeight }
    applyFloatingPlace(panel.value, computeFloatingPlace(trigger.value.getBoundingClientRect(), viewport, options))
  }

  /**
   * Re-places the panel after the page moved under it. Scrolling the panel's own list is not a move.
   *
   * @param event - The scroll or resize event.
   */
  const onMove = (event: Event): void => {
    if (event.target instanceof Node && panel.value?.contains(event.target)) {
      return
    }
    place()
  }

  /** Starts listening for scroll (of any container) and resize. */
  const listen = (): void => {
    window.addEventListener('scroll', onMove, { capture: true, passive: true })
    window.addEventListener('resize', onMove)
  }

  /** Stops listening for scroll and resize. */
  const stopListening = (): void => {
    window.removeEventListener('scroll', onMove, true)
    window.removeEventListener('resize', onMove)
  }

  watch(open, async (isOpen) => {
    if (!isOpen) {
      stopListening()
      return
    }
    // The panel exists after the render, so its place is written then.
    await nextTick()
    // Closed again before the render finished: attach nothing.
    if (!open.value) {
      return
    }
    place()
    listen()
  })

  onUnmounted(stopListening)
}
