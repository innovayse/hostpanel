/**
 * Tests for the activity store: events on success, the API sentence on refusal, the offline fallback, and the
 * initial loading flag the feed lists draw their placeholders from.
 */

import { describe, expect, it } from 'vitest'
import { useActivityStore } from '@/stores/activity'
import { json, problem, stubFetch, stubOffline } from '../helpers/fetch'
import { installStoreHooks } from '../helpers/storeHooks'

describe('activityStore', () => {
  installStoreHooks()

  it('fetchActivity stores the events', async () => {
    const mock = stubFetch(json([{ id: 1 }]))
    const store = useActivityStore()

    await store.fetchActivity(3)
    expect(store.events).toEqual([{ id: 1 }])
    expect(String(mock.mock.calls[0]?.[0])).toContain('limit=3')
    expect(store.loading).toBe(false)
  })

  it('fetchActivity shows the API sentence on refusal', async () => {
    stubFetch(problem('No access.', 403))
    const store = useActivityStore()

    await store.fetchActivity()
    expect(store.events).toEqual([])
    expect(store.error).toBe('No access.')
  })

  it('fetchActivity falls back when nothing answered', async () => {
    stubOffline()
    const store = useActivityStore()

    await store.fetchActivity()
    expect(store.error).toBe('Could not reach the server. Check your connection and try again.')
  })

  it('initialLoading is true only while a read is pending and nothing is held yet', async () => {
    stubFetch(json([{ id: 1 }]))
    const store = useActivityStore()

    expect(store.initialLoading).toBe(false)
    const first = store.fetchActivity()
    expect(store.initialLoading).toBe(true)
    await first
    expect(store.initialLoading).toBe(false)

    stubFetch(json([{ id: 2 }]))
    const refresh = store.fetchActivity()
    expect(store.loading).toBe(true)
    expect(store.initialLoading).toBe(false)
    await refresh
  })
})
