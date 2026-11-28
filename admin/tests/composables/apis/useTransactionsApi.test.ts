/**
 * Tests for the transactions API composable: the billing ledger and the per-client endpoints
 * through the real `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useTransactionsApi } from '@/composables/apis/useTransactionsApi'
import { ApiError } from '@/composables/useApi'
import { json, problem, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useTransactionsApi', () => {
  it('list and listByClient page their endpoints', async () => {
    const mock = stubFetch(json({ transactions: { items: [], totalCount: 0 } }))
    const api = useTransactionsApi()

    await api.list(1, 25)
    await api.listByClient(7, 2, 10)
    expect(callOf(mock, 0).url).toBe('/api/transactions?page=1&pageSize=25')
    expect(callOf(mock, 1).url).toBe('/api/transactions/client/7?page=2&pageSize=10')
  })

  it('list and listByClient send the search and the filters that are set and leave the others off', async () => {
    const mock = stubFetch(json({ transactions: { items: [], totalCount: 0 } }))
    const api = useTransactionsApi()

    await api.list(1, 25, {
      search: 'renewal',
      clientId: 4,
      direction: 'In',
      paymentMethod: 'Card',
      from: '2026-03-01',
      to: '2026-03-31',
      amount: 12.5,
    })
    await api.list(1, 25, { search: '', direction: undefined })
    await api.listByClient(7, 2, 10, { direction: 'Out', amount: 40 })
    expect(callOf(mock, 0).url).toBe(
      '/api/transactions?page=1&pageSize=25&search=renewal&clientId=4&direction=In&paymentMethod=Card&from=2026-03-01&to=2026-03-31&amount=12.5',
    )
    expect(callOf(mock, 1).url).toBe('/api/transactions?page=1&pageSize=25')
    expect(callOf(mock, 2).url).toBe('/api/transactions/client/7?page=2&pageSize=10&direction=Out&amount=40')
  })

  it('get reads one transaction', async () => {
    const mock = stubFetch(json({ id: 2 }))

    expect(await useTransactionsApi().get(2)).toEqual({ id: 2 })
    expect(callOf(mock).url).toBe('/api/transactions/2')
  })

  it('create, update and remove use POST, PUT and DELETE', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useTransactionsApi()
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

    await api.create({ ...fields, clientId: 1, addToCredit: true })
    await api.update(2, fields)
    await api.remove(2)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/transactions', method: 'POST', body: { clientId: 1, addToCredit: true } })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/transactions/2', method: 'PUT', body: fields })
    expect(callOf(mock, 2)).toMatchObject({ url: '/api/transactions/2', method: 'DELETE' })
  })

  it('rejects with an ApiError carrying the refusal body', async () => {
    stubFetch(problem('Transaction is settled.', 409))

    await expect(useTransactionsApi().remove(2)).rejects.toBeInstanceOf(ApiError)
  })
})
