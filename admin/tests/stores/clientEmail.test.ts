/**
 * Tests for the client e-mail log store: a load fills the entries and sends the search, the outcome and the period,
 * a refusal lands in the store.
 */

import { describe, expect, it } from 'vitest'
import { useClientEmailStore } from '@/stores/clientEmail'
import { json, problem, stubFetch, stubOffline, callOf } from '../helpers/fetch'
import { installStoreHooks, NO_ANSWER } from '../helpers/storeHooks'

describe('clientEmailStore', () => {
  installStoreHooks()

  it('fetchClientEmails fills the entries and reset clears them', async () => {
    const mock = stubFetch(json({ items: [{ id: 1 }], totalCount: 3 }))
    const store = useClientEmailStore()

    await store.fetchClientEmails(7, 1)
    expect(store.emails).toEqual([{ id: 1 }])
    expect(store.totalCount).toBe(3)
    expect(callOf(mock).url).toBe('/api/admin/email-logs/client/7?page=1&pageSize=20')

    store.reset()
    expect(store.emails).toEqual([])
    expect(store.error).toBeNull()
  })

  it('fetchClientEmails sends the trimmed search, the outcome and the period that are set', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))
    const store = useClientEmailStore()

    await store.fetchClientEmails(7, 2, { search: '  invoice  ', success: true, from: '2026-03-01', to: '' })

    expect(callOf(mock).url).toBe('/api/admin/email-logs/client/7?page=2&pageSize=20&search=invoice&success=true&from=2026-03-01')
  })

  it('fetchClientEmails shows the API sentence on refusal and the no-answer sentence offline', async () => {
    const store = useClientEmailStore()
    stubFetch(problem('Logs are hidden.', 403))
    await store.fetchClientEmails(7, 1)
    expect(store.error).toBe('Logs are hidden.')

    stubOffline()
    await store.fetchClientEmails(7, 1)
    expect(store.error).toBe(NO_ANSWER)
  })
})
