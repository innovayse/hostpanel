/**
 * Tells which sides of a horizontally scrolling strip still hide content, so a cut label reads as more to scroll to.
 *
 * @module composables/useScrollEdges
 */

import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import type { ComputedRef, Ref } from 'vue'

/** Distance in pixels under which an edge counts as reached, absorbing sub-pixel scroll positions. */
const EDGE_TOLERANCE = 1

/** What {@link useScrollEdges} hands back to the strip. */
export interface ScrollEdges {
  /** True while content is hidden before the visible part (the strip was scrolled away from its start). */
  hasBefore: Ref<boolean>
  /** True while content is hidden after the visible part (the strip continues past its end). */
  hasAfter: Ref<boolean>
  /** The fade class of the strip for the sides that hide content, empty when nothing is hidden. */
  fadeClass: ComputedRef<string>
}

/**
 * Watches a horizontally scrolling element and reports which of its sides hide content.
 *
 * The state is read on mount, on every scroll and whenever the element or its children change size, so a strip
 * that only overflows after fonts or tabs load still shows its edge. Left to right layout is assumed.
 *
 * @param element The scrolling element, null until it is mounted.
 * @param onResize Called after each re-measure caused by a size change (not by scrolling), for a strip that keeps something in view.
 * @returns The two side flags and the fade class that draws them.
 */
export const useScrollEdges = (element: Ref<HTMLElement | null>, onResize?: () => void): ScrollEdges => {
  /** Whether content is hidden before the visible part. */
  const hasBefore = ref(false)

  /** Whether content is hidden after the visible part. */
  const hasAfter = ref(false)

  /** Reads the scroll position of the element into the two flags. */
  const measure = (): void => {
    const strip = element.value
    if (strip === null) {
      hasBefore.value = false
      hasAfter.value = false
      return
    }
    hasBefore.value = strip.scrollLeft > EDGE_TOLERANCE
    hasAfter.value = strip.scrollLeft + strip.clientWidth < strip.scrollWidth - EDGE_TOLERANCE
  }

  /** Re-measures when the element or any of its children change size. */
  let observer: ResizeObserver | null = null

  /**
   * Starts listening on the element, and stops listening on the one it replaces.
   *
   * @param next The element now in use.
   * @param previous The element no longer in use.
   */
  const attach = (next: HTMLElement | null, previous: HTMLElement | null | undefined): void => {
    previous?.removeEventListener('scroll', measure)
    observer?.disconnect()
    if (next === null) {
      measure()
      return
    }
    next.addEventListener('scroll', measure, { passive: true })
    observer = new ResizeObserver(() => {
      measure()
      onResize?.()
    })
    observer.observe(next)
    Array.from(next.children).forEach((child) => {
      observer?.observe(child)
    })
    measure()
  }

  onMounted(() => {
    watch(element, attach, { immediate: true, flush: 'post' })
  })

  onBeforeUnmount(() => {
    element.value?.removeEventListener('scroll', measure)
    observer?.disconnect()
  })

  /** The fade for the sides that hide content: a gradient mask that dissolves the strip toward that edge. */
  const fadeClass = computed<string>(() => {
    if (hasBefore.value && hasAfter.value) {
      return '[mask-image:linear-gradient(to_right,transparent,black_2rem,black_calc(100%-2rem),transparent)]'
    }
    if (hasAfter.value) {
      return '[mask-image:linear-gradient(to_right,black_calc(100%-2rem),transparent)]'
    }
    if (hasBefore.value) {
      return '[mask-image:linear-gradient(to_left,black_calc(100%-2rem),transparent)]'
    }
    return ''
  })

  return { hasBefore, hasAfter, fadeClass }
}
