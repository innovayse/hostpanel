/**
 * Tests for the fraud API composable: every endpoint through the real `useApi` transport with
 * `fetch` stubbed, checking the URL, the verb and the body each function sends.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useFraudApi } from '@/composables/apis/useFraudApi'
import { BlockListType, FraudDecision, FraudReasonCode } from '@/types/fraud'
import type { FraudSettings } from '@/types/fraud'
import { callOf, json, stubFetch } from '../../helpers/fetch'

/** A settings document. */
const settings: FraudSettings = {
  enabled: true,
  manualReviewRiskScore: 50,
  autoRejectRiskScore: 80,
  velocityWindowMinutes: 60,
  velocityMaxOrders: 5,
  maxAccountsPerIp: 3,
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useFraudApi', () => {
  it('listBlockList reads one page of the chosen list', async () => {
    const mock = stubFetch(json({ items: [], pageNumber: 2, pageSize: 20, totalCount: 0, totalPages: 0 }))

    await useFraudApi().listBlockList(BlockListType.EmailDomain, 2, 20)

    const call = callOf(mock)
    expect(call.method).toBe('GET')
    expect(new URL(call.url, 'http://localhost').pathname).toBe('/api/admin/fraud/blocklist')
    const query = new URL(call.url, 'http://localhost').searchParams
    expect(query.get('type')).toBe('EmailDomain')
    expect(query.get('page')).toBe('2')
    expect(query.get('pageSize')).toBe('20')
  })

  it('addBlockListEntry posts the entry and returns the new id', async () => {
    const mock = stubFetch(json(7, 201))

    const id = await useFraudApi().addBlockListEntry({ type: BlockListType.Ip, value: '203.0.113.7', note: null })

    expect(id).toBe(7)
    expect(callOf(mock)).toMatchObject({
      url: '/api/admin/fraud/blocklist',
      method: 'POST',
      body: { type: 'Ip', value: '203.0.113.7', note: null },
    })
  })

  it('deleteBlockListEntry deletes the entry by id', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useFraudApi().deleteBlockListEntry(9)

    expect(callOf(mock)).toMatchObject({ url: '/api/admin/fraud/blocklist/9', method: 'DELETE' })
  })

  it('listLog pages the log and leaves empty filters out', async () => {
    const mock = stubFetch(json({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 }))

    await useFraudApi().listLog({ decision: '', reason: '' }, 1, 20)

    expect(callOf(mock).url).toBe('/api/admin/fraud/log?page=1&pageSize=20')
  })

  it('listLog sends the decision and the reason', async () => {
    const mock = stubFetch(json({ items: [], pageNumber: 3, pageSize: 10, totalCount: 0, totalPages: 0 }))

    await useFraudApi().listLog({ decision: FraudDecision.Review, reason: FraudReasonCode.OrderVelocity }, 3, 10)

    const query = new URL(callOf(mock).url, 'http://localhost').searchParams
    expect(query.get('decision')).toBe('Review')
    expect(query.get('reason')).toBe('OrderVelocity')
    expect(query.get('page')).toBe('3')
  })

  it('getSettings reads the settings document', async () => {
    const mock = stubFetch(json(settings))

    const result = await useFraudApi().getSettings()

    expect(result).toEqual(settings)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/fraud/settings', method: 'GET' })
  })

  it('updateSettings puts the whole document', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useFraudApi().updateSettings(settings)

    expect(callOf(mock)).toMatchObject({ url: '/api/admin/fraud/settings', method: 'PUT', body: settings })
  })
})
