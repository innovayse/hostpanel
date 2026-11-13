/**
 * Tests for the media query follower: the first answer, a change of the screen, and the listener leaving with the
 * component.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
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
 * @returns The wrapper; its text is the answer.
 */
const mountProbe = () => {
  const Probe = defineComponent({
    setup() {
      const matches = useMediaQuery('(max-width: 10px)')
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
})
