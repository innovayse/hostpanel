/**
 * Mount tests for AppSidebar: the top-level entries, sub-menus that open for the current section
 * or on click, the active marker (also on the target of a navigation still downloading), the
 * navigate event, the sign-out button, the documentation link and the theme select of the menu drawer.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { RouteLocationNormalized } from 'vue-router'
import AppSidebar from '@/components/layout/AppSidebar.vue'
import { useNavigationStore } from '@/stores/navigation'
import { useVersionStore } from '@/stores/version'
import { json, problem } from '../../helpers/fetch'
import { stubRoutes } from '../../helpers/routes'
import { mountScreen, mountView } from '../../helpers/mount'
import { resetThemeDocument, stubSchemeQuery } from '../../helpers/theme'
import { THEME_STORAGE_KEY } from '@/lib/themeBoot'
import { AdminTheme } from '@/types/admintheme'

vi.mock('@/lib/runtimeConfig', () => ({
  getRuntimeConfig: vi.fn(),
}))

import { getRuntimeConfig } from '@/lib/runtimeConfig'

/**
 * Mounts the sidebar at a path.
 *
 * @param path - The current route.
 * @param docsUrl - Docs URL to inject (default empty).
 * @returns The wrapper.
 */
const mountSidebar = async (path: string, docsUrl = '') => {
  vi.mocked(getRuntimeConfig).mockReturnValue({ docsUrl, basePath: '/', clientBaseUrl: '' })
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

  it('groups the top-level entries under labelled sections in the order an operator works', async () => {
    const wrapper = await mountSidebar('/dashboard')

    const sections = wrapper.findAll('[data-testid^="nav-section-"]:not([data-testid$="-label"])')
    expect(sections.map((section) => section.attributes('data-testid'))).toEqual([
      'nav-section-daily',
      'nav-section-insights',
      'nav-section-setup',
      'nav-section-system',
    ])
    expect(sections.map((section) => section.get('[data-testid$="-label"]').text())).toEqual([
      'Daily work',
      'Insights',
      'Setup',
      'System',
    ])

    /**
     * Lists the top-level entries of a section in display order: entries outside the sub-menus whose ids have one segment after `nav-`.
     *
     * @param index - Position of the section.
     * @returns The test ids of the section's top-level entries.
     */
    const entriesOf = (index: number): string[] => (sections[index]?.findAll('[data-testid^="nav-"]')
      .filter((entry) => entry.element.closest('[data-testid$="-submenu"]') === null)
      .map((entry) => entry.attributes('data-testid') ?? '')
      .filter((id) => /^nav-[a-z]+$/.test(id))) ?? []
    expect(entriesOf(0)).toEqual(['nav-dashboard', 'nav-clients', 'nav-orders', 'nav-billing', 'nav-domains', 'nav-support'])
    expect(entriesOf(1)).toEqual(['nav-reports'])
    expect(entriesOf(2)).toEqual(['nav-servers', 'nav-integrations', 'nav-plugins', 'nav-storefront'])
    expect(entriesOf(3)).toEqual(['nav-migration', 'nav-settings'])
  })

  it('draws the phone menu drawer in the same order as the desktop sidebar', async () => {
    const desktop = await mountSidebar('/dashboard')
    const { wrapper: embedded } = await mountScreen(AppSidebar, { embedded: true }, '/dashboard')

    /**
     * Lists every top-level entry of a sidebar (outside the sub-menus) in display order.
     *
     * @param root - The mounted sidebar.
     * @returns The test ids of the top-level entries.
     */
    const order = (root: typeof desktop): string[] => root.findAll('nav [data-testid^="nav-"]')
      .filter((entry) => entry.element.closest('[data-testid$="-submenu"]') === null)
      .map((entry) => entry.attributes('data-testid') ?? '')
      .filter((id) => /^nav-[a-z]+$/.test(id))
    expect(order(embedded)).toEqual(order(desktop))
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

  it('shows the installed version quietly in the footer', async () => {
    vi.mocked(getRuntimeConfig).mockReturnValue({ docsUrl: '', basePath: '/', clientBaseUrl: '' })
    stubRoutes({})
    const wrapper = await mountView(AppSidebar, { path: '/dashboard' })
    useVersionStore().version = '1.4.0'
    await flushPromises()

    expect(wrapper.get('[data-testid="sidebar-version"]').text()).toBe('Hostpanel 1.4.0')
  })

  it('shows no version line when the version cannot be loaded', async () => {
    vi.mocked(getRuntimeConfig).mockReturnValue({ docsUrl: '', basePath: '/', clientBaseUrl: '' })
    const mock = stubRoutes({ 'GET /api/admin/version': () => problem('No access.', 403) })
    const wrapper = await mountView(AppSidebar, { path: '/dashboard' })
    // The layout starts this load in the app; here the test starts it and waits for the refusal.
    await useVersionStore().ensureLoaded()
    await flushPromises()

    expect(mock.mock.calls.some(([url]) => String(url).endsWith('/admin/version'))).toBe(true)
    expect(useVersionStore().error).toBe('No access.')

    expect(wrapper.find('[data-testid="sidebar-version"]').exists()).toBe(false)
  })

  it('offers sign-out', async () => {
    const wrapper = await mountSidebar('/dashboard')

    expect(wrapper.find('[data-testid="sidebar-sign-out"]').exists()).toBe(true)
  })

  it('hides the documentation link when docsUrl is empty', async () => {
    const wrapper = await mountSidebar('/dashboard', '')

    expect(wrapper.find('[data-testid="nav-documentation"]').exists()).toBe(false)
  })

  it('shows the documentation link with the correct href and rel when docsUrl is set', async () => {
    const wrapper = await mountSidebar('/dashboard', 'https://docs.example.com')
    const link = wrapper.find('[data-testid="nav-documentation"]')

    expect(link.exists()).toBe(true)
    expect(link.attributes('href')).toBe('https://docs.example.com')
    expect(link.attributes('rel')).toContain('noopener')
  })

  it('offers the theme switch inside the menu drawer only, and applies the choice', async () => {
    resetThemeDocument()
    stubSchemeQuery(false)
    vi.mocked(getRuntimeConfig).mockReturnValue({ docsUrl: '', basePath: '/', clientBaseUrl: '' })
    stubRoutes({})
    const { wrapper: desktop } = await mountScreen(AppSidebar, {}, '/dashboard')
    expect(desktop.find('[data-testid="sidebar-theme"]').exists()).toBe(false)

    const { wrapper: drawer } = await mountScreen(AppSidebar, { embedded: true }, '/dashboard')
    expect(drawer.get('[data-testid="sidebar-theme-system"]').attributes('aria-checked')).toBe('true')
    await drawer.get('[data-testid="sidebar-theme-light"]').trigger('click')

    expect(drawer.get('[data-testid="sidebar-theme-light"]').attributes('aria-checked')).toBe('true')
    expect(document.documentElement.classList.contains('light')).toBe(true)
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(AdminTheme.Light)
  })
})

describe('AppSidebar Add-ons', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('draws an Add-ons section before System from the menu items plugins declare', async () => {
    vi.mocked(getRuntimeConfig).mockReturnValue({ docsUrl: '', basePath: '/', clientBaseUrl: '' })
    stubRoutes({
      'GET /api/admin/plugins/ui': () => json({
        menus: [
          { pluginId: 'sites', id: 'usage', label: 'Usage', page: 'usage', icon: 'chart', order: 2 },
          { pluginId: 'sites', id: 'main', label: 'Sites', page: 'home', icon: null, order: 1 },
        ],
        pages: [
          { pluginId: 'sites', id: 'home', path: '/', title: 'Sites' },
          { pluginId: 'sites', id: 'usage', path: '/usage', title: 'Usage' },
        ],
      }),
    })
    const wrapper = await mountView(AppSidebar, { path: '/apps/sites/usage' })

    const sections = wrapper.findAll('[data-testid^="nav-section-"]:not([data-testid$="-label"])').map((section) => section.attributes('data-testid'))
    expect(sections).toEqual(['nav-section-daily', 'nav-section-insights', 'nav-section-setup', 'nav-section-addons', 'nav-section-system'])
    expect(wrapper.get('[data-testid="nav-section-addons-label"]').text()).toBe('Add-ons')
    const sites = wrapper.get('[data-testid="nav-apps-sites"]')
    expect(sites.text()).toBe('Sites')
    expect(sites.attributes('href')).toBe('/apps/sites')
    const usage = wrapper.get('[data-testid="nav-apps-sites-usage"]')
    expect(usage.attributes('href')).toBe('/apps/sites/usage')
    expect(usage.classes()).toContain('bg-primary-500/10')
    expect(sites.classes()).not.toContain('bg-primary-500/10')
  })

  it('draws no Add-ons section while no plugin declares a menu item', async () => {
    vi.mocked(getRuntimeConfig).mockReturnValue({ docsUrl: '', basePath: '/', clientBaseUrl: '' })
    stubRoutes({ 'GET /api/admin/plugins/ui': () => json({ menus: [], pages: [] }) })
    const wrapper = await mountView(AppSidebar, { path: '/dashboard' })

    expect(wrapper.find('[data-testid="nav-section-addons"]').exists()).toBe(false)
  })
})
