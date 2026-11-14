/**
 * Tests for the billing transactions store and the billable items store: loads fill the
 * state, writes reload the list, and refusals land in the store instead of a view.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useTransactionsStore } from '@/stores/billingTransactions'
import { json, problem, stubFetch, stubOffline, callOf } from '../helpers/fetch'

/** Sentence the client writes when nothing answered. */
const NO_ANSWER = 'Could not reach the server. Check your connection and try again.'

/** The ledger answer the list endpoint sends. */
const ledger = {
  transactions: { items: [{ id: 1 }], pageNumber: 1, pageSize: 25, totalCount: 4, totalPages: 1 },
  totalIn: 10,
  totalOut: 2,
  totalFees: 1,
  balance: 7,
  currency: 'EUR',
}

/** Fields a transaction body carries. */
const fields = {
  date: 'd',
  description: 'x',
  transactionId: 't',
  invoiceId: null,
  paymentMethod: 'Manual',
  amountIn: 1,
  amountOut: 0,
  fees: 0,
}

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('billingTransactionsStore', () => {
  it('fetchAll fills the page and the summary and sends the paging', async () => {
    const mock = stubFetch(json(ledger))
    const store = useTransactionsStore()

    await store.fetchAll(2, {}, 10)
    expect(store.transactions).toEqual([{ id: 1 }])
    expect(store.totalCount).toBe(4)
    expect(store.summary).toEqual({ totalIn: 10, totalOut: 2, totalFees: 1, balance: 7, currency: 'EUR' })
    expect(callOf(mock).url).toBe('/api/transactions?page=2&pageSize=10')
  })

  it('fetchAll sends the search and the filters the screen holds, and the summary is the one the API answers for them', async () => {
    const mock = stubFetch(json({ ...ledger, totalIn: 3 }))
    const store = useTransactionsStore()

    await store.fetchAll(1, { search: 'renewal', clientId: 4, direction: 'In', paymentMethod: 'Credit/Debit Card', from: '2026-03-01', amount: 3 })
    expect(callOf(mock).url).toBe(
      '/api/transactions?page=1&pageSize=25&search=renewal&clientId=4&direction=In&paymentMethod=Credit%2FDebit+Card&from=2026-03-01&amount=3',
    )
    expect(store.summary.totalIn).toBe(3)
  })

  it('fetchAll shows the API sentence on refusal and the no-answer sentence offline', async () => {
    const store = useTransactionsStore()
    stubFetch(problem('Ledger is locked.', 403))
    await store.fetchAll()
    expect(store.error).toBe('Ledger is locked.')

    stubOffline()
    await store.fetchAll()
    expect(store.error).toBe(NO_ANSWER)
  })

  it('fetchById sets the open transaction', async () => {
    stubFetch(json({ id: 2 }))
    const store = useTransactionsStore()

    await store.fetchById(2)
    expect(store.currentTransaction).toEqual({ id: 2 })
  })

  it('create leaves the reload to the screen, and a refusal returns false with field errors', async () => {
    const store = useTransactionsStore()
    const mock = stubFetch(new Response(null, { status: 204 }))
    expect(await store.create({ ...fields, clientId: 1, addToCredit: false })).toBe(true)
    expect(mock).toHaveBeenCalledTimes(1)

    stubFetch(json({ type: 'VALIDATION_ERROR', title: 'Invalid', status: 400, detail: 'Fix the form.', errors: { AmountIn: ['Amount is required.'] } }, 400))
    expect(await store.create({ ...fields, clientId: 1, addToCredit: false })).toBe(false)
    expect(store.fieldErrors).toEqual({ amountIn: 'Amount is required.' })
  })

  it('update returns the outcome without reloading', async () => {
    const store = useTransactionsStore()
    const mock = stubFetch(new Response(null, { status: 204 }))
    expect(await store.update(2, fields)).toBe(true)
    expect(mock).toHaveBeenCalledTimes(1)

    stubFetch(problem('Transaction is settled.', 409))
    expect(await store.update(2, fields)).toBe(false)
    expect(store.error).toBe('Transaction is settled.')
  })

  it('remove leaves the reload to the screen and reports a refusal in actionError', async () => {
    const store = useTransactionsStore()
    const mock = stubFetch(new Response(null, { status: 204 }))
    expect(await store.remove(2)).toBe(true)
    expect(mock).toHaveBeenCalledTimes(1)

    const refused = stubFetch(problem('Transaction is settled.', 409))
    expect(await store.remove(2)).toBe(false)
    expect(refused).toHaveBeenCalledTimes(1)
    expect(store.actionError).toBe('Transaction is settled.')
    expect(store.error).toBeNull()
  })
})
