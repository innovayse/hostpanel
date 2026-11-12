/**
 * Tests for the fraud log store: a read fills the page and the total, the filters reach the API as
 * query values (empty ones are left out), and a refusal or a missing answer lands in `error`.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useFraudLogStore } from '@/stores/fraudLog'
import { FraudCheckKind, FraudDecision, FraudReasonCode } from '@/types/fraud'
import type { FraudLogEntry } from '@/types/fraud'
import { callOf, json, problem, stubFetch, stubOffline } from '../helpers/fetch'

/** Sentence the client writes when nothing answered. */
const NO_ANSWER = 'Could not reach the server. Check your connection and try again.'

/** One entry as the list endpoint returns it. */
const entry: FraudLogEntry = {
  id: 3,
  kind: FraudCheckKind.Order,
  decision: FraudDecision.Review,
  clientId: 12,
  orderId: 40,
  ipAddress: '203.0.113.7',
  email: 'buyer@example.test',
  score: 62,
  reasons: [FraudReasonCode.OrderVelocity],
  createdAt: '2026-11-01T10:00:00Z',
}

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('fraudLogStore', () => {
  it('fetchPage fills the entries and the total and clears loading', async () => {
    const mock = stubFetch(json({ items: [entry], pageNumber: 1, pageSize: 20, totalCount: 9, totalPages: 1 }))
    const store = useFraudLogStore()

    await store.fetchPage({ decision: '', reason: '' }, 1, 20)

    expect(store.entries).toEqual([entry])
    expect(store.totalCount).toBe(9)
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
    expect(callOf(mock).url).toBe('/api/admin/fraud/log?page=1&pageSize=20')
  })

  it('fetchPage sends the decision and the reason', async () => {
    const mock = stubFetch(json({ items: [], pageNumber: 2, pageSize: 10, totalCount: 0, totalPages: 0 }))
    const store = useFraudLogStore()

    await store.fetchPage({ decision: FraudDecision.Rejected, reason: FraudReasonCode.BlockedIp }, 2, 10)

    const query = new URL(callOf(mock).url, 'http://localhost').searchParams
    expect(query.get('decision')).toBe('Rejected')
    expect(query.get('reason')).toBe('BlockedIp')
  })

  it('fetchPage shows the API sentence on refusal and keeps the earlier page', async () => {
    const store = useFraudLogStore()
    stubFetch(json({ items: [entry], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 }))
    await store.fetchPage({ decision: '', reason: '' }, 1, 20)

    stubFetch(problem('The log is not available.', 403))
    await store.fetchPage({ decision: '', reason: '' }, 1, 20)

    expect(store.error).toBe('The log is not available.')
    expect(store.entries).toEqual([entry])
    expect(store.loading).toBe(false)
  })

  it('fetchPage shows the no-answer sentence when the network is down', async () => {
    stubOffline()
    const store = useFraudLogStore()

    await store.fetchPage({ decision: '', reason: '' }, 1, 20)

    expect(store.error).toBe(NO_ANSWER)
  })
})
