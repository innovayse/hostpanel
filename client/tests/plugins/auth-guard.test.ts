/**
 * Tests for `plugins/auth-guard.ts`: when the session expires the client state and the `authed`
 * cookie are cleared, the plugin menu is forgotten, and the customer is sent to sign in, carrying the page they were on.
 *
 * The real client and plugin stores run in a real Pinia; only the Nuxt router, cookie and navigation are stand-ins.
 *
 * @module tests/plugins/auth-guard.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { useClientStore } from '~/stores/client'
import { usePluginsStore } from '~/stores/plugins'

/** The `authed` cookie the plugin must clear. */
const authed = { value: 'yes' as string | null }

/** The recorded navigation. */
const navigateToMock = vi.fn()

/** The `$handleAuthExpired` the plugin provides. */
type Handler = () => unknown

/**
 * Loads the plugin on a page and returns the function it provides.
 *
 * @param fullPath - The route the customer is on when the session expires.
 * @returns The provided `$handleAuthExpired`.
 */
const loadHandler = async (fullPath: string): Promise<Handler> => {
  vi.stubGlobal('defineNuxtPlugin', (setup: () => { provide: { handleAuthExpired: Handler } }) => setup)
  vi.stubGlobal('useRouter', () => ({ currentRoute: { value: { fullPath, path: fullPath.split('?')[0] } } }))
  vi.stubGlobal('useCookie', () => authed)
  vi.stubGlobal('navigateTo', navigateToMock)
  /** The auth guard plugin, imported after the Nuxt globals are stubbed. */
  const plugin = (await import('~/plugins/auth-guard')).default as unknown as () => { provide: { handleAuthExpired: Handler } }
  return plugin().provide.handleAuthExpired
}

/**
 * Loads a signed-in person into the client store, as it is while a session is alive.
 *
 * @returns The client store.
 */
const signIn = (): ReturnType<typeof useClientStore> => {
  const client = useClientStore()
  client.user = makeClientUser()
  client.userLoaded = true
  return client
}

beforeEach(() => {
  setActivePinia(createPinia())
  navigateToMock.mockClear()
  authed.value = 'yes'
})

describe('auth-guard plugin', () => {
  it('forgets the plugin menu of the person whose session ended', async () => {
    const handle = await loadHandler('/client')
    const plugins = usePluginsStore()
    plugins.menu = [{ label: 'My sites', icon: null, url: '/client/apps/acme-sites/sites' }]
    plugins.uiLoaded = true

    handle()

    expect(plugins.menu).toEqual([])
    expect(plugins.uiLoaded).toBe(false)
  })

  it('forgets the unsent chat drafts of the person whose session ended, and nothing else', async () => {
    window.sessionStorage.setItem('hostpanel.plugin-chat-draft:acme:/chat:[]', 'Half a thought')
    window.sessionStorage.setItem('unrelated', 'kept')
    const handle = await loadHandler('/client')

    handle()

    expect(window.sessionStorage.getItem('hostpanel.plugin-chat-draft:acme:/chat:[]')).toBeNull()
    expect(window.sessionStorage.getItem('unrelated')).toBe('kept')
    window.sessionStorage.clear()
  })

  it('clears the client state and the cookie, then goes to sign-in carrying the current page', async () => {
    const client = signIn()
    const handle = await loadHandler('/client/invoices/7?tab=pay')

    handle()

    expect(client.user).toBeNull()
    expect(client.userLoaded).toBe(false)
    expect(authed.value).toBeNull()
    expect(navigateToMock).toHaveBeenCalledWith('/client/login?redirect=%2Fclient%2Finvoices%2F7%3Ftab%3Dpay')
  })

  it('stays on a public page, keeping its return path, when a sign-in is refused there', async () => {
    const client = signIn()
    const handle = await loadHandler('/client/login?redirect=/client/invoices/24')

    handle()

    expect(client.user).toBeNull()
    expect(navigateToMock).not.toHaveBeenCalled()
  })

  it('stays on a localized public page too', async () => {
    const handle = await loadHandler('/ru/client/login?redirect=/client/invoices/24')

    handle()

    expect(navigateToMock).not.toHaveBeenCalled()
  })

  it.each(['/', '/hosting', '/domains', '/ru/domains/transfer', '/cart', '/checkout'])(
    'leaves a visitor on the open page %s when a stale session answers 401',
    async (path) => {
      const client = signIn()
      const handle = await loadHandler(path)

      handle()

      expect(client.user).toBeNull()
      expect(authed.value).toBeNull()
      expect(navigateToMock).not.toHaveBeenCalled()
    }
  )

  it('still sends a localized signed-in page to sign-in', async () => {
    const handle = await loadHandler('/ru/client/invoices/7')

    handle()

    expect(navigateToMock).toHaveBeenCalledWith('/client/login?redirect=%2Fru%2Fclient%2Finvoices%2F7')
  })
})
