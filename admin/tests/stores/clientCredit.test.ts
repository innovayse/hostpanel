/**
 * Tests for the client credit store: a load fills the credit, an adjustment reports whether the API accepted it and reloads the
 * first page, and a refusal lands in `error` as a finished sentence (and in `fieldErrors` per field) while the earlier credit stays.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useClientCreditStore } from '@/stores/clientCredit'
import { CreditAdjustment, CreditMovement } from '@/types/creditmovement'
import { json, problem, stubFetch, stubOffline, callOf } from '../helpers/fetch'

/** Sentence the client writes when nothing answered. */
const NO_ANSWER = 'Could not reach the server. Check your connection and try again.'

/**
 * Builds a credit answer.
 *
 * @param balance - The balance.
 * @param lines - How many history lines the page holds.
 * @returns The body the credit endpoint answers with.
 */
const credit = (balance: number, lines = 1) => ({
  clientId: 9,
  balance,
  currency: 'USD',
  entries: {
    items: Array.from({ length: lines }, (_, i) => ({
      id: i + 1,
      date: '2026-10-01T09:00:00Z',
      movement: CreditMovement.AdminAdd,
      change: balance,
      balanceAfter: balance,
      invoiceId: null,
      description: 'Promo',
    })),
    pageNumber: 1,
    pageSize: 25,
    totalCount: lines,
    totalPages: 1,
  },
})

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('clientCreditStore', () => {
  it('fetchCredit fills the credit and clears loading', async () => {
    const mock = stubFetch(json(credit(40)))
    const store = useClientCreditStore()

    const ok = await store.fetchCredit(9)

    expect(ok).toBe(true)
    expect(store.credit?.balance).toBe(40)
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
    expect(callOf(mock).url).toBe('/api/clients/9/credit?page=1&pageSize=25')
  })

  it('fetchCredit shows the API sentence on refusal and keeps the earlier credit', async () => {
    const store = useClientCreditStore()
    stubFetch(json(credit(40)))
    await store.fetchCredit(9)

    stubFetch(problem('The client was not found.', 404))
    const ok = await store.fetchCredit(9)

    expect(ok).toBe(false)
    expect(store.error).toBe('The client was not found.')
    expect(store.credit?.balance).toBe(40)
    expect(store.loading).toBe(false)
  })

  it('adjust posts the adjustment, reloads the first page and reports success', async () => {
    const store = useClientCreditStore()
    const mock = stubFetch(new Response(null, { status: 204 }), json(credit(65, 2)))

    const ok = await store.adjust(9, CreditAdjustment.Add, 25, 'Goodwill')

    expect(ok).toBe(true)
    const posted = callOf(mock, 0)
    expect(posted.url).toBe('/api/clients/9/credit/adjustments')
    expect(JSON.parse(String(posted.init?.body))).toEqual({ direction: 'Add', amount: 25, reason: 'Goodwill' })
    expect(callOf(mock, 1).url).toBe('/api/clients/9/credit?page=1&pageSize=25')
    expect(store.credit?.balance).toBe(65)
  })

  it('adjust reports a refusal with its sentence and does not reload', async () => {
    const store = useClientCreditStore()
    const mock = stubFetch(problem('There is not enough credit.', 400))

    const ok = await store.adjust(9, CreditAdjustment.Deduct, 500, 'Too much')

    expect(ok).toBe(false)
    expect(store.error).toBe('There is not enough credit.')
    expect(mock).toHaveBeenCalledTimes(1)
  })

  it('adjust says nothing answered when the server cannot be reached', async () => {
    const store = useClientCreditStore()
    stubOffline()

    const ok = await store.adjust(9, CreditAdjustment.Add, 5, 'x')

    expect(ok).toBe(false)
    expect(store.error).toBe(NO_ANSWER)
  })
})
