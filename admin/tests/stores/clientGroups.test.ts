/**
 * Tests for the client groups store: a load fills the list, and a refusal puts the API's sentence
 * into `error` while the groups already loaded stay. The request goes through the real `useApi`
 * transport.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { translate } from '@/lib/translate'
import { useClientGroupsStore } from '@/stores/clientGroups'
import { callOf, json, problem, stubFetch, stubOffline } from '../helpers/fetch'

describe('clientGroupsStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetchAll fills the groups', async () => {
    const mock = stubFetch(json([{ id: 2, name: 'Wholesale' }]))
    const store = useClientGroupsStore()

    expect(await store.fetchAll()).toBe(true)
    expect(callOf(mock).url).toBe('/api/admin/client-groups')
    expect(store.groups).toEqual([{ id: 2, name: 'Wholesale' }])
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
  })

  it('fetchAll reports the API sentence, and the no-answer sentence offline, and keeps the groups it had', async () => {
    const store = useClientGroupsStore()
    stubFetch(json([{ id: 2, name: 'Wholesale' }]))
    await store.fetchAll()

    stubFetch(problem('Not allowed.', 403))
    expect(await store.fetchAll()).toBe(false)
    expect(store.error).toBe('Not allowed.')

    stubOffline()
    expect(await store.fetchAll()).toBe(false)
    expect(store.error).toBe(translate('errors.noAnswer'))
    expect(store.groups).toEqual([{ id: 2, name: 'Wholesale' }])
  })
})
