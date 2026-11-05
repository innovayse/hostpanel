/**
 * Tests for the invoice payment claims API composable: the list endpoint through the real
 * `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useInvoicePaymentClaimsApi } from '@/composables/apis/useInvoicePaymentClaimsApi'
import { InvoicePaymentClaimStatus } from '@/types/invoicepaymentclaim'
import { json, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useInvoicePaymentClaimsApi', () => {
  it('list sends the status and the paging', async () => {
    const mock = stubFetch(json({ items: [], pageNumber: 2, pageSize: 10, totalCount: 0, totalPages: 0 }))

    await useInvoicePaymentClaimsApi().list(InvoicePaymentClaimStatus.Held, 2, 10)

    expect(callOf(mock).url).toBe('/api/billing/payment-claims?page=2&pageSize=10&status=Held')
  })

  it('list leaves an undefined status out', async () => {
    const mock = stubFetch(json({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 }))

    await useInvoicePaymentClaimsApi().list(undefined, 1, 20)

    expect(callOf(mock).url).toBe('/api/billing/payment-claims?page=1&pageSize=20')
  })
})
