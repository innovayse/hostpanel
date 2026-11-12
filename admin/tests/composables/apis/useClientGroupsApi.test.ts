/**
 * Tests for the client groups API composable: paths, verbs and bodies through the real `useApi`
 * transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useClientGroupsApi } from '@/composables/apis/useClientGroupsApi'
import { ApiError } from '@/composables/useApi'
import type { ClientGroupPayload } from '@/types/clientgroup'
import { callOf, callsOf, json, problem, stubFetch } from '../../helpers/fetch'

/** A group as the create and update calls take it. */
const payload: ClientGroupPayload = { name: 'Resellers', description: null, discountPercent: 15, color: '#3B82F6' }

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useClientGroupsApi', () => {
  it('listGroups reads the collection', async () => {
    const mock = stubFetch(json([{ id: 1 }]))

    expect(await useClientGroupsApi().listGroups()).toEqual([{ id: 1 }])
    expect(callOf(mock).url).toBe('/api/admin/client-groups')
    expect(callOf(mock).method).toBe('GET')
  })

  it('createGroup POSTs the fields and answers the new id', async () => {
    const mock = stubFetch(json(9, 201))

    expect(await useClientGroupsApi().createGroup(payload)).toBe(9)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/client-groups', method: 'POST', body: payload })
  })

  it('updateGroup PUTs the fields to the group, setDefaultGroup PUTs the flag and removeGroup DELETEs', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useClientGroupsApi()

    await api.updateGroup(4, payload)
    await api.setDefaultGroup(4, true)
    await api.removeGroup(4)

    expect(callsOf(mock).map(call => [call.method, call.url, call.body])).toEqual([
      ['PUT', '/api/admin/client-groups/4', payload],
      ['PUT', '/api/admin/client-groups/4/default', { isDefault: true }],
      ['DELETE', '/api/admin/client-groups/4', undefined],
    ])
  })

  it('rejects with the API refusal', async () => {
    stubFetch(problem('Group in use.', 409))

    await expect(useClientGroupsApi().removeGroup(4)).rejects.toBeInstanceOf(ApiError)
  })
})
