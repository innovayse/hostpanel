/**
 * Tests for the cancellation requests API composable: paths, verbs and query strings through
 * the real `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useCancellationRequestsApi } from '@/composables/apis/useCancellationRequestsApi'
import { ApiError } from '@/composables/useApi'
import { CancellationStatus, CancellationType } from '@/types/service'
import { json, problem, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useCancellationRequestsApi', () => {
  it('list pages the requests', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))

    await useCancellationRequestsApi().list(2, 20)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/cancellation-requests?page=2&pageSize=20', method: 'GET' })
  })

  it('list sends the search, the type and the status that are set and drops the rest', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))
    const api = useCancellationRequestsApi()

    await api.list(1, 20, { search: '', type: undefined, status: undefined })
    await api.list(2, 20, { search: 'moving', type: CancellationType.Immediate, status: CancellationStatus.Closed })
    expect(callOf(mock, 0).url).toBe('/api/admin/cancellation-requests?page=1&pageSize=20')
    expect(callOf(mock, 1).url).toBe('/api/admin/cancellation-requests?page=2&pageSize=20&search=moving&type=Immediate&status=Closed')
  })

  it('remove DELETEs the request', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useCancellationRequestsApi().remove(6)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/cancellation-requests/6', method: 'DELETE' })
  })

  it('rejects with an ApiError carrying the refusal body', async () => {
    stubFetch(problem('Request not found.', 404))

    await expect(useCancellationRequestsApi().remove(6)).rejects.toBeInstanceOf(ApiError)
  })
})
