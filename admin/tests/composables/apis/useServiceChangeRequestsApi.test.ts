/**
 * Tests for the service change requests API composable: paths, verbs, query strings and bodies
 * through the real `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useServiceChangeRequestsApi } from '@/composables/apis/useServiceChangeRequestsApi'
import { ApiError } from '@/composables/useApi'
import { ServiceChangeStatus } from '@/types/servicechangerequest'
import { json, problem, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useServiceChangeRequestsApi', () => {
  it('list sends the filters and the page', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))

    await useServiceChangeRequestsApi().list(ServiceChangeStatus.Pending, 7, 2, 20)
    expect(callOf(mock)).toMatchObject({
      url: '/api/admin/service-change-requests?status=Pending&serviceId=7&page=2&pageSize=20',
      method: 'GET',
    })
  })

  it('list leaves out an empty status and no service', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))

    await useServiceChangeRequestsApi().list('', null, 1, 20)
    expect(callOf(mock).url).toBe('/api/admin/service-change-requests?page=1&pageSize=20')
  })

  it('approve and reject POST the note', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    await useServiceChangeRequestsApi().approve(4, 'ok')
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/service-change-requests/4/approve', method: 'POST', body: { note: 'ok' } })

    const second = stubFetch(new Response(null, { status: 204 }))
    await useServiceChangeRequestsApi().reject(4, null)
    expect(callOf(second)).toMatchObject({ url: '/api/admin/service-change-requests/4/reject', method: 'POST', body: { note: null } })
  })

  it('rejects with an ApiError carrying the refusal body', async () => {
    stubFetch(problem('Not found.', 404))

    await expect(useServiceChangeRequestsApi().approve(4, null)).rejects.toBeInstanceOf(ApiError)
  })
})
