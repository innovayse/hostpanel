/**
 * Unit tests for `LayoutDemoBanner`: hidden outside a demo; inside one it shows the label, the
 * countdown to the next reset and the data-resets note.
 *
 * @module tests/components/layout/DemoBanner.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import LayoutDemoBanner from '~/components/layout/DemoBanner.vue'
import { useDemoStore } from '~/stores/demo'

afterEach(() => { vi.unstubAllGlobals() })

/** The moment the fake clock is frozen at. */
const NOW = new Date('2030-01-01T00:00:00Z')

beforeEach(() => {
  vi.useFakeTimers()
  vi.setSystemTime(NOW)
})

afterEach(() => {
  vi.useRealTimers()
})

/**
 * Mounts the banner with the demo store in the given state.
 *
 * @param state - Fields to put on the store before mounting.
 * @returns The wrapper.
 */
/**
 * Mounts the banner with the demo store in the given state and an optional docsUrl.
 *
 * @param state - Fields to put on the store before mounting.
 * @param docsUrl - Value for `useRuntimeConfig().public.docsUrl`; defaults to empty.
 * @returns The wrapper.
 */
const mountBanner = (state: { enabled: boolean, nextResetAt: string | null }, docsUrl = '') => {
  vi.stubGlobal('useRuntimeConfig', () => ({ public: { docsUrl } }))
  const pinia = createPinia()
  setActivePinia(pinia)
  const store = useDemoStore()
  store.enabled = state.enabled
  store.nextResetAt = state.nextResetAt
  return mountWithKit(LayoutDemoBanner, { pinia })
}

describe('LayoutDemoBanner', () => {
  it('renders nothing outside a demo', () => {
    const wrapper = mountBanner({ enabled: false, nextResetAt: null })

    expect(wrapper.find('[data-testid="demo-banner"]').exists()).toBe(false)
  })

  it('shows the label, the time to reset and the note in a demo', () => {
    const wrapper = mountBanner({ enabled: true, nextResetAt: '2030-01-01T02:15:00Z' })

    expect(wrapper.find('[data-testid="demo-banner-label"]').text()).toBe('demo.label')
    expect(wrapper.find('[data-testid="demo-banner-reset"]').text()).toBe('demo.resetsIn')
    expect(wrapper.find('[data-testid="demo-banner-note"]').text()).toBe('demo.note')
  })

  it('omits the reset line when no reset is scheduled', () => {
    const wrapper = mountBanner({ enabled: true, nextResetAt: null })

    expect(wrapper.find('[data-testid="demo-banner"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="demo-banner-reset"]').exists()).toBe(false)
  })

  it('hides the guide link when docsUrl is empty', () => {
    const wrapper = mountBanner({ enabled: true, nextResetAt: null })

    expect(wrapper.find('[data-testid="demo-banner-guide"]').exists()).toBe(false)
  })

  it('shows the guide link pointing to the customer portal page when docsUrl is set', () => {
    const wrapper = mountBanner({ enabled: true, nextResetAt: null }, 'https://docs.example.com')

    const link = wrapper.find('[data-testid="demo-banner-guide"]')
    expect(link.exists()).toBe(true)
    expect(link.attributes('href')).toBe('https://docs.example.com/customers/portal')
    expect(link.attributes('rel')).toContain('noopener')
  })

  it('lays the content out inside the page container, centred and wrapping on narrow screens', () => {
    const wrapper = mountBanner({ enabled: true, nextResetAt: null })
    const container = wrapper.get('[data-testid="demo-banner-container"]')
    const row = container.get('[data-testid="demo-banner-label"]').element.parentElement!

    expect(container.classes()).toContain('container-custom')
    expect(row.className).toContain('items-center')
    expect(row.className).toContain('flex-wrap')
    expect(wrapper.get('[data-testid="demo-banner-label"]').classes()).toContain('shrink-0')
  })
})
