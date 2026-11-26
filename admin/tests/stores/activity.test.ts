/**
 * Tests for the activity store: events on success, the API sentence on refusal, the offline fallback, the
 * initial loading flag the feed lists draw their placeholders from, and the read state, which lives on the server.
 */

import { describe, expect, it } from 'vitest'
import { useActivityStore } from '@/stores/activity'
import { activityEvent, overdueInvoice, ticketOpened } from '../helpers/activityEvents'
import { callsOf, json, problem, stubFetch, stubOffline } from '../helpers/fetch'
import { installStoreHooks } from '../helpers/storeHooks'

/** An answer with no body, as the read routes give. */
const noContent = (): Response => new Response(null, { status: 204 })

describe('activityStore', () => {
  installStoreHooks()

  it('fetchActivity stores the events', async () => {
    const mock = stubFetch(json([activityEvent()]))
    const store = useActivityStore()

    await store.fetchActivity(3)
    expect(store.events).toEqual([activityEvent()])
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
    stubFetch(json([activityEvent()]))
    const store = useActivityStore()

    expect(store.initialLoading).toBe(false)
    const first = store.fetchActivity()
    expect(store.initialLoading).toBe(true)
    await first
    expect(store.initialLoading).toBe(false)

    stubFetch(json([activityEvent({ entityId: 8 })]))
    const refresh = store.fetchActivity()
    expect(store.loading).toBe(true)
    expect(store.initialLoading).toBe(false)
    await refresh
  })

  it('counts the events the server says are unread', async () => {
    stubFetch(json([overdueInvoice, { ...ticketOpened, isRead: true }]))
    const store = useActivityStore()

    await store.fetchActivity()

    expect(store.unreadCount).toBe(1)
    expect(store.unreadEvents).toEqual([overdueInvoice])
  })

  it('markRead posts the key of the event and reads the feed again with the same window', async () => {
    const mock = stubFetch(
      json([overdueInvoice, ticketOpened]),
      noContent(),
      json([{ ...overdueInvoice, isRead: true }, ticketOpened]),
    )
    const store = useActivityStore()
    await store.fetchActivity(20)

    await store.markRead(overdueInvoice)

    const calls = callsOf(mock)
    expect(calls[1]?.method).toBe('POST')
    expect(calls[1]?.url).toContain('/admin/dashboard/activity/read')
    expect(calls[1]?.body).toEqual({ eventKey: overdueInvoice.key })
    expect(calls[2]?.url).toContain('limit=20')
    expect(store.unreadEvents).toEqual([ticketOpened])
  })

  it('markAllRead posts to the read-all route and reads the feed again', async () => {
    const mock = stubFetch(
      json([overdueInvoice, ticketOpened]),
      noContent(),
      json([{ ...overdueInvoice, isRead: true }, { ...ticketOpened, isRead: true }]),
    )
    const store = useActivityStore()
    await store.fetchActivity()

    await store.markAllRead()

    expect(callsOf(mock)[1]?.url).toContain('/admin/dashboard/activity/read-all')
    expect(store.unreadCount).toBe(0)
  })

  it('shows the API sentence and keeps the events unread when marking is refused', async () => {
    stubFetch(json([overdueInvoice]), problem('Not allowed.', 403))
    const store = useActivityStore()
    await store.fetchActivity()

    await store.markRead(overdueInvoice)

    expect(store.error).toBe('Not allowed.')
    expect(store.unreadCount).toBe(1)
  })
})
