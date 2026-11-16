/**
 * Tests for the client transactions store: the page with its summary, create and remove.
 */

import { describe, expect, it } from 'vitest'
import { useClientTransactionsStore } from '@/stores/clientTransactions'
import { json, problem, stubFetch, callOf } from '../helpers/fetch'
import { installStoreHooks } from '../helpers/storeHooks'

describe('clientTransactionsStore', () => {
  installStoreHooks()

  it('fetchByClient fills the page and the summary', async () => {
    const mock = stubFetch(json({
      transactions: { items: [{ id: 1 }], totalCount: 2 },
      totalIn: 5,
      totalOut: 1,
      totalFees: 0,
      balance: 4,
    }))
    const store = useClientTransactionsStore()

    await store.fetchByClient(7, 2, {}, 10)
    expect(store.transactions).toEqual([{ id: 1 }])
    expect(store.totalCount).toBe(2)
    expect(store.summary).toEqual({ totalIn: 5, totalOut: 1, totalFees: 0, balance: 4 })
    expect(callOf(mock).url).toBe('/api/transactions/client/7?page=2&pageSize=10')
  })

  it('fetchByClient sends the search and the filters the screen holds', async () => {
    const mock = stubFetch(json({ transactions: { items: [], totalCount: 0 }, totalIn: 0, totalOut: 0, totalFees: 0, balance: 0 }))
    const store = useClientTransactionsStore()

    await store.fetchByClient(7, 1, { search: 'refund', direction: 'Out', to: '2026-03-31' })
    expect(callOf(mock).url).toBe('/api/transactions/client/7?page=1&pageSize=25&search=refund&direction=Out&to=2026-03-31')
  })

  it('fetchByClient shows the API sentence on refusal', async () => {
    stubFetch(problem('Client not found.', 404))
    const store = useClientTransactionsStore()

    await store.fetchByClient(7)
    expect(store.error).toBe('Client not found.')
  })

  it('create and remove report the outcome', async () => {
    const store = useClientTransactionsStore()
    const body = {
      clientId: 7,
      date: 'd',
      description: 'x',
      transactionId: 't',
      invoiceId: null,
      paymentMethod: 'Manual',
      amountIn: 1,
      amountOut: 0,
      fees: 0,
      addedToCredit: false,
    }
    stubFetch(new Response(null, { status: 204 }))
    expect(await store.create(body)).toBe(true)
    expect(await store.remove(2)).toBe(true)

    stubFetch(problem('Transaction is settled.', 409))
    expect(await store.create(body)).toBe(false)
    expect(await store.remove(2)).toBe(false)
    expect(store.error).toBe('Transaction is settled.')
  })
})
