/**
 * Tests for the client groups store: the load fills the list, every write re-reads it, and a
 * refusal lands in the matching error state while the action reports failure as a value.
 */

import { describe, expect, it } from 'vitest'
import { translate } from '@/lib/translate'
import { useClientGroupsStore } from '@/stores/clientGroups'
import type { ClientGroupPayload } from '@/types/clientgroup'
import { callsOf, invalid, json, problem, stubFetch, stubOffline } from '../helpers/fetch'
import { installStoreHooks } from '../helpers/storeHooks'

/** A group as the create and update calls take it. */
const payload: ClientGroupPayload = { name: 'VIP', description: null, discountPercent: 10, color: '#112233' }

/** One group as the list answers it. */
const group = {
  id: 1,
  name: 'VIP',
  description: null,
  discountPercent: 10,
  color: '#112233',
  isDefault: false,
  clientCount: 0,
}

describe('useClientGroupsStore', () => {
  installStoreHooks()

  it('fetchAll fills the list', async () => {
    stubFetch(json([group]))
    const store = useClientGroupsStore()

    expect(await store.fetchAll()).toBe(true)

    expect(store.groups).toEqual([group])
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
  })

  it('keeps the groups it had after a refused or unanswered load, with the matching sentence', async () => {
    const store = useClientGroupsStore()
    stubFetch(json([group]))
    await store.fetchAll()

    stubFetch(problem('Locked.', 423))
    expect(await store.fetchAll()).toBe(false)
    expect(store.error).toBe('Locked.')

    stubOffline()
    expect(await store.fetchAll()).toBe(false)
    expect(store.error).toBe(translate('errors.noAnswer'))
    expect(store.groups).toEqual([group])
  })

  it('create posts, then re-reads the list', async () => {
    const mock = stubFetch(json(1, 201), json([group]))
    const store = useClientGroupsStore()

    expect(await store.create(payload)).toBe(true)
    expect(callsOf(mock).map(call => call.method)).toEqual(['POST', 'GET'])
    expect(store.groups).toEqual([group])
    expect(store.saving).toBe(false)
  })

  it('a refused create returns false with the sentence and the field messages', async () => {
    stubFetch(invalid('Validation failed', { name: ['Name is taken.'] }))
    const store = useClientGroupsStore()

    expect(await store.create(payload)).toBe(false)
    expect(store.saveError).toBe('Validation failed')
    expect(store.fieldErrors.name).toBe('Name is taken.')

    store.resetSaveError()

    expect(store.saveError).toBeNull()
    expect(store.fieldErrors).toEqual({})
  })

  it('update puts, then re-reads the list', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }), json([group]))
    const store = useClientGroupsStore()

    expect(await store.update(1, payload)).toBe(true)
    expect(callsOf(mock).map(call => `${call.method} ${call.url}`)).toEqual([
      'PUT /api/admin/client-groups/1',
      'GET /api/admin/client-groups',
    ])
  })

  it('setDefault and remove re-read the list; a refusal sets actionError', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }), json([group]))
    const store = useClientGroupsStore()

    expect(await store.setDefault(1, true)).toBe(true)
    expect(await store.remove(1)).toBe(true)
    expect(callsOf(mock).map(call => call.method)).toEqual(['PUT', 'GET', 'DELETE', 'GET'])

    stubFetch(problem('Group still has clients.', 409))

    expect(await store.remove(1)).toBe(false)
    expect(store.actionError).toBe('Group still has clients.')

    store.clearActionError()

    expect(store.actionError).toBeNull()
  })
})
