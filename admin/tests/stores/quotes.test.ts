/**
 * Tests for the quote store: loads fill the lists, mutations reload the open quote, and a
 * refusal or a missing answer lands in the store state instead of a view.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useQuotesStore } from '@/stores/quotes'
import { json, problem, stubFetch, stubOffline, callOf } from '../helpers/fetch'

/** Sentence the client writes when nothing answered. */
const NO_ANSWER = 'Could not reach the server. Check your connection and try again.'

/** A quote payload the create call accepts. */
const draft = { clientId: 1, subject: 's', stage: 'Draft' as const, validUntil: '2026-03-01', items: [] }

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('quoteStore', () => {
  it('fetchAll uses the store page size when none is given', async () => {
    const mock = stubFetch(json({ items: [{ id: 1 }], totalCount: 3 }))
    const store = useQuotesStore()

    await store.fetchAll(2)
    expect(store.quotes).toEqual([{ id: 1 }])
    expect(store.totalCount).toBe(3)
    expect(callOf(mock).url).toBe('/api/billing/quotes?page=2&pageSize=25')
  })

  it('fetchAll and fetchClientQuotes send the search and the filters the screen holds', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))
    const store = useQuotesStore()

    await store.fetchAll(1, { search: 'logo', clientId: 4, stage: 'Delivered', validity: 'Valid' })
    await store.fetchClientQuotes(7, 3, { stage: 'Lost' })
    expect(callOf(mock, 0).url).toBe('/api/billing/quotes?page=1&pageSize=25&search=logo&clientId=4&stage=Delivered&validity=Valid')
    expect(callOf(mock, 1).url).toBe('/api/billing/quotes/client/7?page=3&pageSize=25&stage=Lost')
  })

  it('fetchAll shows the API sentence on refusal', async () => {
    stubFetch(problem('Quotes are disabled.', 403))
    const store = useQuotesStore()

    await store.fetchAll()
    expect(store.error).toBe('Quotes are disabled.')
  })

  it('fetchClientQuotes fills the client list', async () => {
    stubFetch(json({ items: [{ id: 4 }], totalCount: 1 }))
    const store = useQuotesStore()

    await store.fetchClientQuotes(7)
    expect(store.clientQuotes).toEqual([{ id: 4 }])
    expect(store.clientQuotesTotal).toBe(1)
  })

  it('fetchById sets the open quote and reports the no-answer sentence offline', async () => {
    const store = useQuotesStore()
    stubFetch(json({ id: 3 }))
    await store.fetchById(3)
    expect(store.currentQuote).toEqual({ id: 3 })

    stubOffline()
    await store.fetchById(3)
    expect(store.error).toBe(NO_ANSWER)
  })

  it('createQuote returns the id, or null with the sentence on refusal', async () => {
    const store = useQuotesStore()
    stubFetch(json(12))
    expect(await store.createQuote(draft)).toBe(12)

    stubFetch(problem('Subject is required.'))
    expect(await store.createQuote(draft)).toBeNull()
    expect(store.error).toBe('Subject is required.')
  })

  it('updateQuote saves then reloads the quote', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }), json({ id: 3, subject: 'new' }))
    const store = useQuotesStore()

    expect(await store.updateQuote(3, { subject: 'new', stage: 'Draft', validUntil: '2026-03-01', items: [] })).toBe(true)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/quotes/3', method: 'PUT' })
    expect(store.currentQuote).toEqual({ id: 3, subject: 'new' })
    expect(store.successMessage).toBe('Quote #3 saved.')
  })

  it('updateQuote returns false and does not reload on refusal', async () => {
    const mock = stubFetch(problem('Quote is accepted.', 409))
    const store = useQuotesStore()

    expect(await store.updateQuote(3, { subject: 's', stage: 'Draft', validUntil: 'x', items: [] })).toBe(false)
    expect(mock).toHaveBeenCalledTimes(1)
    expect(store.error).toBe('Quote is accepted.')
  })

  it('duplicateQuote and convertToInvoice return the new id and set the success message', async () => {
    const store = useQuotesStore()
    stubFetch(json(20))
    expect(await store.duplicateQuote(3)).toBe(20)
    expect(store.successMessage).toContain('20')

    stubFetch(json(31))
    expect(await store.convertToInvoice(3)).toBe(31)
    expect(store.successMessage).toContain('31')
  })

  it('duplicateQuote and convertToInvoice return null on refusal', async () => {
    const store = useQuotesStore()
    stubFetch(problem('Cannot duplicate.', 409))
    expect(await store.duplicateQuote(3)).toBeNull()
    expect(await store.convertToInvoice(3)).toBeNull()
    expect(store.error).toBe('Cannot duplicate.')
  })

  it('deleteQuote reports the outcome', async () => {
    const store = useQuotesStore()
    stubFetch(new Response(null, { status: 204 }))
    expect(await store.deleteQuote(3)).toBe(true)

    stubFetch(problem('Quote is accepted.', 409))
    expect(await store.deleteQuote(3)).toBe(false)
  })
})
