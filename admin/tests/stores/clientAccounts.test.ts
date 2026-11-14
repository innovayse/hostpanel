/**
 * Tests for the admin users store: loads, mutations and the API sentence on refusal.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useUsersStore } from '@/stores/clientAccounts'
import { json, problem, stubFetch, callOf } from '../helpers/fetch'
import { installStoreHooks } from '../helpers/storeHooks'

describe('usersStore mutations', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('updateUser sends the profile and returns true', async () => {
    const mock = stubFetch(json({}))
    const store = useUsersStore()

    expect(await store.updateUser('u1', { firstName: 'A', lastName: 'B', email: 'a@b.c', language: null })).toBe(true)
    expect(mock.mock.calls[0]![0]).toBe('/api/admin/users/u1')
    expect(store.actionError).toBeNull()
  })

  it('deleteUser, sendPasswordReset and changePassword surface the API sentence', async () => {
    const store = useUsersStore()
    stubFetch(problem('User owns a client.'))

    expect(await store.deleteUser('u1')).toBe(false)
    expect(store.actionError).toBe('User owns a client.')
    expect(await store.sendPasswordReset('u1')).toBe(false)
    expect(await store.changePassword('u1', 'secret')).toBe(false)
  })
})

describe('usersStore loads', () => {
  installStoreHooks()

  it('fetchAll fills the list, sending the search term', async () => {
    const mock = stubFetch(json({ items: [{ id: 'u1' }], totalCount: 1 }))
    const store = useUsersStore()

    expect(await store.fetchAll(1, { search: 'ann' })).toBe(true)
    expect(store.users).toEqual([{ id: 'u1' }])
    expect(callOf(mock).url).toBe('/api/admin/users?page=1&pageSize=20&search=ann')
  })

  it('fetchAll asks for the page it is given and leaves an empty search out', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))
    const store = useUsersStore()

    await store.fetchAll(3, { search: '' })
    expect(callOf(mock).url).toBe('/api/admin/users?page=3&pageSize=20')
  })

  it('fetchAll answers false and shows the sentence when the list is refused', async () => {
    const store = useUsersStore()
    stubFetch(problem('Users are not available.', 403))

    expect(await store.fetchAll(1)).toBe(false)
    expect(store.error).toBe('Users are not available.')
  })

  it('fetchById sets the user and shows the sentence on refusal', async () => {
    const store = useUsersStore()
    stubFetch(json({ id: 'u1', accounts: [] }))
    await store.fetchById('u1')
    expect(store.current).toEqual({ id: 'u1', accounts: [] })

    stubFetch(problem('User not found.', 404))
    await store.fetchById('u2')
    expect(store.current).toBeNull()
    expect(store.error).toBe('User not found.')
  })
})
