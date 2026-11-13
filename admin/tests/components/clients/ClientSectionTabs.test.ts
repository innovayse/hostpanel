/**
 * Mount tests for ClientSectionTabs: one tab per section, the selected mark, keyboard and click navigation, and
 * keeping the selected tab in view.
 */
import { describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import ClientSectionTabs from '@/components/clients/ClientSectionTabs.vue'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the tabs on the given path.
 *
 * @param path - Route the router starts at.
 * @returns The wrapper.
 */
const mountTabs = async (path: string) => (await mountWithRouter(path)).wrapper

/**
 * Mounts the tabs on the given path and hands back the router too.
 *
 * @param path - Route the router starts at.
 * @returns The wrapper and the router.
 */
const mountWithRouter = async (path: string) => {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/:pathMatch(.*)*', component: { render: () => null } }],
  })
  await router.push(path)
  await router.isReady()
  const wrapper = mount(ClientSectionTabs, {
    props: { clientId: '12' },
    global: { plugins: [createTestI18n(), router] },
  })
  await flushPromises()
  return { wrapper, router }
}

describe('ClientSectionTabs', () => {
  it('offers every section as a tab', async () => {
    const wrapper = await mountTabs('/clients/12/summary')

    const strip = wrapper.get('[data-testid="client-nav-strip"]')
    expect(strip.find('[role="tablist"]').exists()).toBe(true)
    expect(strip.findAll('[role="tab"]')).toHaveLength(14)
    expect(wrapper.findAll('[data-testid^="client-tab-"]')).toHaveLength(14)
  })

  it('shows the section of the current route as the selected tab', async () => {
    const wrapper = await mountTabs('/clients/12/tickets/4')

    expect(wrapper.get('[data-testid="client-tab-tickets"]').attributes('aria-selected')).toBe('true')
    expect(wrapper.get('[data-testid="client-tab-summary"]').attributes('aria-selected')).toBe('false')
  })

  it('moves to the next and the previous section with the arrow keys', async () => {
    const { wrapper, router } = await mountWithRouter('/clients/12/summary')

    await wrapper.get('[data-testid="client-tab-summary"]').trigger('keydown', { key: 'ArrowRight' })
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/clients/12/profile')

    await wrapper.get('[data-testid="client-tab-profile"]').trigger('keydown', { key: 'ArrowLeft' })
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/clients/12/summary')
  })

  it('names the tab list', async () => {
    const wrapper = await mountTabs('/clients/12/summary')

    expect(wrapper.get('[data-testid="client-nav-strip"] [role="tablist"]').attributes('aria-label')).toBe('Client section tabs')
  })

  it('brings the selected tab into view on load and when the route changes', async () => {
    const scrolled: string[] = []
    const original = HTMLElement.prototype.scrollIntoView
    HTMLElement.prototype.scrollIntoView = function scrollIntoView(this: HTMLElement) {
      scrolled.push(this.getAttribute('data-testid') ?? '')
    }
    try {
      const { router } = await mountWithRouter('/clients/12/log')
      await flushPromises()
      expect(scrolled).toContain('client-tab-log')

      await router.push('/clients/12/invoices')
      await flushPromises()
      expect(scrolled).toContain('client-tab-invoices')
    } finally {
      HTMLElement.prototype.scrollIntoView = original
    }
  })

  it('brings the selected tab back into view when the strip changes size, and stops watching once left', async () => {
    /** Callbacks of the observers the component created, so the test can play a resize. */
    const callbacks: ResizeObserverCallback[] = []
    /** How many observers were disconnected. */
    let disconnected = 0
    /** Every element an observer was asked to watch. */
    const observed: Element[] = []
    vi.stubGlobal('ResizeObserver', class {
      /**
       * Keeps the callback to play later.
       *
       * @param callback - The component's resize callback.
       */
      constructor(callback: ResizeObserverCallback) {
        callbacks.push(callback)
      }

      /**
       * Records the observed element.
       *
       * @param target - The element the component watches.
       */
      observe = (target: Element): void => {
        observed.push(target)
      }

      /** Not used by the component. */
      unobserve = (): void => {}

      /** Counts the disconnect on unmount. */
      disconnect = (): void => {
        disconnected += 1
      }
    })
    const scrolled: string[] = []
    const original = HTMLElement.prototype.scrollIntoView
    HTMLElement.prototype.scrollIntoView = function scrollIntoView(this: HTMLElement) {
      scrolled.push(this.getAttribute('data-testid') ?? '')
    }
    try {
      const { wrapper } = await mountWithRouter('/clients/12/log')
      await flushPromises()
      scrolled.length = 0
      expect(callbacks).toHaveLength(1)
      // The list keeps its box when a tab changes width (it scrolls), so every tab is watched as well.
      expect(observed).toContain(wrapper.get('[role="tablist"]').element)
      for (const tab of wrapper.findAll('[role="tab"]')) {
        expect(observed).toContain(tab.element)
      }

      callbacks[0]?.([], {} as ResizeObserver)
      await flushPromises()
      expect(scrolled).toEqual(['client-tab-log'])

      wrapper.unmount()
      expect(disconnected).toBe(1)
    } finally {
      HTMLElement.prototype.scrollIntoView = original
      vi.unstubAllGlobals()
    }
  })

  it('opens the section of the tab that is picked', async () => {
    const { wrapper, router } = await mountWithRouter('/clients/12/summary')

    await wrapper.get('[data-testid="client-tab-invoices"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.path).toBe('/clients/12/invoices')
  })

  it('opens the billable items section under its own path segment', async () => {
    const { wrapper, router } = await mountWithRouter('/clients/12/summary')

    await wrapper.get('[data-testid="client-tab-billable"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.path).toBe('/clients/12/billable-items')
  })
})
