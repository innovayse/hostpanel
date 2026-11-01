/**
 * Mount tests for AppSidebar: the top-level entries, sub-menus that open for the current section
 * or on click, the active marker (also on the target of a navigation still downloading), the
 * navigate event and the sign-out button.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { RouteLocationNormalized } from 'vue-router'
import AppSidebar from '@/components/layout/AppSidebar.vue'
import { useNavigationStore } from '@/stores/navigation'
import { stubRoutes } from '../../helpers/routes'
import { mountScreen, mountView } from '../../helpers/mount'

/**
 * Mounts the sidebar at a path.
 *
 * @param path - The current route.
 * @returns The wrapper.
 */
const mountSidebar = async (path: string) => {
  stubRoutes({})
  return mountView(AppSidebar, { path })
}

describe('AppSidebar', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('renders every top-level entry as a link or a section toggle', async () => {
    const wrapper = await mountSidebar('/dashboard')

    expect(wrapper.get('[data-testid="nav-dashboard"]').attributes('href')).toBe('/dashboard')
    expect(wrapper.get('[data-testid="nav-servers"]').attributes('href')).toBe('/servers')
    expect(wrapper.get('[data-testid="nav-migration"]').attributes('href')).toBe('/migration')
    expect(wrapper.get('[data-testid="nav-clients"]').element.tagName).toBe('BUTTON')
  })

  it('shows the logo and a fixed width on the desktop, and neither inside the menu drawer', async () => {
    stubRoutes({})
    const desktop = await mountView(AppSidebar, { path: '/dashboard' })
    expect(desktop.get('[data-testid="app-sidebar"]').classes()).toContain('w-[220px]')
    expect(desktop.find('[data-testid="app-sidebar"] img, [data-testid="app-sidebar"] svg').exists()).toBe(true)

    const { wrapper: embedded } = await mountScreen(AppSidebar, { embedded: true }, '/dashboard')
    expect(embedded.get('[data-testid="app-sidebar"]').classes()).toEqual(expect.arrayContaining(['w-full']))
    expect(embedded.get('[data-testid="app-sidebar"]').classes()).not.toContain('w-[220px]')
    expect(embedded.find('[data-testid="app-sidebar"] > div.border-b').exists()).toBe(false)
  })

  it('opens the sub-menu of the current section and marks the current child', async () => {
    const wrapper = await mountSidebar('/clients/users')

    expect(wrapper.get('[data-testid="nav-clients"]').attributes('aria-expanded')).toBe('true')
    expect(wrapper.get('[data-testid="nav-clients-users"]').attributes('href')).toBe('/clients/users')
    expect(wrapper.get('[data-testid="nav-clients-users"]').classes()).toContain('text-primary-400')
  })

  it('lists the five storefront content pages and opens the section on one of them', async () => {
    const wrapper = await mountSidebar('/storefront/faqs')

    expect(wrapper.get('[data-testid="nav-storefront"]').attributes('aria-expanded')).toBe('true')
    expect(wrapper.get('[data-testid="nav-storefront-testimonials"]').attributes('href')).toBe('/storefront/testimonials')
    expect(wrapper.get('[data-testid="nav-storefront-partners"]').attributes('href')).toBe('/storefront/partners')
    expect(wrapper.get('[data-testid="nav-storefront-projects"]').attributes('href')).toBe('/storefront/projects')
    expect(wrapper.get('[data-testid="nav-storefront-faqs"]').classes()).toContain('text-primary-400')
    expect(wrapper.get('[data-testid="nav-storefront-locations"]').attributes('href')).toBe('/storefront/locations')
  })

  it('keeps a closed sub-menu out of reach and opens it over a transition, not by switching it on', async () => {
    const wrapper = await mountSidebar('/dashboard')
    const submenu = wrapper.get('[data-testid="nav-billing-submenu"]')

    expect(submenu.attributes('inert')).toBeDefined()
    expect(submenu.classes()).toEqual(expect.arrayContaining(['grid-rows-[0fr]', 'opacity-0', 'duration-300']))

    await wrapper.get('[data-testid="nav-billing"]').trigger('click')

    expect(submenu.attributes('inert')).toBeUndefined()
    expect(submenu.classes()).toEqual(expect.arrayContaining(['grid-rows-[1fr]', 'opacity-100']))
  })

  it('gives the list a quiet scrollbar that shows on hover', async () => {
    const wrapper = await mountSidebar('/dashboard')

    expect(wrapper.get('nav').classes()).toContain('scroll-subtle')
  })

  it('opens a closed sub-menu when its toggle is clicked', async () => {
    const wrapper = await mountSidebar('/dashboard')
    expect(wrapper.get('[data-testid="nav-billing"]').attributes('aria-expanded')).toBe('false')

    await wrapper.get('[data-testid="nav-billing"]').trigger('click')

    expect(wrapper.get('[data-testid="nav-billing"]').attributes('aria-expanded')).toBe('true')
  })

  it('marks the target of a navigation still downloading its screen', async () => {
    const wrapper = await mountSidebar('/dashboard')

    useNavigationStore().pending = { path: '/clients/users', matched: [] } as unknown as RouteLocationNormalized
    await flushPromises()

    expect(wrapper.get('[data-testid="nav-clients"]').attributes('aria-expanded')).toBe('true')
    expect(wrapper.get('[data-testid="nav-clients-users"]').classes()).toContain('text-primary-400')
  })

  it('emits navigate when a link is clicked', async () => {
    const wrapper = await mountSidebar('/dashboard')

    await wrapper.get('[data-testid="nav-orders"]').trigger('click')
    await flushPromises()

    expect(wrapper.emitted('navigate')).toHaveLength(1)
  })

  it('renders the built-in wordmark for each theme', async () => {
    const wrapper = await mountSidebar('/dashboard')

    expect(wrapper.get('[data-testid="brand-logo-dark"]').attributes('src')).toBe('/logo-dark.svg')
    expect(wrapper.get('[data-testid="brand-logo-light"]').attributes('src')).toBe('/logo.svg')
    expect(wrapper.get('[data-testid="brand-logo-dark"]').attributes('alt')).toBe('Hostpanel')
  })

  it('offers sign-out', async () => {
    const wrapper = await mountSidebar('/dashboard')

    expect(wrapper.find('[data-testid="sidebar-sign-out"]').exists()).toBe(true)
  })
})
