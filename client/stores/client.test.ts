/**
 * Regression tests for the identity request in `stores/client.ts`.
 *
 * The store is the layer that decides when `/api/portal/client/me` is asked for, so it is the
 * layer these assertions belong to. A client-area page load has three callers — the
 * `client-auth` plugin (through `stores/auth.ts`), the `client` layout's `onMounted`, and the
 * page's own fetch — and they all start before the first answer returns. Production served
 * three identical `/me` requests per page load for exactly that reason. Without a test naming
 * the invariant, the fourth caller someone adds brings the duplication straight back.
 *
 * @module stores/client.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

/** The mocked `/api/portal/client/me` call, re-created for every test. */
const fetchMe = vi.fn()

vi.mock('~/composables/apis/useClientApi', () => ({
  /**
   * Stands in for the real API composable so the store can be exercised without transport.
   *
   * @returns Only the endpoint functions these tests reach for.
   */
  useClientApi: () => ({ fetchMe }),
}))

const { useClientStore } = await import('~/stores/client')

/**
 * Builds a promise the test resolves or rejects by hand, so several callers can be observed
 * while the request is still in flight.
 *
 * @returns The pending promise and the two functions that settle it.
 */
function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<T>((res, rej) => { resolve = res; reject = rej })
  return { promise, resolve, reject }
}

/** A minimal profile — only the fields the assertions read. */
const profile = { id: 1, firstname: 'Ada', lastname: 'Lovelace', email: 'ada@example.com' }

describe('useClientStore.fetchUser', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    fetchMe.mockReset()
  })

  it('asks for the identity once when all three page-load callers ask at the same time', async () => {
    const gate = deferred<typeof profile>()
    fetchMe.mockReturnValue(gate.promise)

    const store = useClientStore()

    // The plugin, the layout and the page, all before the first answer comes back.
    const callers = Promise.all([store.fetchUser(), store.fetchUser(), store.fetchUser()])
    expect(fetchMe).toHaveBeenCalledTimes(1)

    gate.resolve(profile)
    await callers

    expect(fetchMe).toHaveBeenCalledTimes(1)
    expect(store.user).toEqual(profile)
    expect(store.userLoaded).toBe(true)
    expect(store.userLoading).toBe(false)
  })

  it('every concurrent caller waits for the answer rather than returning early and empty', async () => {
    const gate = deferred<typeof profile>()
    fetchMe.mockReturnValue(gate.promise)

    const store = useClientStore()
    const seen: Array<typeof profile | null> = []
    const callers = Promise.all([
      store.fetchUser().then(() => { seen.push(store.user) }),
      store.fetchUser().then(() => { seen.push(store.user) }),
    ])

    gate.resolve(profile)
    await callers

    expect(seen).toEqual([profile, profile])
  })

  it('does not ask again once the identity is loaded', async () => {
    fetchMe.mockResolvedValue(profile)
    const store = useClientStore()

    await store.fetchUser()
    await store.fetchUser()

    expect(fetchMe).toHaveBeenCalledTimes(1)
  })

  it('asks again when a caller forces a refresh after saving the profile', async () => {
    fetchMe.mockResolvedValue(profile)
    const store = useClientStore()

    await store.fetchUser()
    await store.fetchUser(true)

    expect(fetchMe).toHaveBeenCalledTimes(2)
  })

  it('asks once, not three times, for a staff identity with no client record', async () => {
    const gate = deferred<typeof profile>()
    fetchMe.mockReturnValue(gate.promise)

    const store = useClientStore()
    const callers = Promise.all([store.fetchUser(), store.fetchUser(), store.fetchUser()])

    gate.reject({ data: { code: 'CLIENT_PROFILE_NOT_FOUND' } })
    await callers

    expect(fetchMe).toHaveBeenCalledTimes(1)
    expect(store.clientProfileMissing).toBe(true)
    // Not a fault, so nothing renders it in red.
    expect(store.userError).toBeNull()
    // And not remembered as loaded either: a later navigation may ask again, so the answer
    // is never cached as a permanent "no".
    expect(store.userLoaded).toBe(false)
  })

  it('does not let a signed-out session’s answer land on the next visitor', async () => {
    const gate = deferred<typeof profile>()
    fetchMe.mockReturnValue(gate.promise)

    const store = useClientStore()
    const pending = store.fetchUser()

    // Signing out while the identity request is still on the wire.
    store.reset()

    gate.resolve(profile)
    await pending

    expect(store.user).toBeNull()
    expect(store.userLoaded).toBe(false)
    expect(store.userLoading).toBe(false)
  })

  it('lets the next sign-in fetch a fresh identity rather than joining the old request', async () => {
    const first = deferred<typeof profile>()
    fetchMe.mockReturnValueOnce(first.promise)

    const store = useClientStore()
    const stale = store.fetchUser()
    store.reset()

    const next = { ...profile, id: 2, firstname: 'Grace', email: 'grace@example.com' }
    fetchMe.mockResolvedValueOnce(next)
    await store.fetchUser()

    first.resolve(profile)
    await stale

    expect(fetchMe).toHaveBeenCalledTimes(2)
    expect(store.user).toEqual(next)
  })
})
