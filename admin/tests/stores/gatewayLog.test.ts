/**
 * Tests for the gateway log store: a load fills the page and the total, the filters and paging
 * reach the API as query values (empty ones are left out), and a refusal or a missing answer
 * lands in `error` as a finished sentence.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useGatewayLogStore } from '@/stores/gatewayLog'
import { GatewayLogResult } from '@/types/gatewaylog'
import type { GatewayLogFilters } from '@/types/gatewaylog'
import { json, problem, stubFetch, stubOffline, callOf } from '../helpers/fetch'

/** Sentence the client writes when nothing answered. */
const NO_ANSWER = 'Could not reach the server. Check your connection and try again.'

/** Filters with nothing set. */
const noFilters: GatewayLogFilters = { from: '', to: '', gateway: '', debugData: '', result: '' }

/** One entry as the list endpoint returns it. */
const entry = {
  id: 'tx-12',
  dateTime: '2027-01-01T10:00:00Z',
  gateway: 'Example Gateway',
  debugData: 'Invoice 5',
  result: GatewayLogResult.Payment,
  invoiceId: 5,
}

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('gatewayLogStore', () => {
  it('fetchPage fills the entries and the total and clears loading', async () => {
    const mock = stubFetch(json({ items: [entry], pageNumber: 1, pageSize: 25, totalCount: 7, totalPages: 1 }))
    const store = useGatewayLogStore()

    await store.fetchPage(noFilters, 1, 25)

    expect(store.entries).toEqual([entry])
    expect(store.totalCount).toBe(7)
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
    expect(callOf(mock).url).toBe('/api/gateway-logs?page=1&pageSize=25')
  })

  it('fetchPage sends the paging and the filters, and leaves empty ones out', async () => {
    const mock = stubFetch(json({ items: [], pageNumber: 3, pageSize: 10, totalCount: 0, totalPages: 0 }))
    const store = useGatewayLogStore()

    await store.fetchPage(
      { from: '2027-01-01', to: '2027-01-31', gateway: 'Example Gateway', debugData: '', result: GatewayLogResult.Refund },
      3,
      10,
    )

    const query = new URL(callOf(mock).url, 'http://localhost').searchParams
    expect(query.get('page')).toBe('3')
    expect(query.get('pageSize')).toBe('10')
    expect(query.get('from')).toBe('2027-01-01T00:00:00Z')
    expect(query.get('to')).toBe('2027-01-31T23:59:59.999Z')
    expect(query.get('gateway')).toBe('Example Gateway')
    expect(query.get('result')).toBe('Refund')
    expect(query.has('debugData')).toBe(false)
  })

  it('fetchPage shows the API sentence on refusal and keeps the earlier page', async () => {
    const store = useGatewayLogStore()
    stubFetch(json({ items: [entry], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 }))
    await store.fetchPage(noFilters, 1, 25)

    stubFetch(problem('The log is not available.', 403))
    await store.fetchPage(noFilters, 1, 25)

    expect(store.error).toBe('The log is not available.')
    expect(store.entries).toEqual([entry])
    expect(store.loading).toBe(false)
  })

  it('fetchPage shows the no-answer sentence when the network is down', async () => {
    stubOffline()
    const store = useGatewayLogStore()

    await store.fetchPage(noFilters, 1, 25)

    expect(store.error).toBe(NO_ANSWER)
    expect(store.loading).toBe(false)
  })
})
