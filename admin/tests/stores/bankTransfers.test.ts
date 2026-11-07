/**
 * Tests for the bank transfers store: a load fills the page and the total, a decision reports whether the API accepted it,
 * and a refusal or a missing answer lands in `error` as a finished sentence while the earlier page stays.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useBankTransfersStore } from '@/stores/bankTransfers'
import { json, problem, stubFetch, stubOffline, callOf } from '../helpers/fetch'

/** Sentence the client writes when nothing answered. */
const NO_ANSWER = 'Could not reach the server. Check your connection and try again.'

/** One waiting transfer as the list endpoint returns it. */
const transfer = {
  invoiceId: 5,
  reference: 'INV-5',
  clientId: 9,
  clientName: 'Ada Lovelace',
  total: 100,
  amountDue: 100,
  currency: 'USD',
  dueDate: '2027-01-15T00:00:00Z',
  requestedAt: '2027-01-01T10:00:00Z',
}

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('bankTransfersStore', () => {
  it('fetchPage fills the transfers and the total and clears loading', async () => {
    const mock = stubFetch(json({ items: [transfer], pageNumber: 1, pageSize: 20, totalCount: 3, totalPages: 1 }))
    const store = useBankTransfersStore()

    await store.fetchPage(1, 20)

    expect(store.transfers).toEqual([transfer])
    expect(store.totalCount).toBe(3)
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
    expect(callOf(mock).url).toBe('/api/billing/bank-transfers/pending?page=1&pageSize=20')
  })

  it('fetchPage shows the API sentence on refusal and keeps the earlier page', async () => {
    const store = useBankTransfersStore()
    stubFetch(json({ items: [transfer], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 }))
    await store.fetchPage(1, 20)

    stubFetch(problem('The transfers are not available.', 403))
    await store.fetchPage(1, 20)

    expect(store.error).toBe('The transfers are not available.')
    expect(store.transfers).toEqual([transfer])
    expect(store.loading).toBe(false)
  })

  it('fetchPage shows the no-answer sentence when the network is down', async () => {
    stubOffline()
    const store = useBankTransfersStore()

    await store.fetchPage(1, 20)

    expect(store.error).toBe(NO_ANSWER)
  })

  it('confirm reports true when the API accepts it', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const store = useBankTransfersStore()

    const confirmed = await store.confirm(5)

    expect(confirmed).toBe(true)
    expect(store.error).toBeNull()
    expect(callOf(mock).url).toBe('/api/billing/5/bank-transfer/confirm')
  })

  it('confirm reports false with the API sentence when nothing is waiting any more', async () => {
    stubFetch(problem('No bank transfer is waiting on this invoice, so there is nothing to confirm or reject.', 409))
    const store = useBankTransfersStore()

    const confirmed = await store.confirm(5)

    expect(confirmed).toBe(false)
    expect(store.error).toBe('No bank transfer is waiting on this invoice, so there is nothing to confirm or reject.')
  })

  it('reject sends the note and reports the outcome', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const store = useBankTransfersStore()

    const rejected = await store.reject(5, 'Not received')

    expect(rejected).toBe(true)
    expect(JSON.parse(String(callOf(mock).init?.body))).toEqual({ note: 'Not received' })
  })

  it('reject reports false when the API refuses', async () => {
    stubFetch(problem('A reason is required.', 400))
    const store = useBankTransfersStore()

    expect(await store.reject(5, '')).toBe(false)
    expect(store.error).toBe('A reason is required.')
  })
})
