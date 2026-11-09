/**
 * Tests for the version store: one request however many callers ask, a kept value, a failure that
 * is not cached, and a reset on sign-out and when the account or its roles change.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAuthStore } from '@/stores/auth'
import { useVersionStore } from '@/stores/version'
import { json, problem, stubFetch } from '../helpers/fetch'

/** The version the API reports. */
const dto = { installedVersion: '1.4.0' }

/**
 * Signs a staff account in on the auth store without any request.
 *
 * @param roles - Roles the account holds.
 */
const signIn = (roles: string[]): void => {
  useAuthStore().user = { email: 'staff@example.com', roles }
}

describe('versionStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('makes one request for several callers and keeps the value', async () => {
    const mock = stubFetch(json(dto))
    const store = useVersionStore()

    await Promise.all([store.ensureLoaded(), store.ensureLoaded(), store.ensureLoaded()])
    await store.ensureLoaded()

    expect(mock).toHaveBeenCalledTimes(1)
    expect(store.version).toBe('1.4.0')
  })

  it('does not cache a failure: the next call asks again', async () => {
    const refused = stubFetch(problem('No access.', 403))
    const store = useVersionStore()

    await store.ensureLoaded()

    expect(store.version).toBe('')
    expect(store.error).toBe('No access.')

    const answered = stubFetch(json(dto))
    await store.ensureLoaded()

    expect(refused).toHaveBeenCalledTimes(1)
    expect(answered).toHaveBeenCalledTimes(1)
    expect(store.version).toBe('1.4.0')
  })

  it('resets on sign-out and loads again for the next account', async () => {
    const mock = stubFetch(json(dto))
    signIn(['Reseller'])
    const store = useVersionStore()
    await store.ensureLoaded()

    useAuthStore().user = null

    expect(store.version).toBe('')

    signIn(['Reseller'])
    await store.ensureLoaded()

    expect(mock).toHaveBeenCalledTimes(2)
    expect(store.version).toBe('1.4.0')
  })

  it('reloads after the roles change', async () => {
    const mock = stubFetch(json(dto))
    signIn(['Reseller'])
    const store = useVersionStore()
    await store.ensureLoaded()

    signIn(['Reseller', 'Admin'])

    expect(store.version).toBe('')

    await store.ensureLoaded()

    expect(mock).toHaveBeenCalledTimes(2)
    expect(store.version).toBe('1.4.0')
  })

  it('keeps the value when the user is replaced by an identical account', async () => {
    const mock = stubFetch(json(dto))
    signIn(['Admin'])
    const store = useVersionStore()
    await store.ensureLoaded()

    signIn(['Admin'])
    await store.ensureLoaded()

    expect(mock).toHaveBeenCalledTimes(1)
    expect(store.version).toBe('1.4.0')
  })

  it('ignores the answer of a request that was started before a reset', async () => {
    let release: (response: Response) => void = () => undefined
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>((resolve) => { release = resolve })))
    signIn(['Admin'])
    const store = useVersionStore()
    const pending = store.ensureLoaded()

    useAuthStore().user = null
    release(new Response(JSON.stringify(dto), { status: 200, headers: { 'content-type': 'application/json' } }))
    await pending

    expect(store.version).toBe('')
  })
})
