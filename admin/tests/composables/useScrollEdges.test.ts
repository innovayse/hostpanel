/**
 * Tests for the scroll edge detector: a strip that fits shows no fade, one that overflows fades its end, and
 * scrolling moves the fade to the start and then to both sides.
 */
import { describe, expect, it, vi } from 'vitest'
import { defineComponent, h, nextTick, ref } from 'vue'
import { mount } from '@vue/test-utils'
import { useScrollEdges } from '@/composables/useScrollEdges'
import type { ScrollEdges } from '@/composables/useScrollEdges'

/**
 * Mounts a strip whose layout sizes are set by the test, since jsdom lays nothing out.
 *
 * @param sizes - The strip's visible width, content width and scroll position.
 * @returns The strip element and the edges read from it.
 */
const mountStrip = async (sizes: { clientWidth: number; scrollWidth: number; scrollLeft: number }) => {
  /** The edges the mounted strip reports, set by its setup. */
  let edges: ScrollEdges | null = null
  const Strip = defineComponent({
    setup() {
      /** The strip element the edges are read from. */
      const element = ref<HTMLElement | null>(null)
      edges = useScrollEdges(element)
      return () => h('div', { ref: element })
    },
  })
  const wrapper = mount(Strip, { attachTo: document.body })
  const strip = wrapper.element as HTMLElement
  for (const [key, value] of Object.entries(sizes)) {
    Object.defineProperty(strip, key, { value, configurable: true, writable: true })
  }
  strip.dispatchEvent(new Event('scroll'))
  await nextTick()
  return { strip, edges: edges as unknown as ScrollEdges }
}

describe('useScrollEdges', () => {
  it('shows no fade while the content fits', async () => {
    const { edges } = await mountStrip({ clientWidth: 300, scrollWidth: 300, scrollLeft: 0 })

    expect(edges.hasBefore.value).toBe(false)
    expect(edges.hasAfter.value).toBe(false)
    expect(edges.fadeClass.value).toBe('')
  })

  it('fades the end of a strip that overflows', async () => {
    const { edges } = await mountStrip({ clientWidth: 300, scrollWidth: 600, scrollLeft: 0 })

    expect(edges.hasAfter.value).toBe(true)
    expect(edges.hasBefore.value).toBe(false)
    expect(edges.fadeClass.value).toContain('to_right,black_calc')
  })

  it('fades both sides in the middle and only the start at the end', async () => {
    const { strip, edges } = await mountStrip({ clientWidth: 300, scrollWidth: 600, scrollLeft: 100 })
    expect(edges.hasBefore.value && edges.hasAfter.value).toBe(true)

    Object.defineProperty(strip, 'scrollLeft', { value: 300, configurable: true, writable: true })
    strip.dispatchEvent(new Event('scroll'))
    await nextTick()

    expect(edges.hasBefore.value).toBe(true)
    expect(edges.hasAfter.value).toBe(false)
    expect(edges.fadeClass.value).toContain('to_left')
  })
})

describe('useScrollEdges resize callback', () => {
  it('calls the callback after a size change, and not after a scroll', async () => {
    /** Callbacks of the observers created, so the test can play a resize. */
    const callbacks: ResizeObserverCallback[] = []
    vi.stubGlobal('ResizeObserver', class {
      /**
       * Keeps the callback to play later.
       *
       * @param callback - The detector's resize callback.
       */
      constructor(callback: ResizeObserverCallback) {
        callbacks.push(callback)
      }

      /** Not used by the test. */
      observe = (): void => {}

      /** Not used by the test. */
      unobserve = (): void => {}

      /** Not used by the test. */
      disconnect = (): void => {}
    })
    try {
      let resized = 0
      const Strip = defineComponent({
        setup() {
          /** The strip element the edges are read from. */
          const element = ref<HTMLElement | null>(null)
          useScrollEdges(element, () => {
            resized += 1
          })
          return () => h('div', { ref: element })
        },
      })
      const wrapper = mount(Strip, { attachTo: document.body })
      await nextTick()

      wrapper.element.dispatchEvent(new Event('scroll'))
      expect(resized).toBe(0)

      callbacks[0]?.([], {} as ResizeObserver)
      expect(resized).toBe(1)
    } finally {
      vi.unstubAllGlobals()
    }
  })
})
