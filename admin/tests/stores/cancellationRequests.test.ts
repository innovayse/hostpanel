/**
 * Tests for the cancellation requests store: the paged load and the delete.
 */

import { describe, expect, it } from 'vitest'
import { useCancellationRequestsStore } from '@/stores/cancellationRequests'
import { CancellationStatus, CancellationType } from '@/types/service'
import { json, problem, stubFetch, callOf } from '../helpers/fetch'
import { installStoreHooks } from '../helpers/storeHooks'

describe('cancellationRequestsStore', () => {
  installStoreHooks()

  it('fetchAll loads the page it is given with the store page size', async () => {
    const mock = stubFetch(json({ items: [{ id: 1 }], totalCount: 5 }))
    const store = useCancellationRequestsStore()

    expect(await store.fetchAll(1)).toBe(true)
    expect(store.cancellations).toEqual([{ id: 1 }])
    expect(store.totalCount).toBe(5)
    expect(callOf(mock).url).toBe('/api/admin/cancellation-requests?page=1&pageSize=20')
  })

  it('fetchAll sends the filters and trims the search', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))
    const store = useCancellationRequestsStore()

    await store.fetchAll(2, { search: '  moving  ', type: CancellationType.EndOfBillingPeriod, status: CancellationStatus.Open })
    expect(callOf(mock).url).toBe(
      '/api/admin/cancellation-requests?page=2&pageSize=20&search=moving&type=EndOfBillingPeriod&status=Open',
    )
  })

  it('fetchAll reports a refusal with its sentence', async () => {
    stubFetch(problem('Not allowed.', 403))
    const store = useCancellationRequestsStore()

    expect(await store.fetchAll(1)).toBe(false)
    expect(store.error).toBe('Not allowed.')
    expect(store.loading).toBe(false)
  })

  it('deleteRequest deletes once, leaves the reload to the screen and keeps the sentence on refusal', async () => {
    const store = useCancellationRequestsStore()
    const mock = stubFetch(new Response(null, { status: 204 }))
    expect(await store.deleteRequest(3)).toBe(true)
    expect(mock).toHaveBeenCalledTimes(1)

    stubFetch(problem('Request not found.', 404))
    expect(await store.deleteRequest(3)).toBe(false)
    expect(store.error).toBe('Request not found.')
    expect(store.loading).toBe(false)
  })
})
