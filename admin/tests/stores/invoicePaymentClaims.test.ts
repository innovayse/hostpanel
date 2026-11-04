/**
 * Tests for the invoice payment claims store: a load fills the page and the total, the status and
 * paging reach the API as query values, and a refusal or a missing answer lands in `error` as a
 * finished sentence while the earlier page stays.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useInvoicePaymentClaimsStore } from '@/stores/invoicePaymentClaims'
import { InvoicePaymentClaimStatus } from '@/types/invoicepaymentclaim'
import { json, problem, stubFetch, stubOffline, callOf } from '../helpers/fetch'

/** Sentence the client writes when nothing answered. */
const NO_ANSWER = 'Could not reach the server. Check your connection and try again.'

/** One held claim as the list endpoint returns it. */
const claim = {
  id: 3,
  invoiceId: 5,
  clientId: 9,
  clientName: 'Ada Lovelace',
  gatewayModuleId: 'bank-gateway',
  amount: 12.5,
  currency: 'USD',
  status: InvoicePaymentClaimStatus.Held,
  transactionId: 'tx-1',
  createdAt: '2027-01-01T10:00:00Z',
  resolvedAt: '2027-01-01T10:01:00Z',
}

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('invoicePaymentClaimsStore', () => {
  it('fetchPage fills the claims and the total and clears loading', async () => {
    const mock = stubFetch(json({ items: [claim], pageNumber: 1, pageSize: 25, totalCount: 4, totalPages: 1 }))
    const store = useInvoicePaymentClaimsStore()

    await store.fetchPage(InvoicePaymentClaimStatus.Held, 1, 25)

    expect(store.claims).toEqual([claim])
    expect(store.totalCount).toBe(4)
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
    expect(callOf(mock).url).toBe('/api/billing/payment-claims?page=1&pageSize=25&status=Held')
  })

  it('fetchPage shows the API sentence on refusal and keeps the earlier page', async () => {
    const store = useInvoicePaymentClaimsStore()
    stubFetch(json({ items: [claim], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 }))
    await store.fetchPage(InvoicePaymentClaimStatus.Held, 1, 25)

    stubFetch(problem('The claims are not available.', 403))
    await store.fetchPage(InvoicePaymentClaimStatus.Held, 1, 25)

    expect(store.error).toBe('The claims are not available.')
    expect(store.claims).toEqual([claim])
    expect(store.loading).toBe(false)
  })

  it('fetchPage shows the no-answer sentence when the network is down', async () => {
    stubOffline()
    const store = useInvoicePaymentClaimsStore()

    await store.fetchPage(InvoicePaymentClaimStatus.Held, 1, 25)

    expect(store.error).toBe(NO_ANSWER)
    expect(store.loading).toBe(false)
  })
})
