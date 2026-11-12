/**
 * Tests for the fraud settings store: a read fills the settings, a save sends the whole document
 * and reads it back, and a refusal lands in `actionError` with its per-field messages.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useFraudSettingsStore } from '@/stores/fraudSettings'
import type { FraudSettings } from '@/types/fraud'
import { callOf, callsOf, invalid, json, problem, stubFetch, stubOffline } from '../helpers/fetch'

/** Sentence the client writes when nothing answered. */
const NO_ANSWER = 'Could not reach the server. Check your connection and try again.'

/** Stored settings. */
const stored: FraudSettings = {
  enabled: true,
  manualReviewRiskScore: 50,
  autoRejectRiskScore: 80,
  velocityWindowMinutes: 60,
  velocityMaxOrders: 5,
  maxAccountsPerIp: 3,
}

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('fraudSettingsStore', () => {
  it('fetchSettings fills the settings and clears loading', async () => {
    const mock = stubFetch(json(stored))
    const store = useFraudSettingsStore()

    await store.fetchSettings()

    expect(store.settings).toEqual(stored)
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
    expect(callOf(mock).url).toBe('/api/admin/fraud/settings')
  })

  it('fetchSettings shows the API sentence on refusal', async () => {
    stubFetch(problem('Not allowed.', 403))
    const store = useFraudSettingsStore()

    await store.fetchSettings()

    expect(store.error).toBe('Not allowed.')
    expect(store.settings).toBeNull()
  })

  it('fetchSettings shows the no-answer sentence when the network is down', async () => {
    stubOffline()
    const store = useFraudSettingsStore()

    await store.fetchSettings()

    expect(store.error).toBe(NO_ANSWER)
  })

  it('save puts the settings and reads them back', async () => {
    const next: FraudSettings = { ...stored, manualReviewRiskScore: 40 }
    const mock = stubFetch(new Response(null, { status: 204 }), json(next))
    const store = useFraudSettingsStore()

    const saved = await store.save(next)

    expect(saved).toBe(true)
    expect(store.saving).toBe(false)
    expect(store.settings).toEqual(next)
    const [put, get] = callsOf(mock)
    expect(put).toMatchObject({ url: '/api/admin/fraud/settings', method: 'PUT', body: next })
    expect(get).toMatchObject({ url: '/api/admin/fraud/settings', method: 'GET' })
  })

  it('save keeps the refusal sentence and the field messages, and reads nothing back', async () => {
    const mock = stubFetch(invalid('Review must be below reject.', { ManualReviewRiskScore: ['Must be lower than the auto reject threshold.'] }))
    const store = useFraudSettingsStore()

    const saved = await store.save(stored)

    expect(saved).toBe(false)
    expect(store.actionError).toBe('Review must be below reject.')
    expect(store.fieldErrors).toEqual({ manualReviewRiskScore: 'Must be lower than the auto reject threshold.' })
    expect(mock).toHaveBeenCalledTimes(1)
  })
})
