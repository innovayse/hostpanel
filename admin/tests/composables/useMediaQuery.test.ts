/**
 * Tests for the media query follower: the first answer, a change of the screen, and the listener leaving with the
 * component.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, ref } from 'vue'
import { mount } from '@vue/test-utils'
import type { Ref } from 'vue'
import { useMediaQuery } from '@/composables/useMediaQuery'

/** The fake query the stub hands out. */
interface FakeQuery {
  /** Whether the query matches. */
  matches: boolean
  /** Adds a listener. */
  addEventListener: ReturnType<typeof vi.fn>
  /** Removes a listener. */
  removeEventListener: ReturnType<typeof vi.fn>
}

/**
 * Stubs `matchMedia` with one fake query.
 *
 * @param matches - Whether it matches at first.
 * @returns The fake query.
 */
const stubQuery = (matches: boolean): FakeQuery => {
  const query: FakeQuery = { matches, addEventListener: vi.fn(), removeEventListener: vi.fn() }
  vi.stubGlobal('matchMedia', () => query)
  return query
}

/**
 * Mounts a component that uses the composable.
 *
 * @param query - The query, or a ref of one.
 * @returns The wrapper; its text is the answer.
 */
const mountProbe = (query: string | Ref<string> = '(max-width: 10px)') => {
  const Probe = defineComponent({
    setup() {
      const matches = useMediaQuery(query)
      return () => h('span', String(matches.value))
    },
  })
  return mount(Probe)
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useMediaQuery', () => {
  it('answers at once from the browser', () => {
    stubQuery(true)

    expect(mountProbe().text()).toBe('true')
  })

  it('never matches where the browser cannot answer queries', () => {
    vi.stubGlobal('matchMedia', undefined)

    expect(mountProbe().text()).toBe('false')
  })

  it('follows the screen and lets the listener go on unmount', async () => {
    const query = stubQuery(false)
    const wrapper = mountProbe()
    const listener = query.addEventListener.mock.calls[0]?.[1] as (event: { matches: boolean }) => void

    listener({ matches: true })
    await wrapper.vm.$nextTick()

    expect(wrapper.text()).toBe('true')
    wrapper.unmount()
    expect(query.removeEventListener).toHaveBeenCalledWith('change', listener)
  })

  it('follows a query that changes: the old listener goes and the new answer is read', async () => {
    const wide: FakeQuery = { matches: false, addEventListener: vi.fn(), removeEventListener: vi.fn() }
    const narrow: FakeQuery = { matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() }
    vi.stubGlobal('matchMedia', (text: string) => (text === 'narrow' ? narrow : wide))
    const query = ref('wide')
    const wrapper = mountProbe(query)
    expect(wrapper.text()).toBe('false')

    query.value = 'narrow'
    await wrapper.vm.$nextTick()

    expect(wrapper.text()).toBe('true')
    expect(wide.removeEventListener).toHaveBeenCalledTimes(1)
    expect(narrow.addEventListener).toHaveBeenCalledTimes(1)
  })
})
