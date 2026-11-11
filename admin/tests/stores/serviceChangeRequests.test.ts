/**
 * Tests for the service change requests store: the filtered load and the two decisions.
 */

import { describe, expect, it } from 'vitest'
import { useServiceChangeRequestsStore } from '@/stores/serviceChangeRequests'
import { ServiceChangeStatus } from '@/types/servicechangerequest'
import { json, problem, stubFetch, callOf } from '../helpers/fetch'
import { installStoreHooks } from '../helpers/storeHooks'

describe('serviceChangeRequestsStore', () => {
  installStoreHooks()

  it('fetchAll fills the page using the filters and paging of the store', async () => {
    const mock = stubFetch(json({ items: [{ id: 1 }], totalCount: 5 }))
    const store = useServiceChangeRequestsStore()
    store.status = ServiceChangeStatus.Pending
    store.serviceId = 3

    await store.fetchAll()
    expect(store.requests).toEqual([{ id: 1 }])
    expect(store.totalCount).toBe(5)
    expect(callOf(mock).url).toBe('/api/admin/service-change-requests?status=Pending&serviceId=3&page=1&pageSize=20')
  })

  it('approve and reject reload on success', async () => {
    const store = useServiceChangeRequestsStore()
    const mock = stubFetch(new Response(null, { status: 204 }), json({ items: [], totalCount: 0 }))
    expect(await store.approve(3, null)).toBe(true)
    expect(mock).toHaveBeenCalledTimes(2)

    const second = stubFetch(new Response(null, { status: 204 }), json({ items: [], totalCount: 0 }))
    expect(await store.reject(3, 'no')).toBe(true)
    expect(second).toHaveBeenCalledTimes(2)
  })

  it('keeps the sentence of a refused decision and does not reload', async () => {
    const store = useServiceChangeRequestsStore()
    const mock = stubFetch(problem('The upgrade invoice is not paid.', 402))

    expect(await store.approve(3, null)).toBe(false)
    expect(store.actionError).toBe('The upgrade invoice is not paid.')
    expect(store.error).toBeNull()
    expect(store.loading).toBe(false)
    expect(mock).toHaveBeenCalledTimes(1)
  })

  it('keeps a refused decision apart from a failed reload', async () => {
    const store = useServiceChangeRequestsStore()
    stubFetch(problem('Down.', 500))
    await store.fetchAll()
    expect(store.error).toBe('Down.')

    stubFetch(problem('Already resolved.', 409))
    expect(await store.reject(3, null)).toBe(false)
    expect(store.actionError).toBe('Already resolved.')
    expect(store.error).toBe('Down.')

    stubFetch(new Response(null, { status: 204 }), problem('Still down.', 500))
    expect(await store.approve(3, null)).toBe(true)
    expect(store.actionError).toBeNull()
    expect(store.error).toBe('Still down.')
  })
})
