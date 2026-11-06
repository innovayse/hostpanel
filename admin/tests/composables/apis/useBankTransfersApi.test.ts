/**
 * Tests for the bank transfers API composable: each endpoint through the real `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useBankTransfersApi } from '@/composables/apis/useBankTransfersApi'
import { json, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useBankTransfersApi', () => {
  it('listPending sends the paging', async () => {
    const mock = stubFetch(json({ items: [], pageNumber: 2, pageSize: 10, totalCount: 0, totalPages: 0 }))

    await useBankTransfersApi().listPending(2, 10)

    expect(callOf(mock).url).toBe('/api/billing/bank-transfers/pending?page=2&pageSize=10')
  })

  it('confirm posts to the invoice confirm route with no body', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useBankTransfersApi().confirm(7)

    const call = callOf(mock)
    expect(call.url).toBe('/api/billing/7/bank-transfer/confirm')
    expect(call.init?.method).toBe('POST')
    expect(call.init?.body).toBeUndefined()
  })

  it('reject posts the note to the invoice reject route', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useBankTransfersApi().reject(7, 'Amount too low')

    const call = callOf(mock)
    expect(call.url).toBe('/api/billing/7/bank-transfer/reject')
    expect(call.init?.method).toBe('POST')
    expect(JSON.parse(String(call.init?.body))).toEqual({ note: 'Amount too low' })
  })
})
