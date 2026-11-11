/**
 * Tests for the client groups API composable: the path and verb through the real `useApi`
 * transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useClientGroupsApi } from '@/composables/apis/useClientGroupsApi'
import { callOf, json, stubFetch } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useClientGroupsApi', () => {
  it('listGroups reads the collection', async () => {
    const mock = stubFetch(json([{ id: 1, name: 'Wholesale' }]))

    expect(await useClientGroupsApi().listGroups()).toEqual([{ id: 1, name: 'Wholesale' }])
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/client-groups', method: 'GET' })
  })
})
