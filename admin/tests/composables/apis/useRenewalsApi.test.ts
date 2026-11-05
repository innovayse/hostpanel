/**
 * Tests for the renewals API composable: each endpoint through the real `useApi` transport with `fetch`
 * stubbed, checking the URL, the verb and the body.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useRenewalsApi } from '@/composables/apis/useRenewalsApi'
import { RenewalWindow } from '@/types/renewal'
import { callOf, json, stubFetch } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useRenewalsApi', () => {
  it('listServices sends the window and the paging', async () => {
    const mock = stubFetch(json({ items: [], pageNumber: 2, pageSize: 10, totalCount: 0, totalPages: 0 }))

    await useRenewalsApi().listServices(RenewalWindow.Next60Days, 2, 10)

    expect(callOf(mock).url).toBe('/api/services/renewals/upcoming?window=Next60Days&page=2&pageSize=10')
  })

  it('listDomains sends the window and the paging', async () => {
    const mock = stubFetch(json({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 }))

    await useRenewalsApi().listDomains(RenewalWindow.Next90Days, 1, 20)

    expect(callOf(mock).url).toBe('/api/domains/renewals/upcoming?window=Next90Days&page=1&pageSize=20')
  })

  it('setServiceAutoRenew puts the value', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useRenewalsApi().setServiceAutoRenew(4, false)

    expect(callOf(mock)).toMatchObject({ url: '/api/services/4/auto-renew', method: 'PUT', body: { value: false } })
  })

  it('setDomainAutoRenew puts the value', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useRenewalsApi().setDomainAutoRenew(7, true)

    expect(callOf(mock)).toMatchObject({ url: '/api/domains/7/auto-renew', method: 'PUT', body: { value: true } })
  })

  it('generateInvoices posts for the client and returns the count', async () => {
    const mock = stubFetch(json(2))

    const raised = await useRenewalsApi().generateInvoices(9)

    expect(raised).toBe(2)
    expect(callOf(mock)).toMatchObject({ url: '/api/billing/client/9/renewal-invoices', method: 'POST' })
  })

  it('listClaims reads the claimed lines', async () => {
    const mock = stubFetch(json([]))

    await useRenewalsApi().listClaims()

    expect(callOf(mock).url).toBe('/api/billing/renewal-claims')
  })

  it('releaseClaim posts to the line', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useRenewalsApi().releaseClaim(5, 11)

    expect(callOf(mock)).toMatchObject({ url: '/api/billing/5/items/11/release-renewal-claim', method: 'POST' })
  })
})
