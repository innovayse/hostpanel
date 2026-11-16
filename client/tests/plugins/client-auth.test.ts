/**
 * Tests for `plugins/client-auth.ts`: the profile is fetched once after hydration, and only
 * when a session exists.
 *
 * The real auth and client stores, their API composable and the transport run against a stub server
 * playing the BFF; only the Nuxt plugin wrapper and the session cookie are stood in for.
 *
 * @module tests/plugins/client-auth.test
 */

import { createPinia, setActivePinia } from 'pinia'
import { ref } from 'vue'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AccountEndpoint } from '~/tests/helpers/accountPaths'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { readOf, requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { SessionCookie } from '~/constants/sessionCookie'
import { useAuthStore } from '~/stores/auth'

/** The stub playing the BFF. */
let stub: StubServer

/** The account the stub answers the identity read with. */
const ACCOUNT = makeClientUser()

/**
 * Loads the plugin and runs it for a visitor with or without the session flag cookie.
 *
 * @param signedIn - Whether the readable session flag cookie is set.
 */
const runPlugin = async (signedIn: boolean): Promise<void> => {
  vi.stubGlobal('defineNuxtPlugin', (setup: () => Promise<void>) => setup)
  vi.stubGlobal('useAuthStore', useAuthStore)
  vi.stubGlobal('useCookie', (name: string) => ref(signedIn && name === SessionCookie.Authed ? '1' : null))
  /** The plugin as Nuxt calls it. */
  const plugin = (await import('~/plugins/client-auth')).default as unknown as () => Promise<void>
  await plugin()
}

beforeEach(async () => {
  setActivePinia(createPinia())
  stub = await startTransport(routeTable({ [readOf(AccountEndpoint.Me)]: { body: ACCOUNT } }))
  vi.resetModules()
})

afterEach(async () => {
  await stub.close()
  vi.unstubAllGlobals()
})

describe('client-auth plugin', () => {
  it('fetches the profile when a session exists', async () => {
    await runPlugin(true)

    expect(requestsTo(stub.requests, AccountEndpoint.Me)).toHaveLength(1)
    expect(useAuthStore().user?.email).toBe(ACCOUNT.email)
  })

  it('does not call the API for an anonymous visitor', async () => {
    await runPlugin(false)

    expect(requestsTo(stub.requests, AccountEndpoint.Me)).toHaveLength(0)
  })
})
