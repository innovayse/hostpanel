/**
 * Mount tests for ClientSectionTabs: one tab per section, the selected mark, keyboard and click navigation.
 */
import { describe, expect, it } from 'vitest'
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
