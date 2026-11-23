/**
 * Tests for the scroll edge detector: a strip that fits shows no fade, one that overflows fades its end, scrolling moves
 * the fade, and a size change calls the resize callback while a scroll does not.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h, nextTick, ref } from 'vue'
import { mount } from '@vue/test-utils'
import { useScrollEdges } from '~/composables/useScrollEdges'
import type { ScrollEdges } from '~/composables/useScrollEdges'

/**
 * Mounts a strip whose layout sizes are set by the test, since jsdom lays nothing out.
 *
 * @param sizes The strip's visible width, content width and scroll position.
 * @param onResize Callback handed to the detector.
 * @returns The strip element and the edges read from it.
 */
const mountStrip = async (sizes: { clientWidth: number; scrollWidth: number; scrollLeft: number }, onResize?: () => void) => {
  /** The edges the mounted strip reports, set by its setup. */
  let edges: ScrollEdges | null = null
  const Strip = defineComponent({
    setup() {
      /** The strip element the edges are read from. */
      const element = ref<HTMLElement | null>(null)
      edges = useScrollEdges(element, onResize)
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
  return { strip, edges: edges as unknown as ScrollEdges, wrapper }
}

describe('useScrollEdges', () => {
  it('shows no fade while the content fits', async () => {
    const { edges } = await mountStrip({ clientWidth: 300, scrollWidth: 300, scrollLeft: 0 })

    expect(edges.fadeClass.value).toBe('')
  })

  it('fades the end of a strip that overflows', async () => {
    const { edges } = await mountStrip({ clientWidth: 300, scrollWidth: 600, scrollLeft: 0 })

    expect(edges.hasAfter.value).toBe(true)
    expect(edges.hasBefore.value).toBe(false)
    expect(edges.fadeClass.value).toContain('to_right,black_calc')
  })

  it('fades only the start once scrolled to the end', async () => {
    const { edges } = await mountStrip({ clientWidth: 300, scrollWidth: 600, scrollLeft: 300 })

    expect(edges.hasBefore.value).toBe(true)
    expect(edges.hasAfter.value).toBe(false)
    expect(edges.fadeClass.value).toContain('to_left')
  })

  it('does not call the resize callback on a scroll', async () => {
    let resized = 0
    await mountStrip({ clientWidth: 300, scrollWidth: 600, scrollLeft: 0 }, () => {
      resized += 1
    })

    expect(resized).toBe(0)
  })
})
