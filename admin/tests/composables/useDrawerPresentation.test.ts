/**
 * Tests for `useDrawerPresentation`: a phone gets the bottom sheet, every wider screen the side shape, and the answer
 * follows the screen when it crosses the `sm` edge.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { useDrawerPresentation } from '@/composables/useDrawerPresentation'
import { UI_BELOW_SM_QUERY } from '@/utils/ui/breakpoints'
import { UiDrawerPresentation } from '@/utils/ui/drawer'

/**
 * Stubs `matchMedia` with one fake query.
 *
 * @param matches - Whether it matches at first.
 * @returns The queries asked for and the change listeners registered on the fake.
 */
const stubQuery = (matches: boolean) => {
  const asked: string[] = []
  const listeners: Array<(event: { matches: boolean }) => void> = []
  vi.stubGlobal('matchMedia', (query: string) => {
    asked.push(query)
    return {
      matches,
      addEventListener: (_type: string, listener: (event: { matches: boolean }) => void) => listeners.push(listener),
      removeEventListener: vi.fn(),
    }
  })
  return { asked, listeners }
}

/**
 * Mounts a component that prints the answer.
 *
 * @returns The wrapper; its text is the presentation.
 */
const mountProbe = () =>
  mount(defineComponent({
    setup: () => {
      const presentation = useDrawerPresentation()
      return () => h('span', presentation.value)
    },
  }))

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useDrawerPresentation', () => {
  it('is a sheet on a phone, asked through the below-sm query', () => {
    const { asked } = stubQuery(true)

    expect(mountProbe().text()).toBe(UiDrawerPresentation.Sheet)
    expect(asked).toEqual([UI_BELOW_SM_QUERY])
  })

  it('is the side shape on a wider screen', () => {
    stubQuery(false)

    expect(mountProbe().text()).toBe(UiDrawerPresentation.Side)
  })

  it('follows the screen across the edge', async () => {
    const { listeners } = stubQuery(false)
    const wrapper = mountProbe()

    listeners.forEach((listener) => listener({ matches: true }))
    await wrapper.vm.$nextTick()

    expect(wrapper.text()).toBe(UiDrawerPresentation.Sheet)
  })
})
