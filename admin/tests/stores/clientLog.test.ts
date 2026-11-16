/**
 * Tests for the client activity log store: the page, the search and the period reach the request, a refusal lands in
 * the store.
 */

import { describe, expect, it } from 'vitest'
import { useClientLogStore } from '@/stores/clientLog'
import { json, problem, stubFetch, callOf } from '../helpers/fetch'
import { installStoreHooks } from '../helpers/storeHooks'

describe('clientLogStore', () => {
  installStoreHooks()

  it('fetchLogs sends the page and only the search and the period that are set', async () => {
    const mock = stubFetch(json({ items: [{ id: 1 }], totalCount: 1 }))
    const store = useClientLogStore()

    await store.fetchLogs(7, 2, { search: '  login  ', from: '2026-03-01', to: '' })
    expect(store.logs).toEqual([{ id: 1 }])
    expect(store.totalCount).toBe(1)
    expect(callOf(mock).url).toBe('/api/clients/7/activity-logs?page=2&pageSize=25&search=login&from=2026-03-01')
  })

  it('fetchLogs sends the page alone when no filter is set', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))
    const store = useClientLogStore()

    await store.fetchLogs(7, 1)
    expect(callOf(mock).url).toBe('/api/clients/7/activity-logs?page=1&pageSize=25')
  })

  it('fetchLogs shows the API sentence on refusal and reset clears the state', async () => {
    const store = useClientLogStore()
    stubFetch(problem('Log is locked.', 403))
    await store.fetchLogs(7, 1)
    expect(store.error).toBe('Log is locked.')

    store.reset()
    expect(store.error).toBeNull()
    expect(store.logs).toEqual([])
  })
})
