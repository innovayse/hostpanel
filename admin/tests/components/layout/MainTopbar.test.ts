/**
 * Mount tests for MainTopbar: page title from the route, the notification badge and drawer, the
 * language select, the theme select, the hamburger event, and the primary action.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import MainTopbar from '@/components/layout/MainTopbar.vue'
import { useCommandPaletteStore } from '@/stores/commandPalette'
import { usePageTitleStore } from '@/stores/pageTitle'
import { stubRoutes } from '../../helpers/routes'
import { json } from '../../helpers/fetch'
import { mountView } from '../../helpers/mount'
import { stubPhone } from '../../helpers/phone'
import { resetThemeDocument, stubSchemeQuery } from '../../helpers/theme'
import { THEME_STORAGE_KEY } from '@/lib/themeBoot'
import { AdminTheme } from '@/types/admintheme'

/** Two events of the notification feed. */
const events = [
  { type: 'ClientRegistered', message: 'New client Ada', occurredAt: '2026-10-14T08:00:00Z', entityId: 7 },
  { type: 'DomainExpiring', message: 'example.com expires', occurredAt: '2026-10-14T09:00:00Z', entityId: 3 },
]

/**
 * Mounts the bar at a path with a scripted feed.
 *
 * @param path - The current route.
 * @param feed - Events the feed endpoint returns.
 * @returns The wrapper.
 */
const mountBar = async (path: string, feed: unknown[] = events) => {
  stubRoutes({ 'GET /api/admin/dashboard/activity': () => json(feed) })
  return mountView(MainTopbar, { path })
}

describe('MainTopbar', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('takes its height from the shell header token the sidebar brand row shares', async () => {
    const wrapper = await mountBar('/dashboard')

    expect(wrapper.get('[data-testid="topbar"]').classes()).toContain('h-[calc(var(--shell-header-h)+env(safe-area-inset-top))]')
  })

  it('titles the bar after the current section', async () => {
    const wrapper = await mountBar('/servers')

    expect(wrapper.get('[data-testid="topbar-title"]').text()).toBe('Servers')
  })

  it('shows the number of notifications and lists them in the drawer', async () => {
    const wrapper = await mountBar('/dashboard')

    expect(wrapper.get('[data-testid="topbar-notification-count"]').text()).toBe('2')
    await wrapper.get('[data-testid="topbar-notifications"]').trigger('click')

    expect(wrapper.get('[data-testid="notifications-drawer"]').text()).toContain('New client Ada')
    expect(wrapper.get('[data-testid="notifications-drawer"]').text()).toContain('example.com expires')
  })

  it('lists the notifications in a side panel on a wide screen and in a bottom sheet on a phone', async () => {
    stubPhone(false)
    const wide = await mountBar('/dashboard')
    await wide.get('[data-testid="topbar-notifications"]').trigger('click')
    expect(wide.get('aside[role="dialog"]').classes()).toContain('right-0')
    expect(wide.find('[data-testid="drawer-grip"]').exists()).toBe(false)
    wide.unmount()

    stubPhone(true)
    const narrow = await mountBar('/dashboard')
    await narrow.get('[data-testid="topbar-notifications"]').trigger('click')
    expect(narrow.get('aside[role="dialog"]').classes()).toContain('rounded-t-panel')
    expect(narrow.find('[data-testid="drawer-grip"]').exists()).toBe(true)
  })

  it('hangs the count off the bell corner, names the button with the count and caps it at 9+', async () => {
    const many = Array.from({ length: 12 }, (_, i) => ({ type: 'ClientRegistered', message: `Client ${i}`, occurredAt: '2026-10-14T08:00:00Z', entityId: i }))
    const wrapper = await mountBar('/dashboard', many)

    const badge = wrapper.get('[data-testid="topbar-notification-count"]')
    expect(badge.text()).toBe('9+')
    const corner = badge.element.parentElement
    expect(corner?.classList).toContain('absolute')
    expect(corner?.classList).toContain('-right-1')
    expect(corner?.classList).toContain('-top-1')
    expect(corner?.parentElement?.getAttribute('data-testid')).toBe('topbar-notifications')
    expect(corner?.parentElement?.classList.contains('relative')).toBe(true)
    expect(wrapper.get('[data-testid="topbar-notifications"]').attributes('aria-label')).toBe('Notifications, 12 new')
  })

  it('draws a dot instead of the number on a phone, for as long as there is something new', async () => {
    const wrapper = await mountBar('/dashboard')

    const dot = wrapper.get('[data-testid="topbar-notification-dot"]')
    expect(dot.element.parentElement?.getAttribute('aria-hidden')).toBe('true')
    expect(dot.text()).toBe('')
    expect(dot.classes()).toEqual(expect.arrayContaining(['rounded-full', 'bg-primary-500']))
    expect(dot.classes()).toContain('sm:hidden')
    const count = wrapper.get('[data-testid="topbar-notification-count"]')
    expect(count.classes()).toEqual(expect.arrayContaining(['rounded-pill', 'ring-2']))
    expect(count.classes()).toContain('max-sm:hidden')
  })

  it('shows the empty message for an empty feed and hides the badge', async () => {
    const wrapper = await mountBar('/dashboard', [])

    expect(wrapper.find('[data-testid="topbar-notification-count"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="topbar-notification-dot"]').exists()).toBe(false)
    await wrapper.get('[data-testid="topbar-notifications"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="notifications-drawer"]').text()).toContain('No notifications')
  })

  it('has the menu button only for the phone drawer: named for opening, hidden from lg up', async () => {
    const wrapper = await mountBar('/dashboard')
    const button = wrapper.get('[data-testid="topbar-toggle-sidebar"]')

    expect(button.attributes('aria-label')).toBe('Open menu')
    expect(button.classes()).toContain('lg:hidden')
  })

  it('emits toggle-sidebar from the hamburger', async () => {
    const wrapper = await mountBar('/dashboard')

    await wrapper.get('[data-testid="topbar-toggle-sidebar"]').trigger('click')

    expect(wrapper.emitted('toggle-sidebar')).toHaveLength(1)
  })

  it('offers the language menu and leaves the account to the sidebar', async () => {
    const wrapper = await mountBar('/dashboard')

    expect(wrapper.find('[data-testid="topbar-locale"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="topbar-user-menu"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="topbar-avatar"]').exists()).toBe(false)
  })

  it('offers the gradient primary action that opens the new client form', async () => {
    const wrapper = await mountBar('/dashboard')
    const action = wrapper.get('[data-testid="topbar-primary-action"]')

    expect(action.attributes('href')).toBe('/clients/add')
    expect(action.text()).toBe('Create client')
  })

  it('opens the command palette from the search trigger, which announces the shortcut', async () => {
    const wrapper = await mountBar('/dashboard')
    const trigger = wrapper.get('[data-testid="topbar-search"]')

    expect(trigger.attributes('aria-label')).toBe('Search the panel')
    // A narrow bar (a phone, the tablet rail layout) shows a bare icon like the bell, theme and language triggers; a bar of 4xl or more the wide field.
    expect(trigger.classes()).toEqual(expect.arrayContaining(['rounded-control', 'bg-transparent', 'border-transparent', 'w-10.5']))
    expect(trigger.classes()).toEqual(expect.arrayContaining(['@4xl:bg-surface-field', '@4xl:max-w-[460px]', '@4xl:flex-1']))
    expect(trigger.text()).toContain('Search pages and actions')
    // The shortcut chip is the first thing to go as the bar narrows: it needs 5xl, the wide field only 4xl.
    expect(wrapper.get('[data-testid="topbar-search-shortcut"]').text()).toBe('Ctrl K')
    expect(wrapper.get('[data-testid="topbar-search-shortcut"]').classes()).toEqual(expect.arrayContaining(['hidden', '@5xl:block']))
    expect(wrapper.get('[data-testid="topbar"]').classes()).toContain('@container')
    expect(wrapper.get('[data-testid="topbar"]').element.tagName).toBe('HEADER')
    expect(useCommandPaletteStore().isOpen).toBe(false)

    await trigger.trigger('click')

    expect(useCommandPaletteStore().isOpen).toBe(true)
  })

  it('blurs the bar, hairlines it and keeps clear of the top safe area', async () => {
    const wrapper = await mountBar('/dashboard')

    expect(wrapper.get('[data-testid="topbar"]').classes()).toEqual(expect.arrayContaining(['bg-surface-topbar', 'border-b', 'backdrop-blur-[14px]', 'pt-[env(safe-area-inset-top)]']))
  })

  it('keeps the theme toggle out of a phone top bar', async () => {
    const wrapper = await mountBar('/dashboard')

    // The menu sits in a stack that is hidden below the sm breakpoint.
    const holder = wrapper.get('[data-testid="topbar-theme"]').element.closest('.hidden')
    expect(holder?.classList).toContain('sm:flex')
  })

  it('switches the theme with one click on the theme toggle and remembers it', async () => {
    resetThemeDocument()
    stubSchemeQuery(false)
    const wrapper = await mountBar('/dashboard')

    expect(wrapper.get('[data-testid="topbar-theme"]').attributes('aria-pressed')).toBe('true')
    await wrapper.get('[data-testid="topbar-theme"]').trigger('click')

    expect(document.documentElement.classList.contains('light')).toBe(true)
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(AdminTheme.Light)
    expect(wrapper.get('[data-testid="topbar-theme"]').attributes('aria-pressed')).toBe('false')

    await wrapper.get('[data-testid="topbar-theme"]').trigger('click')
    expect(document.documentElement.classList.contains('light')).toBe(false)
  })

  it('draws bars in the rows of the drawer while the feed loads', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const wrapper = await mountView(MainTopbar, { path: '/dashboard' })

    await wrapper.get('[data-testid="topbar-notifications"]').trigger('click')

    const drawer = wrapper.get('[data-testid="notifications-drawer"]')
    expect(drawer.findAll('[data-testid^="notification-"]')).toHaveLength(3)
    expect(drawer.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(6)
    expect(drawer.text()).not.toContain('No notifications')
  })

  it('draws the trail from the section down to the page in place of the plain section title', async () => {
    stubRoutes({ 'GET /api/admin/dashboard/activity': () => json(events) })
    const wrapper = await mountView(MainTopbar, { path: '/settings/slides/create', pattern: '/settings/slides/create' })

    const bar = wrapper.get('[data-testid="topbar"]')
    expect(bar.get('[data-testid="ui-breadcrumb"]').text()).toContain('Settings')
    expect(bar.get('[data-testid="ui-breadcrumb"]').text()).toContain('Slides')
    expect(bar.get('[data-testid="topbar-title"]').text()).toBe('New')
    expect(bar.get('[data-testid="topbar-title"]').attributes('aria-current')).toBe('page')
  })

  it('names the page by its own title on a phone, not by the generic last step of the trail', async () => {
    stubPhone(true)
    const wrapper = await mountView(MainTopbar, { path: '/clients/7', pattern: '/clients/:id' })
    usePageTitleStore().announce(Symbol('header'), 'Ada Lovelace')
    await flushPromises()

    expect(wrapper.get('[data-testid="topbar-title"]').text()).toBe('Ada Lovelace')
  })

  it('keeps the trail on a phone until a header shows a title', async () => {
    stubPhone(true)
    const wrapper = await mountView(MainTopbar, { path: '/orders/8', pattern: '/orders/:id' })

    expect(wrapper.get('[data-testid="topbar-title"]').text()).toBe('Details')
  })

  it('keeps the trail from a tablet up, whatever title a header shows', async () => {
    stubPhone(false)
    const wrapper = await mountView(MainTopbar, { path: '/orders/8', pattern: '/orders/:id' })
    usePageTitleStore().announce(Symbol('header'), 'Order 8')
    await flushPromises()

    expect(wrapper.get('[data-testid="topbar-title"]').text()).toBe('Details')
  })
})
