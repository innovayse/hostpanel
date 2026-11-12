/**
 * Mount tests for DemoBanner: shown only in demo mode, with the label, the countdown and the reset note.
 * Also covers the guide link: hidden when docsUrl is empty, shown with the correct href when set.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { mount } from '@vue/test-utils'
import DemoBanner from '@/components/layout/DemoBanner.vue'
import { useDemoStore } from '@/stores/demo'
import { json } from '../../helpers/fetch'
import { createTestI18n } from '../../helpers/i18n'
import { requestsOf, stubRoutes } from '../../helpers/routes'
import { stubDocsUrl } from '../../helpers/runtimeConfig'

afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

/**
 * Mounts the banner with the given demo state.
 *
 * @param enabled - Whether demo mode is on.
 * @param nextResetAt - The next reset timestamp, or null.
 * @param docsUrl - The docs address the deployment injects through `config.js`.
 * @returns The wrapper.
 */
const mountBanner = (enabled: boolean, nextResetAt: string | null, docsUrl = '') => {
  stubDocsUrl(docsUrl)
  const pinia = createPinia()
  setActivePinia(pinia)
  const store = useDemoStore()
  store.enabled = enabled
  store.nextResetAt = nextResetAt
  return mount(DemoBanner, { global: { plugins: [createTestI18n(), pinia] } })
}

describe('DemoBanner', () => {
  it('stays hidden when demo mode is off', () => {
    expect(mountBanner(false, null).find('[data-testid="demo-banner"]').exists()).toBe(false)
  })

  it('shows the label, the countdown and the reset note', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2030-01-01T00:00:00Z'))
    const wrapper = mountBanner(true, '2030-01-01T01:05:00Z')

    const text = wrapper.get('[data-testid="demo-banner"]').text()
    expect(text).toContain('Demo mode')
    expect(wrapper.get('[data-testid="demo-banner-countdown"]').text()).toContain('1h 5m')
    expect(text).toContain('reset on a schedule')
  })

  it('omits the countdown when no reset is scheduled', () => {
    const wrapper = mountBanner(true, null)

    expect(wrapper.find('[data-testid="demo-banner-countdown"]').exists()).toBe(false)
  })

  it('reloads the demo description when the countdown reaches zero', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2030-01-01T00:00:00Z'))
    const mock = stubRoutes({
      'GET /api/public/demo': () => json({ enabled: true, nextResetAt: '2030-01-02T00:00:00Z', accounts: [] }),
    })
    const wrapper = mountBanner(true, '2030-01-01T00:01:00Z')
    // A load already happened, so only a forced reload reaches the network.
    useDemoStore().loaded = true

    await vi.advanceTimersByTimeAsync(60_000)
    expect(requestsOf(mock)).toEqual(['GET /public/demo'])
    expect(useDemoStore().nextResetAt).toBe('2030-01-02T00:00:00Z')
    wrapper.unmount()
  })

  it('hides the guide link when docsUrl is empty', () => {
    const wrapper = mountBanner(true, null, '')
    expect(wrapper.find('[data-testid="demo-banner-guide"]').exists()).toBe(false)
  })

  it('shows the guide link with the correct href when docsUrl is set', () => {
    const wrapper = mountBanner(true, null, 'https://docs.example.com')
    const link = wrapper.find('[data-testid="demo-banner-guide"]')
    expect(link.exists()).toBe(true)
    expect(link.attributes('href')).toBe('https://docs.example.com')
    expect(link.attributes('rel')).toContain('noopener')
  })
})
