/**
 * Tests for the quotes API composable: paths, verbs, query strings and bodies through the
 * real `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useQuotesApi } from '@/composables/apis/useQuotesApi'
import { ApiError } from '@/composables/useApi'
import { json, problem, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useQuotesApi', () => {
  it('list and listByClient page their endpoints', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))
    const api = useQuotesApi()

    await api.list(2, 25)
    await api.listByClient(7, 1, 5)
    expect(callOf(mock, 0).url).toBe('/api/billing/quotes?page=2&pageSize=25')
    expect(callOf(mock, 1).url).toBe('/api/billing/quotes/client/7?page=1&pageSize=5')
  })

  it('list and listByClient send the search and the filters that are set and leave the others off', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))
    const api = useQuotesApi()

    await api.list(1, 25, { search: 'logo', clientId: 4, stage: 'Delivered', validity: 'Expired' })
    await api.list(1, 25, { search: '', stage: undefined })
    await api.listByClient(7, 2, 5, { search: 'logo', stage: 'Accepted', validity: 'Valid' })
    expect(callOf(mock, 0).url).toBe('/api/billing/quotes?page=1&pageSize=25&search=logo&clientId=4&stage=Delivered&validity=Expired')
    expect(callOf(mock, 1).url).toBe('/api/billing/quotes?page=1&pageSize=25')
    expect(callOf(mock, 2).url).toBe('/api/billing/quotes/client/7?page=2&pageSize=5&search=logo&stage=Accepted&validity=Valid')
  })

  it('get reads one quote', async () => {
    const mock = stubFetch(json({ id: 3 }))

    expect(await useQuotesApi().get(3)).toEqual({ id: 3 })
    expect(callOf(mock).url).toBe('/api/quotes/3')
  })

  it('create POSTs the payload and returns the id', async () => {
    const mock = stubFetch(json(12))
    const payload = { clientId: 1, subject: 's', stage: 'Draft' as const, validUntil: '2026-03-01', items: [] }

    expect(await useQuotesApi().create(payload)).toBe(12)
    expect(callOf(mock)).toMatchObject({ url: '/api/billing/quotes', method: 'POST', body: payload })
  })

  it('update PUTs the payload', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const payload = { subject: 's', stage: 'Accepted' as const, validUntil: '2026-03-01', items: [] }

    await useQuotesApi().update(3, payload)
    expect(callOf(mock)).toMatchObject({ url: '/api/quotes/3', method: 'PUT', body: payload })
  })

  it('duplicate and convertToInvoice POST and return the new id', async () => {
    const mock = stubFetch(json(20))
    const api = useQuotesApi()

    expect(await api.duplicate(3)).toBe(20)
    expect(await api.convertToInvoice(3)).toBe(20)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/quotes/3/duplicate', method: 'POST' })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/quotes/3/convert-to-invoice', method: 'POST' })
  })

  it('remove DELETEs the quote', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useQuotesApi().remove(3)
    expect(callOf(mock)).toMatchObject({ url: '/api/quotes/3', method: 'DELETE' })
  })

  it('rejects with an ApiError carrying the refusal body', async () => {
    stubFetch(problem('Quote is accepted.', 409))

    await expect(useQuotesApi().remove(3)).rejects.toBeInstanceOf(ApiError)
  })
})
