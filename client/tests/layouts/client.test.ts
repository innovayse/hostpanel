/**
 * Unit tests for `layouts/client.vue` against the real auth and client stores: the
 * permission-filtered navigation, the mobile drawer, the identity fetch, the platform widget
 * hand-over, signing out and the links plugins add. Nothing of our own is replaced: the identity, the
 * sign-out and the plugin index go through the real stores, API composables and transport to a stub
 * server playing the BFF, and the tests read what it received.
 *
 * @module tests/layouts/client.test
 */

import { createServer } from 'node:http'
import type { AddressInfo } from 'node:net'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ofetch } from 'ofetch'
import { enableAutoUnmount } from '@vue/test-utils'
import { nextTick, ref } from 'vue'
import { storeToRefs } from 'pinia'
import { mountClient } from '~/tests/helpers/mountClient'
import { stubAuthRuntime } from '~/tests/helpers/authScreens'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { startStubServer } from '~/tests/helpers/stubServer'
import type { StubReply, StubServer } from '~/tests/helpers/stubServer'
import { settle } from '~/tests/helpers/settle'

/** The BFF path of the signed-in person's identity. */
const ME_URL = '/api/portal/client/me'

/** The BFF path of the local sign-out. */
const LOGOUT_URL = '/api/portal/auth/logout'

/** The BFF path of the plugin index. */
const PLUGIN_INDEX_URL = '/api/portal/client/plugins/ui'

const { default: ClientLayout } = await import('~/layouts/client.vue')
const { default: LayoutDemoBanner } = await import('~/components/layout/DemoBanner.vue')
const { Permission } = await import('~/types/permission')
const { useAuthStore } = await import('~/stores/auth')
const { useClientStore } = await import('~/stores/client')
const { usePluginsStore } = await import('~/stores/plugins')
const { default: ClientLayoutSidebar } = await import('~/components/client/layout/Sidebar.vue')

/** The theme and language controls need runtime services this test does not provide. */
const STUBS = { UiThemeToggle: true, UiLanguageSwitcher: true, LogoLockup: true, UiCookieBanner: { template: '<div data-testid="cookie-banner" />' } }

/** The signed-in person the identity call answers with. */
const ME = makeClientUser({ email: 'ada@example.com', permissions: Permission.All })

// Unmounting releases the drawer's scroll lock and key listener between tests.
enableAutoUnmount(afterEach)

afterEach(async () => {
  await bff.close()
})

/** Runtime configuration of a deployment that is served the platform shell's widget. */
const SHELL_CONFIG = { authMode: 'sso', mainUrl: 'https://main.example.test' }

/** The stub playing the BFF's plugin index; empty unless a test gives it plugins. */
let bff: StubServer

/** What the stub answers for the plugin index in the current test. */
let pluginIndex: () => StubReply = () => ({ body: { menus: [], pages: [] } })

beforeEach(async () => {
  pluginIndex = () => ({ body: { menus: [], pages: [] } })
  bff = await startStubServer((request) => {
    if (request.url === PLUGIN_INDEX_URL) {
      return pluginIndex()
    }
    if (request.method === 'GET' && request.url === ME_URL) {
      return { body: ME }
    }
    if (request.method === 'POST' && request.url === LOGOUT_URL) {
      return { body: {} }
    }
    return { status: 404, body: {} }
  })
  vi.stubGlobal('$fetch', ofetch.create({ baseURL: bff.baseUrl }))

  stubAuthRuntime({ publicConfig: SHELL_CONFIG })
  vi.stubGlobal('useAuthStore', useAuthStore)
  vi.stubGlobal('useClientStore', useClientStore)
  vi.stubGlobal('storeToRefs', storeToRefs)
})

/**
 * How many requests the stub received for one endpoint.
 *
 * @param method - The HTTP method.
 * @param url - The path, query included.
 * @returns The count.
 */
const callsTo = (method: string, url: string): number =>
  bff.requests.filter(request => request.method === method && request.url === url).length

/**
 * Mounts the layout with a page in its slot and waits for the identity fetch.
 *
 * @returns The wrapper.
 */
const mountLayout = async () => {
  const wrapper = mountClient(ClientLayout, { slots: { default: 'page content' }, stubs: STUBS, components: { LayoutDemoBanner } })
  await settle()
  return wrapper
}

describe('client layout', () => {
  describe('workspace page', () => {
    it.each(['ru', 'hy'])('hides the side menu on a workspace page under the %s prefix', async (language) => {
      const wrapper = await mountAt(`/${language}${PAGE_PATH}`, 'workspace')

      expect(wrapper.get('[data-testid="client-sidebar"]').classes()).toContain('lg:hidden')
    })

    /** The address of the workspace page these tests hold in the store. */
    const PAGE_PATH = '/client/apps/acme/sites/7'

    /**
     * Mounts the layout at an address with a plugin page in the store.
     *
     * @param path - The address on screen.
     * @param layout - The page's layout.
     * @returns The wrapper.
     */
    const mountAt = async (path: string, layout: string) => {
      vi.stubGlobal('useRoute', () => ({ params: path.includes('/client/apps/acme/') ? { plugin: 'acme' } : {}, query: {}, path, fullPath: path, meta: {} }))
      const wrapper = await mountLayout()
      usePluginsStore().page = {
        pluginId: 'acme', id: 'sites', path: '/sites/{id}', title: 'Sites', view: { schema: 1, blocks: [] },
        externalOrigins: [], layout, frameOrigins: [],
      } as never
      await nextTick()
      return wrapper
    }

    it('drops the gutter and hides the side menu on a wide screen', async () => {
      const wrapper = await mountAt(PAGE_PATH, 'workspace')

      expect(wrapper.get('main > div').classes()).not.toContain('p-6')
      expect(wrapper.get('main > div').classes()).toContain('flex-1')
      expect(wrapper.findComponent(ClientLayoutSidebar).classes()).toContain('lg:hidden')
    })

    it('keeps the usual frame for a standard page', async () => {
      const wrapper = await mountAt(PAGE_PATH, 'standard')

      expect(wrapper.get('main > div').classes()).toContain('p-6')
      expect(wrapper.findComponent(ClientLayoutSidebar).classes()).not.toContain('lg:hidden')
    })

    it('keeps the usual frame once the reader left the page the store still holds', async () => {
      const wrapper = await mountAt('/client/dashboard', 'workspace')

      expect(wrapper.get('main > div').classes()).toContain('p-6')
      expect(wrapper.findComponent(ClientLayoutSidebar).classes()).not.toContain('lg:hidden')
    })
  })

  it('mounts the cookie banner inside the client area', async () => {
    const wrapper = await mountLayout()

    expect(wrapper.find('[data-testid="cookie-banner"]').exists()).toBe(true)
  })

  it('renders the page in its slot', async () => {
    const wrapper = await mountLayout()

    expect(wrapper.text()).toContain('page content')
  })

  it('sets its own text colour for both themes instead of inheriting the body colour', async () => {
    const wrapper = await mountLayout()

    // The body keeps the dark page colour and white text outside the aurora template, so a
    // child that inherits (the sidebar brand) was white on the white light-mode sidebar.
    const classes = wrapper.find('[data-testid="client-layout"]').classes()
    expect(classes).toContain('text-gray-900')
    expect(classes).toContain('dark:text-white')
  })

  it('fetches the identity once and greets the person by name', async () => {
    const wrapper = await mountLayout()

    expect(callsTo('GET', ME_URL)).toBe(1)
    expect(wrapper.find('[data-testid="client-topbar-name"]').text()).toBe('Ada Lovelace')
  })

  it('hands the person to the platform widget mounts', async () => {
    const wrapper = await mountLayout()

    const user = JSON.parse(wrapper.find('#inno-account-mount').attributes('data-user') ?? '{}')
    expect(user).toEqual({ email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' })
  })

  it('shows every link to a person with all permissions, without the auth store ever loading its user', async () => {
    const wrapper = await mountLayout()

    expect(useAuthStore().user).toBeNull()

    for (const id of ['dashboard', 'services', 'domains', 'invoices', 'credit-notes', 'tickets', 'account']) {
      expect(wrapper.find(`[data-testid="nav-client-${id}"]`).exists()).toBe(true)
    }
    expect(wrapper.find('[data-testid="nav-announcements"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="nav-knowledgebase"]').exists()).toBe(true)
  })

  it('shows every link while the identity has not loaded yet, then narrows them to the permissions', async () => {
    bff.respondWith((request) => {
      if (request.method === 'GET' && request.url === ME_URL) {
        return { hold: true }
      }
      return { body: { menus: [], pages: [] } }
    })
    const wrapper = await mountLayout()

    expect(useClientStore().user).toBeNull()
    for (const id of ['dashboard', 'services', 'domains', 'invoices', 'credit-notes', 'tickets', 'account']) {
      expect(wrapper.find(`[data-testid="nav-client-${id}"]`).exists()).toBe(true)
    }

    useClientStore().user = { ...ME, permissions: Permission.ViewDomains } as never
    await nextTick()

    expect(wrapper.find('[data-testid="nav-client-services"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="nav-client-domains"]').exists()).toBe(true)
  })

  it('hides the sections a person has no permission for', async () => {
    const wrapper = await mountLayout()
    useClientStore().user = { ...ME, permissions: Permission.ViewDomains } as never
    await nextTick()

    expect(wrapper.find('[data-testid="nav-client-domains"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="nav-client-services"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="nav-client-invoices"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="nav-client-dashboard"]').exists()).toBe(true)
  })

  it('keeps every link for a person who acts for no account yet, by the same rule as the actions', async () => {
    const wrapper = await mountLayout()
    useClientStore().user = { ...ME, permissions: Permission.None, actsForAccount: false } as never
    await nextTick()

    expect(wrapper.find('[data-testid="nav-client-services"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="nav-client-invoices"]').exists()).toBe(true)
  })

  it('reads the plugin index once, with the identity', async () => {
    await mountLayout()

    expect(bff.requests.filter(request => request.url === '/api/portal/client/plugins/ui')).toHaveLength(1)
  })

  it('reads the plugin index again when the reader switches language, so the plugin labels follow', async () => {
    /** The reader language; switched to prove the plugin index is read again. */
    const locale = ref('en')
    vi.stubGlobal('useI18n', () => ({ t: (key: string) => key, locale }))
    await mountLayout()
    expect(callsTo('GET', PLUGIN_INDEX_URL)).toBe(1)

    locale.value = 'ru'
    await settle()

    expect(callsTo('GET', PLUGIN_INDEX_URL)).toBe(2)
  })

  it('lists the plugin pages the API offers this person before the account link, labelled as the API sent them', async () => {
    pluginIndex = () => ({
      body: {
        menus: [
          { pluginId: 'acme-sites', id: 'sites', label: 'Իմ կայքերը', page: 'sites', icon: 'globe', order: 2 },
          { pluginId: 'acme-mail', id: 'inbox', label: 'Mail', page: 'inbox', icon: null, order: 1 },
        ],
        pages: [
          { pluginId: 'acme-sites', id: 'sites', path: '/sites', title: 'Իմ կայքերը' },
          { pluginId: 'acme-mail', id: 'inbox', path: '/inbox', title: 'Mail' },
        ],
      },
    })
    const wrapper = await mountLayout()

    const links = wrapper.findAll('[data-testid="client-sidebar"] a').map(link => link.attributes('data-testid'))
    const account = links.indexOf('nav-client-account')
    expect(links.slice(account - 2, account)).toEqual(['nav-client-apps-acme-mail-inbox', 'nav-client-apps-acme-sites-sites'])
    expect(wrapper.get('[data-testid="nav-client-apps-acme-sites-sites"]').text()).toBe('Իմ կայքերը')
    expect(wrapper.get('[data-testid="nav-client-apps-acme-sites-sites"]').attributes('href')).toBe('/client/apps/acme-sites/sites')
  })

  it('links no page that needs values the menu cannot give', async () => {
    pluginIndex = () => ({
      body: {
        menus: [{ pluginId: 'acme-sites', id: 'site', label: 'One site', page: 'site', icon: null, order: 1 }],
        pages: [{ pluginId: 'acme-sites', id: 'site', path: '/sites/{siteId}', title: 'One site' }],
      },
    })
    const wrapper = await mountLayout()

    expect(wrapper.find('[data-testid^="nav-client-apps-"]').exists()).toBe(false)
  })

  it('renders a plugin label as text, never as markup', async () => {
    pluginIndex = () => ({
      body: {
        menus: [{ pluginId: 'acme-sites', id: 'sites', label: '<img src=x onerror="alert(1)">', page: 'sites', icon: null, order: 1 }],
        pages: [{ pluginId: 'acme-sites', id: 'sites', path: '/sites', title: 'Sites' }],
      },
    })
    const wrapper = await mountLayout()

    const link = wrapper.get('[data-testid="nav-client-apps-acme-sites-sites"]')
    expect(link.find('img').exists()).toBe(false)
    expect(link.text()).toBe('<img src=x onerror="alert(1)">')
  })

  it('shows no plugin link, and every portal link, when the plugin index is refused', async () => {
    pluginIndex = () => ({ status: 404, body: { statusMessage: 'No client.', data: { code: 'CLIENT_PROFILE_NOT_FOUND' } } })
    const wrapper = await mountLayout()

    expect(wrapper.find('[data-testid^="nav-client-apps-"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="nav-client-account"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="client-topbar-name"]').text()).toBe('Ada Lovelace')
  })

  it('opens the mobile drawer with the menu button and closes it from the backdrop', async () => {
    const wrapper = await mountLayout()
    expect(wrapper.find('[data-testid="client-sidebar-backdrop"]').exists()).toBe(false)

    await wrapper.find('[data-testid="client-menu-toggle"]').trigger('click')
    expect(wrapper.find('[data-testid="client-sidebar"]').classes()).toContain('translate-x-0')
    expect(wrapper.find('[data-testid="client-sidebar-backdrop"]').exists()).toBe(true)

    await wrapper.find('[data-testid="client-sidebar-backdrop"]').trigger('click')
    expect(wrapper.find('[data-testid="client-sidebar"]').classes()).toContain('-translate-x-full')
  })

  it('opens one drawer below the top bar, with no brand of its own and no close control', async () => {
    const wrapper = await mountLayout()
    await wrapper.find('[data-testid="client-menu-toggle"]').trigger('click')

    const classes = wrapper.find('[data-testid="client-sidebar"]').classes()
    expect(classes).toContain('absolute')
    expect(classes).not.toContain('fixed')
    expect(wrapper.find('[data-testid="client-sidebar-backdrop"]').classes()).toContain('absolute')
    // The brand lives in the top bar only; the drawer starts with the menu.
    expect(wrapper.find('[data-testid="client-sidebar-brand"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="client-topbar-brand"]')).toHaveLength(1)
    expect(wrapper.find('[data-testid="client-sidebar-close"]').exists()).toBe(false)
  })

  it('closes the drawer on Escape, locks the page scroll while open and returns focus to the menu button', async () => {
    const wrapper = mountClient(ClientLayout, { slots: { default: 'page content' }, stubs: STUBS, components: { LayoutDemoBanner }, attachToBody: true })
    await settle()
    const toggle = wrapper.find('[data-testid="client-menu-toggle"]')
    ;(toggle.element as HTMLElement).focus()

    await toggle.trigger('click')
    await settle()
    expect(document.body.classList.contains('overflow-hidden')).toBe(true)
    expect(wrapper.find('[data-testid="client-sidebar"]').element.contains(document.activeElement)).toBe(true)

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    await nextTick()

    expect(wrapper.find('[data-testid="client-sidebar"]').classes()).toContain('-translate-x-full')
    expect(document.body.classList.contains('overflow-hidden')).toBe(false)
    expect(document.activeElement).toBe(toggle.element)
  })

  it('closes the drawer when a link is followed', async () => {
    const wrapper = await mountLayout()
    await wrapper.find('[data-testid="client-menu-toggle"]').trigger('click')

    await wrapper.find('[data-testid="nav-client-dashboard"]').trigger('click')

    expect(wrapper.find('[data-testid="client-sidebar-backdrop"]').exists()).toBe(false)
  })

  it('signs out through the auth store and returns to the sign-in page', async () => {
    const wrapper = await mountLayout()

    await wrapper.find('[data-testid="client-sign-out"]').trigger('click')
    await settle()

    expect(callsTo('POST', LOGOUT_URL)).toBe(1)
    expect(navigateTo).toHaveBeenCalledWith('/client/login')
  })

  it('re-initialises the platform widget once its script has loaded', async () => {
    const reinit = vi.fn()
    Object.assign(window, { InnoWidget: { reinit } })
    await mountLayout()

    await new Promise((resolve) => setTimeout(resolve, 120))

    expect(reinit).toHaveBeenCalled()
    Reflect.deleteProperty(window, 'InnoWidget')
  })

  it('stops looking for the platform widget once the layout is gone', async () => {
    // Only the timers are faked: the mount waits on real sockets with `setImmediate`, which must keep turning.
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'setInterval', 'clearInterval'] })
    try {
      const wrapper = await mountLayout()
      wrapper.unmount()
      const reinit = vi.fn()
      Object.assign(window, { InnoWidget: { reinit } })

      vi.runAllTimers()

      expect(reinit).not.toHaveBeenCalled()
    } finally {
      vi.useRealTimers()
      Reflect.deleteProperty(window, 'InnoWidget')
    }
  })

  it('schedules no widget poll when the layout is left while the identity is still loading', async () => {
    // A server of its own that holds the identity answer until the layout is gone; the stub server answers at once.
    let asked = false
    let release: () => void = () => {}
    const held = createServer((req, res) => {
      // The layout runs in a DOM environment that enforces the same-origin policy: the preflight is answered at once
      // and every answer carries a permissive origin, standing in for the same origin it has in production.
      res.setHeader('access-control-allow-origin', '*')
      if (req.method === 'OPTIONS') {
        res.writeHead(204, { 'access-control-allow-headers': String(req.headers['access-control-request-headers'] ?? '*') })
        res.end()
        return
      }
      if (req.url === ME_URL) {
        asked = true
        release = () => {
          res.writeHead(200, { 'Content-Type': 'application/json' })
          res.end(JSON.stringify(ME))
        }
        return
      }
      res.writeHead(200, { 'Content-Type': 'application/json' })
      res.end(JSON.stringify({ menus: [], pages: [] }))
    })
    await new Promise<void>(resolve => held.listen(0, '127.0.0.1', resolve))
    vi.stubGlobal('$fetch', ofetch.create({ baseURL: `http://127.0.0.1:${(held.address() as AddressInfo).port}` }))
    try {
      const wrapper = mountClient(ClientLayout, { slots: { default: 'page content' }, stubs: STUBS, components: { LayoutDemoBanner } })
      await vi.waitFor(() => expect(asked).toBe(true))
      wrapper.unmount()
      vi.useFakeTimers()
      release()
      // `vi.waitFor` polls on the real timers, so it still runs while the fake ones are installed.
      await vi.waitFor(() => expect(useClientStore().user).not.toBeNull())

      expect(vi.getTimerCount()).toBe(0)
    } finally {
      vi.useRealTimers()
      held.closeAllConnections()
      await new Promise<void>(resolve => held.close(() => resolve()))
    }
  })
})

describe('client layout without the platform widget', () => {
  beforeEach(() => {
    stubAuthRuntime({ publicConfig: { authMode: 'local', mainUrl: '' } })
  })

  it('draws its own account menu with the initials of the signed-in person instead of an empty mount', async () => {
    const wrapper = await mountLayout()

    expect(wrapper.find('#inno-account-mount').exists()).toBe(false)
    expect(wrapper.find('[data-testid="client-account-trigger"]').text()).toBe('AL')
  })

  it('offers the name, the e-mail and the permitted links in the menu', async () => {
    const wrapper = await mountLayout()
    useClientStore().user = { ...ME, permissions: Permission.ViewDomains } as never
    await nextTick()
    await wrapper.find('[data-testid="client-account-trigger"]').trigger('click')

    expect(wrapper.find('[data-testid="client-account-name"]').text()).toBe('Ada Lovelace')
    expect(wrapper.find('[data-testid="client-account-email"]').text()).toBe('ada@example.com')
    expect(wrapper.find('[data-testid="client-account-link-/client/domains"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="client-account-link-/client/invoices"]').exists()).toBe(false)
  })

  it('signs out from the menu', async () => {
    const wrapper = await mountLayout()
    await wrapper.find('[data-testid="client-account-trigger"]').trigger('click')

    await wrapper.find('[data-testid="client-account-sign-out"]').trigger('click')
    await settle()

    expect(callsTo('POST', LOGOUT_URL)).toBe(1)
    expect(navigateTo).toHaveBeenCalledWith('/client/login')
  })

  it('does not poll for a widget that is never loaded', async () => {
    // Only the timers are faked: the mount waits on real sockets with `setImmediate`, which must keep turning.
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'setInterval', 'clearInterval'] })
    const reinit = vi.fn()
    Object.assign(window, { InnoWidget: { reinit } })
    try {
      await mountLayout()

      // Long enough for the first poll (50 ms) and several more (200 ms apart) had there been one.
      vi.advanceTimersByTime(1000)

      expect(reinit).not.toHaveBeenCalled()
    } finally {
      vi.useRealTimers()
      Reflect.deleteProperty(window, 'InnoWidget')
    }
  })
})
