/**
 * Unit tests for `layouts/client.vue` against the real auth and client stores: the
 * permission-filtered navigation, the mobile drawer, the identity fetch, the platform widget
 * hand-over and signing out. Only the API composables are replaced.
 *
 * @module tests/layouts/client.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises } from '@vue/test-utils'
import { nextTick } from 'vue'
import { storeToRefs } from 'pinia'
import { mountClient } from '~/tests/helpers/mountClient'
import { stubAuthRuntime } from '~/tests/helpers/authScreens'
import { makeClientUser } from '~/tests/helpers/clientUser'

/** The mocked identity call, re-created for every test. */
const fetchMe = vi.fn()

/** The mocked local sign-out call, re-created for every test. */
const logout = vi.fn()

vi.mock('~/composables/apis/useClientApi', () => ({
  /**
   * Stands in for the real API composable so the client store runs without transport.
   *
   * @returns Only the endpoint the layout reaches.
   */
  useClientApi: () => ({ fetchMe })
}))

vi.mock('~/composables/apis/useAuthApi', () => ({
  /**
   * Stands in for the real API composable so the auth store runs without transport.
   *
   * @returns Only the endpoint the layout reaches.
   */
  useAuthApi: () => ({ logout })
}))

const { default: ClientLayout } = await import('~/layouts/client.vue')
const { default: LayoutDemoBanner } = await import('~/components/layout/DemoBanner.vue')
const { Permission } = await import('~/composables/usePermissions')
const { useAuthStore } = await import('~/stores/auth')
const { useClientStore } = await import('~/stores/client')

/** The theme and language controls need runtime services this test does not provide. */
const STUBS = { UiThemeToggle: true, UiLanguageSwitcher: true, UiCookieBanner: { template: '<div data-testid="cookie-banner" />' } }

/** The signed-in person the identity call answers with. */
const ME = makeClientUser({ email: 'ada@example.com', permissions: Permission.All })

// Unmounting releases the drawer's scroll lock and key listener between tests.
enableAutoUnmount(afterEach)

/** Runtime configuration of a deployment that is served the platform shell's widget. */
const SHELL_CONFIG = { authMode: 'sso', mainUrl: 'https://main.example.test' }

beforeEach(() => {
  stubAuthRuntime({ publicConfig: SHELL_CONFIG })
  vi.stubGlobal('useAuthStore', useAuthStore)
  vi.stubGlobal('useClientStore', useClientStore)
  vi.stubGlobal('storeToRefs', storeToRefs)
  fetchMe.mockReset()
  fetchMe.mockResolvedValue(ME)
  logout.mockReset()
  logout.mockResolvedValue({})
})

/**
 * Mounts the layout with a page in its slot and waits for the identity fetch.
 *
 * @returns The wrapper.
 */
const mountLayout = async () => {
  const wrapper = mountClient(ClientLayout, { slots: { default: 'page content' }, stubs: STUBS, components: { LayoutDemoBanner } })
  await flushPromises()
  return wrapper
}

describe('client layout', () => {
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

    expect(fetchMe).toHaveBeenCalledTimes(1)
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

    for (const id of ['dashboard', 'services', 'domains', 'invoices', 'tickets', 'account']) {
      expect(wrapper.find(`[data-testid="nav-client-${id}"]`).exists()).toBe(true)
    }
    expect(wrapper.find('[data-testid="nav-announcements"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="nav-knowledgebase"]').exists()).toBe(true)
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

  it('opens the mobile drawer with the menu button and closes it from the backdrop', async () => {
    const wrapper = await mountLayout()
    expect(wrapper.find('[data-testid="client-sidebar-backdrop"]').exists()).toBe(false)

    await wrapper.find('[data-testid="client-menu-toggle"]').trigger('click')
    expect(wrapper.find('[data-testid="client-sidebar"]').classes()).toContain('translate-x-0')
    expect(wrapper.find('[data-testid="client-sidebar-backdrop"]').exists()).toBe(true)

    await wrapper.find('[data-testid="client-sidebar-backdrop"]').trigger('click')
    expect(wrapper.find('[data-testid="client-sidebar"]').classes()).toContain('-translate-x-full')
  })

  it('opens one drawer below the top bar, with a single brand and close control', async () => {
    const wrapper = await mountLayout()
    await wrapper.find('[data-testid="client-menu-toggle"]').trigger('click')

    const classes = wrapper.find('[data-testid="client-sidebar"]').classes()
    expect(classes).toContain('absolute')
    expect(classes).not.toContain('fixed')
    expect(wrapper.find('[data-testid="client-sidebar-backdrop"]').classes()).toContain('absolute')
    expect(wrapper.find('[data-testid="client-sidebar-brand"]').element.parentElement?.classList.contains('hidden')).toBe(true)
    expect(wrapper.find('[data-testid="client-sidebar-close"]').exists()).toBe(false)
  })

  it('closes the drawer on Escape, locks the page scroll while open and returns focus to the menu button', async () => {
    const wrapper = mountClient(ClientLayout, { slots: { default: 'page content' }, stubs: STUBS, components: { LayoutDemoBanner }, attachToBody: true })
    await flushPromises()
    const toggle = wrapper.find('[data-testid="client-menu-toggle"]')
    ;(toggle.element as HTMLElement).focus()

    await toggle.trigger('click')
    await flushPromises()
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
    await flushPromises()

    expect(logout).toHaveBeenCalledTimes(1)
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
    vi.useFakeTimers()
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
    vi.useFakeTimers()
    try {
      /** Resolves the pending identity request once the layout is already gone. */
      let answer: (value: typeof ME) => void = () => {}
      fetchMe.mockReturnValue(new Promise<typeof ME>((resolve) => {
        answer = resolve
      }))
      const wrapper = mountClient(ClientLayout, { slots: { default: 'page content' }, stubs: STUBS, components: { LayoutDemoBanner } })
      wrapper.unmount()
      answer(ME)
      await flushPromises()

      expect(vi.getTimerCount()).toBe(0)
    } finally {
      vi.useRealTimers()
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
    await flushPromises()

    expect(logout).toHaveBeenCalledTimes(1)
    expect(navigateTo).toHaveBeenCalledWith('/client/login')
  })

  it('does not poll for a widget that is never loaded', async () => {
    vi.useFakeTimers()
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
