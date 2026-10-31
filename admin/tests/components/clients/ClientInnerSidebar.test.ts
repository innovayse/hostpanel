/**
 * Mount tests for ClientInnerSidebar: the icon rail, its entries and the active mark, and the tab strip
 * that stands in for the rail on small screens.
 */
import { describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import ClientInnerSidebar from '@/components/clients/ClientInnerSidebar.vue'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the sidebar on the given path.
 *
 * @param path - Route the router starts at.
 * @returns The wrapper.
 */
const mountSidebar = async (path: string) => (await mountWithRouter(path)).wrapper

/**
 * Mounts the sidebar on the given path and hands back the router too.
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
  const wrapper = mount(ClientInnerSidebar, {
    props: { clientId: '12' },
    global: { plugins: [createTestI18n(), router] },
  })
  await flushPromises()
  return { wrapper, router }
}

describe('ClientInnerSidebar', () => {
  it('links every section under the client path', async () => {
    const wrapper = await mountSidebar('/clients/12/summary')

    expect(wrapper.findAll('a')).toHaveLength(13)
    expect(wrapper.get('[data-testid="client-nav-invoices"]').attributes('href')).toBe('/clients/12/invoices')
    expect(wrapper.get('[data-testid="client-nav-billable"]').attributes('href')).toBe('/clients/12/billable-items')
  })

  it('marks the entry of the current route', async () => {
    const wrapper = await mountSidebar('/clients/12/tickets/4')

    expect(wrapper.get('[data-testid="client-nav-tickets"]').attributes('aria-current')).toBe('page')
    expect(wrapper.get('[data-testid="client-nav-summary"]').attributes('aria-current')).toBeUndefined()
  })

  it('offers the same sections as a tab strip for small screens, next to the rail', async () => {
    const wrapper = await mountSidebar('/clients/12/summary')

    expect(wrapper.get('[data-testid="client-nav-strip"]').attributes('role')).toBe(undefined)
    expect(wrapper.findAll('[data-testid^="client-tab-"]')).toHaveLength(13)
    expect(wrapper.get('[data-testid="client-nav-rail"]').find('[data-testid="client-nav-summary"]').exists()).toBe(true)
  })

  it('shows the section of the current route as the selected tab', async () => {
    const wrapper = await mountSidebar('/clients/12/tickets/4')

    expect(wrapper.get('[data-testid="client-tab-tickets"]').attributes('aria-selected')).toBe('true')
    expect(wrapper.get('[data-testid="client-tab-summary"]').attributes('aria-selected')).toBe('false')
  })

  it('opens the section of the tab that is picked', async () => {
    const { wrapper, router } = await mountWithRouter('/clients/12/summary')

    await wrapper.get('[data-testid="client-tab-invoices"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.path).toBe('/clients/12/invoices')
  })
})
