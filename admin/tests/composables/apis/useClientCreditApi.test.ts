/**
 * Tests for the client credit API composable: each endpoint through the real `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useClientCreditApi } from '@/composables/apis/useClientCreditApi'
import { CreditAdjustment } from '@/types/creditmovement'
import { json, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useClientCreditApi', () => {
  it('fetchCredit reads the client credit with the paging', async () => {
    const mock = stubFetch(json({ clientId: 9, balance: 5, currency: 'USD', entries: { items: [], pageNumber: 2, pageSize: 25, totalCount: 0, totalPages: 0 } }))

    const credit = await useClientCreditApi().fetchCredit(9, 2, 25)

    expect(callOf(mock).url).toBe('/api/clients/9/credit?page=2&pageSize=25')
    expect(credit.balance).toBe(5)
  })

  it('adjust posts the direction, the amount and the reason to the client adjustments route', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useClientCreditApi().adjust(9, { direction: CreditAdjustment.Deduct, amount: 4.5, reason: 'Wrong client' })

    const call = callOf(mock)
    expect(call.url).toBe('/api/clients/9/credit/adjustments')
    expect(call.init?.method).toBe('POST')
    expect(JSON.parse(String(call.init?.body))).toEqual({ direction: 'Deduct', amount: 4.5, reason: 'Wrong client' })
  })
})
