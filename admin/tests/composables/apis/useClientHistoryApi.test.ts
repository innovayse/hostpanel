/**
 * Tests for the client history API composable: the activity log and the e-mail log through
 * the real `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useClientHistoryApi } from '@/composables/apis/useClientHistoryApi'
import { ApiError } from '@/composables/useApi'
import { json, problem, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useClientHistoryApi', () => {
  it('activityLogs sends page, size and only the search and period that are set', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))

    await useClientHistoryApi().activityLogs(7, 2, 25, { search: '10.0.0.1', from: '2026-01-01', to: '' })
    expect(callOf(mock).url).toBe('/api/clients/7/activity-logs?page=2&pageSize=25&search=10.0.0.1&from=2026-01-01')
  })

  it('activityLogs sends the period as the bare days it holds', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))

    await useClientHistoryApi().activityLogs(7, 1, 25, { from: '2026-01-01', to: '2026-01-31' })
    expect(callOf(mock).url).toBe('/api/clients/7/activity-logs?page=1&pageSize=25&from=2026-01-01&to=2026-01-31')
  })

  it('emailLogs pages the client e-mail log', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))

    await useClientHistoryApi().emailLogs(7, 1, 10)
    expect(callOf(mock).url).toBe('/api/admin/email-logs/client/7?page=1&pageSize=10')
  })

  it('emailLogs sends the search, the outcome and the period that are set', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))

    await useClientHistoryApi().emailLogs(7, 1, 10, { search: 'invoice', success: false, from: '2026-01-01', to: '2026-01-31' })
    expect(callOf(mock).url).toBe('/api/admin/email-logs/client/7?page=1&pageSize=10&search=invoice&success=false&from=2026-01-01&to=2026-01-31')
  })

  it('rejects with an ApiError carrying the refusal body', async () => {
    stubFetch(problem('Forbidden.', 403))

    await expect(useClientHistoryApi().emailLogs(7, 1, 10)).rejects.toBeInstanceOf(ApiError)
  })
})
