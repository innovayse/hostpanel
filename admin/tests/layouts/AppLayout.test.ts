/**
 * Mount tests for AppLayout: the shell holds the sidebar, the topbar and the routed content, the
 * hamburger opens the navigation drawer, and a screen still downloading is answered in the content
 * area while the shell stays.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { RouteLocationNormalized } from 'vue-router'
import AppLayout from '@/layouts/AppLayout.vue'
import { useAuthStore } from '@/stores/auth'
import { useNavigationStore } from '@/stores/navigation'
import { useReleaseStatusStore } from '@/stores/releaseStatus'
import { stubRoutes } from '../helpers/routes'
import { json, problem } from '../helpers/fetch'
import { mountView } from '../helpers/mount'

describe('AppLayout', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('renders the desktop sidebar, the topbar and the content area', async () => {
    stubRoutes({ 'GET /api/admin/dashboard/activity': () => json([]) })
    const wrapper = await mountView(AppLayout, { path: '/dashboard' })

    expect(wrapper.find('[data-testid="app-layout"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="app-sidebar-desktop"] [data-testid="app-sidebar"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="app-topbar"]').exists()).toBe(true)
  })

  it('opens the navigation drawer from the hamburger', async () => {
    stubRoutes({ 'GET /api/admin/dashboard/activity': () => json([]) })
    const wrapper = await mountView(AppLayout, { path: '/dashboard' })
    expect(wrapper.findAll('[data-testid="app-sidebar"]')).toHaveLength(1)

    await wrapper.get('[data-testid="topbar-toggle-sidebar"]').trigger('click')
    await flushPromises()

    expect(wrapper.findAll('[data-testid="app-sidebar"]')).toHaveLength(2)
  })

  it('shows the skeleton in the content area while the next screen downloads, and keeps the shell', async () => {
    stubRoutes({ 'GET /api/admin/dashboard/activity': () => json([]) })
    const wrapper = await mountView(AppLayout, { path: '/dashboard' })
    expect(wrapper.find('[data-testid="view-loading"]').exists()).toBe(false)

    // A navigation the guards admitted, heading for a screen drawn by another record than the current one.
    useNavigationStore().pending = { path: '/clients', matched: [{ components: { default: {} } }] } as unknown as RouteLocationNormalized
    await flushPromises()

    expect(wrapper.find('main [data-testid="view-loading"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="app-sidebar-desktop"] [data-testid="app-sidebar"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="app-topbar"]').exists()).toBe(true)
  })
})

/**
 * Counts the requests a fetch mock received for a path.
 *
 * @param mock - The stubbed `fetch`.
 * @param path - Fragment the request URL must contain.
 * @returns How many requests matched.
 */
const callsTo = (mock: ReturnType<typeof vi.fn>, path: string): number =>
  mock.mock.calls.filter((call) => String(call[0]).includes(path)).length

describe('AppLayout version loading', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('loads the version once for a non-admin staff user, in every sidebar, and not the release status', async () => {
    const mock = stubRoutes({
      'GET /api/admin/dashboard/activity': () => json([]),
      'GET /api/admin/version': () => json({ installedVersion: '1.4.0' }),
    })
    const wrapper = await mountView(AppLayout, { path: '/dashboard' })
    useAuthStore().user = { email: 'staff@example.com', roles: ['Reseller'] }
    await flushPromises()
    await wrapper.get('[data-testid="topbar-toggle-sidebar"]').trigger('click')
    await flushPromises()

    expect(wrapper.findAll('[data-testid="sidebar-version"]')).toHaveLength(2)
    expect(wrapper.get('[data-testid="sidebar-version"]').text()).toBe('Hostpanel 1.4.0')
    expect(callsTo(mock, '/admin/version')).toBe(1)
    expect(callsTo(mock, '/admin/release-status')).toBe(0)
  })

  it('also loads the release status for an admin', async () => {
    const mock = stubRoutes({
      'GET /api/admin/dashboard/activity': () => json([]),
      'GET /api/admin/version': () => json({ installedVersion: '1.4.0' }),
      'GET /api/admin/release-status': () => json({ installedVersion: '1.4.0', latestVersion: '2.0.0', isUpgradeAvailable: true }),
    })
    const wrapper = await mountView(AppLayout, { path: '/dashboard' })
    useAuthStore().user = { email: 'admin@example.com', roles: ['Admin'] }
    await flushPromises()

    expect(callsTo(mock, '/admin/release-status')).toBe(1)
    expect(useReleaseStatusStore().status?.isUpgradeAvailable).toBe(true)
    expect(wrapper.find('[data-testid="release-update-notice"]').exists()).toBe(true)
  })

  it('shows no version after a refused load and shows it once the roles change', async () => {
    let refuse = true
    stubRoutes({
      'GET /api/admin/dashboard/activity': () => json([]),
      'GET /api/admin/version': () => (refuse ? problem('No access.', 403) : json({ installedVersion: '1.4.0' })),
    })
    const wrapper = await mountView(AppLayout, { path: '/dashboard' })
    useAuthStore().user = { email: 'staff@example.com', roles: ['Reseller'] }
    await flushPromises()

    expect(wrapper.find('[data-testid="sidebar-version"]').exists()).toBe(false)

    refuse = false
    useAuthStore().user = { email: 'staff@example.com', roles: ['Reseller', 'Admin'] }
    await flushPromises()

    expect(wrapper.get('[data-testid="sidebar-version"]').text()).toBe('Hostpanel 1.4.0')
  })

  it('drops the version on sign-out', async () => {
    stubRoutes({
      'GET /api/admin/dashboard/activity': () => json([]),
      'GET /api/admin/version': () => json({ installedVersion: '1.4.0' }),
    })
    const wrapper = await mountView(AppLayout, { path: '/dashboard' })
    useAuthStore().user = { email: 'staff@example.com', roles: ['Reseller'] }
    await flushPromises()
    expect(wrapper.find('[data-testid="sidebar-version"]').exists()).toBe(true)

    useAuthStore().user = null
    await flushPromises()

    expect(wrapper.find('[data-testid="sidebar-version"]').exists()).toBe(false)
  })
})
