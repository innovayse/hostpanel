/**
 * Mount tests for AppTopbar: page title from the route, the notification badge and drawer, the
 * language select, the theme select, the hamburger event, and the account actions.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import AppTopbar from '@/components/layout/AppTopbar.vue'
import { stubRoutes } from '../../helpers/routes'
import { json } from '../../helpers/fetch'
import { mountView } from '../../helpers/mount'
import { pickOption, selectText } from '../../helpers/select'
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
  return mountView(AppTopbar, { path })
}

describe('AppTopbar', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('titles the bar after the current section', async () => {
    const wrapper = await mountBar('/servers/anything')

    expect(wrapper.get('[data-testid="topbar-title"]').text()).toBe('Servers')
  })

  it('shows the number of notifications and lists them in the drawer', async () => {
    const wrapper = await mountBar('/dashboard')

    expect(wrapper.get('[data-testid="topbar-notification-count"]').text()).toBe('2')
    await wrapper.get('[data-testid="topbar-notifications"]').trigger('click')

    expect(wrapper.get('[data-testid="notifications-drawer"]').text()).toContain('New client Ada')
    expect(wrapper.get('[data-testid="notifications-drawer"]').text()).toContain('example.com expires')
  })

  it('hangs the count off the bell corner, names the button with the count and caps it at 9+', async () => {
    const many = Array.from({ length: 12 }, (_, i) => ({ type: 'ClientRegistered', message: `Client ${i}`, occurredAt: '2026-10-14T08:00:00Z', entityId: i }))
    const wrapper = await mountBar('/dashboard', many)

    const badge = wrapper.get('[data-testid="topbar-notification-count"]')
    expect(badge.text()).toBe('9+')
    expect(badge.classes()).toEqual(expect.arrayContaining(['absolute', '-right-2', '-top-2']))
    expect(badge.element.parentElement?.classList.contains('relative')).toBe(true)
    expect(wrapper.get('[data-testid="topbar-notifications"]').attributes('aria-label')).toBe('Notifications, 12 new')
  })

  it('draws a dot instead of the number on a phone, for as long as there is something new', async () => {
    const wrapper = await mountBar('/dashboard')

    const dot = wrapper.get('[data-testid="topbar-notification-dot"]')
    expect(dot.attributes('aria-hidden')).toBe('true')
    expect(dot.classes()).toEqual(expect.arrayContaining(['absolute', 'rounded-full', 'sm:hidden']))
    expect(wrapper.get('[data-testid="topbar-notification-count"]').classes()).toContain('max-sm:hidden')
  })

  it('shows the empty message for an empty feed and hides the badge', async () => {
    const wrapper = await mountBar('/dashboard', [])

    expect(wrapper.find('[data-testid="topbar-notification-count"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="topbar-notification-dot"]').exists()).toBe(false)
    await wrapper.get('[data-testid="topbar-notifications"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="notifications-drawer"]').text()).toContain('No notifications')
  })

  it('emits toggle-sidebar from the hamburger', async () => {
    const wrapper = await mountBar('/dashboard')

    await wrapper.get('[data-testid="topbar-toggle-sidebar"]').trigger('click')

    expect(wrapper.emitted('toggle-sidebar')).toHaveLength(1)
  })

  it('offers the language select, settings and sign-out', async () => {
    const wrapper = await mountBar('/dashboard')

    expect(wrapper.find('[data-testid="topbar-locale"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="topbar-settings"]').attributes('href')).toBe('/settings')
    expect(wrapper.find('[data-testid="topbar-sign-out"]').exists()).toBe(true)
  })

  it('switches the theme from the theme select and remembers it', async () => {
    resetThemeDocument()
    stubSchemeQuery(false)
    const wrapper = await mountBar('/dashboard')

    expect(selectText(wrapper, 'topbar-theme')).toBe('System')
    await pickOption(wrapper, 'topbar-theme', 'Light')

    expect(selectText(wrapper, 'topbar-theme')).toBe('Light')
    expect(document.documentElement.classList.contains('light')).toBe(true)
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(AdminTheme.Light)

    await pickOption(wrapper, 'topbar-theme', 'Dark')
    expect(document.documentElement.classList.contains('light')).toBe(false)
  })
})
