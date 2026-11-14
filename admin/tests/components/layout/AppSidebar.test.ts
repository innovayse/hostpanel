/**
 * Mount tests for AppSidebar: the top-level entries, sub-menus that open for the current section
 * or on click, the active marker (also on the target of a navigation still downloading), the
 * navigate event, the fold to an icon rail, the documentation link and the theme select of the menu drawer.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { RouteLocationNormalized } from 'vue-router'
import AppSidebar from '@/components/layout/AppSidebar.vue'
import { useBrandingStore } from '@/stores/branding'
import { useDashboardStore } from '@/stores/dashboard'
import { useNavigationStore } from '@/stores/navigation'
import { SIDEBAR_STORAGE_KEY } from '@/stores/sidebar'
import { useVersionStore } from '@/stores/version'
import { json, problem } from '../../helpers/fetch'
import { stubRoutes } from '../../helpers/routes'
import { stubDocsUrl } from '../../helpers/runtimeConfig'
import { mountScreen, mountView } from '../../helpers/mount'
import { resetThemeDocument, stubSchemeQuery } from '../../helpers/theme'
import { THEME_STORAGE_KEY } from '@/lib/themeBoot'
import { AdminTheme } from '@/types/admintheme'

/**
 * Mounts the sidebar at a path.
 *
 * @param path - The current route.
 * @param docsUrl - Docs address the deployment injects through `config.js` (default empty).
 * @returns The wrapper.
 */
const mountSidebar = async (path: string, docsUrl = '') => {
  stubDocsUrl(docsUrl)
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

  it('shows the product mark and name and a fixed width on the desktop, and neither inside the menu drawer', async () => {
    stubRoutes({})
    const desktop = await mountView(AppSidebar, { path: '/dashboard' })
    expect(desktop.get('[data-testid="app-sidebar"]').classes()).toEqual(expect.arrayContaining(['w-62', 'border-r', 'bg-surface-sidebar']))
    expect(desktop.get('[data-testid="sidebar-brand-mark"]').attributes('src')).toBe('/favicon.svg')
    expect(desktop.get('[data-testid="sidebar-brand-name"]').text()).toBe('Hostpanel')

    const { wrapper: embedded } = await mountScreen(AppSidebar, { embedded: true }, '/dashboard')
    expect(embedded.get('[data-testid="app-sidebar"]').classes()).toEqual(expect.arrayContaining(['w-full']))
    expect(embedded.get('[data-testid="app-sidebar"]').classes()).not.toContain('w-62')
    expect(embedded.find('[data-testid="sidebar-brand-mark"]').exists()).toBe(false)
    expect(embedded.find('[data-testid="sidebar-brand-name"]').exists()).toBe(false)
  })

  it('shows the operator logo instead of the mark and name when one is uploaded', async () => {
    stubRoutes({})
    const { wrapper } = await mountScreen(AppSidebar, {}, '/dashboard')
    const branding = useBrandingStore()
    branding.logoUrl = '/logo-custom.png'
    await flushPromises()

    expect(wrapper.find('[data-testid="brand-logo-dark"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="sidebar-brand-mark"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="sidebar-brand-name"]').exists()).toBe(false)
  })

  it('opens the sub-menu of the current section and marks the current child', async () => {
    const wrapper = await mountSidebar('/clients/users')

    expect(wrapper.get('[data-testid="nav-clients"]').attributes('aria-expanded')).toBe('true')
    expect(wrapper.get('[data-testid="nav-clients-users"]').attributes('href')).toBe('/clients/users')
    expect(wrapper.get('[data-testid="nav-clients-users"]').classes()).toContain('text-text-accent')
  })

  it('lists the five storefront content pages and opens the section on one of them', async () => {
    const wrapper = await mountSidebar('/storefront/faqs')

    expect(wrapper.get('[data-testid="nav-storefront"]').attributes('aria-expanded')).toBe('true')
    expect(wrapper.get('[data-testid="nav-storefront-testimonials"]').attributes('href')).toBe('/storefront/testimonials')
    expect(wrapper.get('[data-testid="nav-storefront-partners"]').attributes('href')).toBe('/storefront/partners')
    expect(wrapper.get('[data-testid="nav-storefront-projects"]').attributes('href')).toBe('/storefront/projects')
    expect(wrapper.get('[data-testid="nav-storefront-faqs"]').classes()).toContain('text-text-accent')
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
      'nav-section-home',
      'nav-section-sales',
      'nav-section-support',
      'nav-section-catalog',
      'nav-section-infrastructure',
      'nav-section-insights',
      'nav-section-system',
    ])
    // The dashboard stands alone under no heading.
    expect(sections.map((section) => section.find('[data-testid$="-label"]').exists() ? section.get('[data-testid$="-label"]').text() : null)).toEqual([
      null,
      'Sales',
      'Support',
      'Catalog',
      'Infrastructure',
      'Insights',
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
    expect(entriesOf(0)).toEqual(['nav-dashboard'])
    expect(entriesOf(1)).toEqual(['nav-clients', 'nav-orders', 'nav-billing', 'nav-domains'])
    expect(entriesOf(2)).toEqual(['nav-support'])
    expect(entriesOf(3)).toEqual(['nav-storefront'])
    expect(entriesOf(4)).toEqual(['nav-servers', 'nav-integrations', 'nav-plugins'])
    expect(entriesOf(5)).toEqual(['nav-reports'])
    expect(entriesOf(6)).toEqual(['nav-settings', 'nav-fraud', 'nav-migration'])
  })

  it('separates the header from the scrolling menu with a hairline and keeps the first entry clear of it', async () => {
    const wrapper = await mountSidebar('/dashboard')

    expect(wrapper.get('[data-testid="sidebar-collapse-toggle"]').element.closest('div[class*="border-b"]')).not.toBeNull()
    expect(wrapper.get('nav').classes()).toEqual(expect.arrayContaining(['pt-3', 'scroll-pt-3']))
  })

  it('gives the brand row the shell header height token, expanded and folded, so it lines up with the top bar', async () => {
    const wrapper = await mountSidebar('/dashboard')
    const header = (): HTMLElement | null => wrapper.get('[data-testid="sidebar-collapse-toggle"]').element.closest<HTMLElement>('div[class*="border-b"]')

    expect(header()?.className).toContain('h-(--shell-header-h)')
    expect(header()?.className).toContain('shrink-0')
    expect(header()?.className).not.toMatch(/p[tb]-/)
    await wrapper.get('[data-testid="sidebar-collapse-toggle"]').trigger('click')
    expect(header()?.className).toContain('h-(--shell-header-h)')
    expect(wrapper.get('nav').classes()).toContain('min-h-0')
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

  it('scrolls the nav only when it overflows: no reserved scrollbar gutter, equal side padding', async () => {
    const wrapper = await mountSidebar('/dashboard')
    const nav = wrapper.get('nav')

    expect(nav.classes()).toEqual(expect.arrayContaining(['overflow-y-auto', 'px-3']))
    expect(nav.classes().some((name) => name.includes('scrollbar-gutter'))).toBe(false)
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
    expect(wrapper.get('[data-testid="nav-clients-users"]').classes()).toContain('text-text-accent')
  })

  it('brings the current page link into view on load and when the route changes', async () => {
    const scrolled: string[] = []
    const original = HTMLElement.prototype.scrollIntoView
    HTMLElement.prototype.scrollIntoView = function scrollIntoView(this: HTMLElement) {
      scrolled.push(this.getAttribute('data-testid') ?? '')
    }
    try {
      const wrapper = await mountSidebar('/dashboard')
      await flushPromises()
      expect(scrolled).toContain('nav-dashboard')

      useNavigationStore().pending = { path: '/clients/users', matched: [] } as unknown as RouteLocationNormalized
      await flushPromises()
      expect(scrolled).toContain('nav-clients-users')
      expect(wrapper.get('[data-testid="nav-clients-users"]').attributes('aria-current')).toBe('page')
    } finally {
      HTMLElement.prototype.scrollIntoView = original
    }
  })

  it('draws an entry at 14px and a sub-menu link at 13px, with a 42px entry and a tight gap between entries', async () => {
    const wrapper = await mountSidebar('/clients/users')

    expect(wrapper.get('[data-testid="nav-servers"]').classes()).toEqual(expect.arrayContaining(['text-sm', 'h-[2.625rem]']))
    expect(wrapper.get('[data-testid="nav-clients-users"]').classes()).toContain('text-[0.8125rem]')
    expect(wrapper.get('[data-testid="nav-section-infrastructure"]').classes()).toContain('gap-0.5')
  })

  it('lights the elbows up to the active sub-menu link and leaves the rest a muted hairline', async () => {
    const wrapper = await mountSidebar('/billing/invoices')

    const above = wrapper.get('[data-testid="nav-billing-transactions"]').classes()
    const active = wrapper.get('[data-testid="nav-billing-invoices"]').classes()
    const below = wrapper.get('[data-testid="nav-billing-quotes"]').classes()

    expect(above).toEqual(expect.arrayContaining(['nav-guide-lit', '[--i:0]', 'before:border-(--nav-guide-from)']))
    expect(active).toEqual(expect.arrayContaining(['nav-guide-lit', '[--i:1]', 'before:border-(--nav-guide-from)']))
    expect(below).toContain('before:border-border-strong')
    expect(below).not.toContain('nav-guide-lit')
  })

  it('draws one muted rail per sub-menu list and a lit overlay down to the active link, and a small rounded elbow per link', async () => {
    const wrapper = await mountSidebar('/billing/invoices')

    const list = wrapper.get('[data-testid="nav-billing-submenu"] .relative').classes()
    const link = wrapper.get('[data-testid="nav-billing-quotes"]').classes()

    expect(list).toEqual(expect.arrayContaining(['[--n:1]', 'before:bg-border-strong', 'after:from-(--nav-guide-start)', 'after:to-(--nav-guide-end)']))
    expect(list).not.toContain('after:hidden')
    expect(link).toEqual(expect.arrayContaining(['h-8', 'before:rounded-bl-lg', 'before:border-b-[1.5px]', 'before:border-l-[1.5px]']))
  })

  it('draws no lit overlay in a sub-menu without an active link', async () => {
    const wrapper = await mountSidebar('/dashboard')

    await wrapper.get('[data-testid="nav-billing"]').trigger('click')

    expect(wrapper.get('[data-testid="nav-billing-submenu"] .relative').classes()).toContain('after:hidden')
  })

  it('emits navigate when a link is clicked', async () => {
    const wrapper = await mountSidebar('/dashboard')

    await wrapper.get('[data-testid="nav-orders"]').trigger('click')
    await flushPromises()

    expect(wrapper.emitted('navigate')).toHaveLength(1)
  })

  it('draws each entry with its own icon, muted and in the accent colour on the current one', async () => {
    const wrapper = await mountSidebar('/dashboard')

    const active = wrapper.get('[data-testid="nav-dashboard"] > svg')
    const idle = wrapper.get('[data-testid="nav-servers"] > svg')
    expect(active.classes()).toContain('text-text-accent')
    expect(idle.classes()).toContain('text-text-muted')
    expect(wrapper.find('[data-testid="nav-clients"] > svg').exists()).toBe(true)
  })

  it('shows the client and open ticket totals as count badges once the statistics are loaded', async () => {
    const wrapper = await mountSidebar('/dashboard')
    expect(wrapper.find('[data-testid="nav-clients-count"]').exists()).toBe(false)

    useDashboardStore().stats = { activeServices: 0, overdueInvoices: 0, openTickets: 7, totalClients: 120 }
    await flushPromises()

    expect(wrapper.get('[data-testid="nav-clients-count"]').text()).toBe('120')
    expect(wrapper.get('[data-testid="nav-support-count"]').text()).toBe('7')
    expect(wrapper.find('[data-testid="nav-orders-count"]').exists()).toBe(false)
  })

  it('shows the signed-in account card at the bottom', async () => {
    const wrapper = await mountSidebar('/dashboard')

    expect(wrapper.find('[data-testid="account-card"]').exists()).toBe(true)
  })

  it('shows the installed version quietly in the footer', async () => {
    stubDocsUrl('')
    stubRoutes({})
    const wrapper = await mountView(AppSidebar, { path: '/dashboard' })
    useVersionStore().version = '1.4.0'
    await flushPromises()

    expect(wrapper.get('[data-testid="sidebar-version"]').text()).toBe('Hostpanel 1.4.0')
  })

  it('shows no version line when the version cannot be loaded', async () => {
    stubDocsUrl('')
    const mock = stubRoutes({ 'GET /api/admin/version': () => problem('No access.', 403) })
    const wrapper = await mountView(AppSidebar, { path: '/dashboard' })
    // The layout starts this load in the app; here the test starts it and waits for the refusal.
    await useVersionStore().ensureLoaded()
    await flushPromises()

    expect(mock.mock.calls.some(([url]) => String(url).endsWith('/admin/version'))).toBe(true)
    expect(useVersionStore().error).toBe('No access.')

    expect(wrapper.find('[data-testid="sidebar-version"]').exists()).toBe(false)
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

  it('offers the theme and language menus inside the menu drawer only, and applies the choice', async () => {
    resetThemeDocument()
    stubSchemeQuery(false)
    stubDocsUrl('')
    stubRoutes({})
    const { wrapper: desktop } = await mountScreen(AppSidebar, {}, '/dashboard')
    expect(desktop.find('[data-testid="sidebar-theme"]').exists()).toBe(false)

    const { wrapper: drawer } = await mountScreen(AppSidebar, { embedded: true }, '/dashboard')
    expect(drawer.find('[data-testid="sidebar-locale"]').exists()).toBe(true)
    expect(desktop.find('[data-testid="sidebar-locale"]').exists()).toBe(false)
    await drawer.get('[data-testid="sidebar-theme"]').trigger('click')
    expect(drawer.get('[data-testid="theme-option-system"]').attributes('aria-checked')).toBe('true')
    await drawer.get('[data-testid="theme-option-light"]').trigger('click')

    expect(document.documentElement.classList.contains('light')).toBe(true)
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(AdminTheme.Light)
  })
})

describe('AppSidebar icon rail', () => {
  beforeEach(() => {
    window.localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('folds to an icon rail from the header button and unfolds again', async () => {
    const wrapper = await mountSidebar('/dashboard')
    expect(wrapper.get('[data-testid="sidebar-collapse-toggle"]').attributes('aria-label')).toBe('Collapse sidebar')

    await wrapper.get('[data-testid="sidebar-collapse-toggle"]').trigger('click')

    const aside = wrapper.get('[data-testid="app-sidebar"]')
    expect(aside.classes()).toContain('w-18')
    expect(wrapper.find('[data-testid="nav-section-sales-label"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="sidebar-brand-mark"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="sidebar-brand-name"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="nav-dashboard"]').attributes('aria-label')).toBe('Dashboard')
    expect(wrapper.get('[data-testid="nav-dashboard"]').text()).toBe('')
    expect(wrapper.get('[data-testid="sidebar-collapse-toggle"]').attributes('aria-label')).toBe('Expand sidebar')

    await wrapper.get('[data-testid="sidebar-collapse-toggle"]').trigger('click')

    expect(aside.classes()).toContain('w-62')
    expect(wrapper.find('[data-testid="nav-section-sales-label"]').exists()).toBe(true)
  })

  it('opens the panel when an entry with a sub-menu is clicked in the rail', async () => {
    const wrapper = await mountSidebar('/dashboard')
    await wrapper.get('[data-testid="sidebar-collapse-toggle"]').trigger('click')
    expect(wrapper.find('[data-testid="nav-billing-submenu"]').exists()).toBe(false)

    await wrapper.get('[data-testid="nav-billing"]').trigger('click')

    expect(wrapper.get('[data-testid="app-sidebar"]').classes()).toContain('w-62')
    expect(wrapper.get('[data-testid="nav-billing"]').attributes('aria-expanded')).toBe('true')
  })

  it('draws the phone drawer in full even when the desktop panel is folded, without the fold button', async () => {
    window.localStorage.setItem(SIDEBAR_STORAGE_KEY, '1')
    stubRoutes({})
    const { wrapper } = await mountScreen(AppSidebar, { embedded: true }, '/dashboard')

    expect(wrapper.find('[data-testid="nav-section-sales-label"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="sidebar-collapse-toggle"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="app-sidebar"]').classes()).toContain('pb-[env(safe-area-inset-bottom)]')
  })
})

describe('AppSidebar Add-ons', () => {
  beforeEach(() => {
    window.localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('draws an Add-ons section before System from the menu items plugins declare', async () => {
    stubDocsUrl('')
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
    expect(sections).toEqual(['nav-section-home', 'nav-section-sales', 'nav-section-support', 'nav-section-catalog', 'nav-section-infrastructure', 'nav-section-insights', 'nav-section-addons', 'nav-section-system'])
    expect(wrapper.get('[data-testid="nav-section-addons-label"]').text()).toBe('Add-ons')
    const sites = wrapper.get('[data-testid="nav-apps-sites"]')
    expect(sites.text()).toBe('Sites')
    expect(sites.attributes('href')).toBe('/apps/sites')
    const usage = wrapper.get('[data-testid="nav-apps-sites-usage"]')
    expect(usage.attributes('href')).toBe('/apps/sites/usage')
    expect(usage.classes()).toContain('bg-(--nav-active-bg)')
    expect(sites.classes()).not.toContain('bg-(--nav-active-bg)')
  })

  it('draws no Add-ons section while no plugin declares a menu item', async () => {
    stubDocsUrl('')
    stubRoutes({ 'GET /api/admin/plugins/ui': () => json({ menus: [], pages: [] }) })
    const wrapper = await mountView(AppSidebar, { path: '/dashboard' })

    expect(wrapper.find('[data-testid="nav-section-addons"]').exists()).toBe(false)
  })
})
