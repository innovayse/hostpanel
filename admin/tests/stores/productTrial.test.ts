/**
 * Tests for the product trial store: the defaults are returned, and a refusal puts the API's
 * sentence into `error`. The requests go through the real `useApi` transport.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useProductTrialStore } from '@/stores/productTrial'
import { json, problem, stubFetch, callOf } from '../helpers/fetch'

describe('productTrialStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetchDefaults returns what the API answers', async () => {
    const mock = stubFetch(json({ days: 14, reminderDays: [3, 1] }))
    const store = useProductTrialStore()

    expect(await store.fetchDefaults()).toEqual({ days: 14, reminderDays: [3, 1] })
    expect(callOf(mock).url).toBe('/api/products/trial-defaults')
    expect(store.error).toBeNull()
  })

  it('fetchDefaults reports the API sentence and returns null when refused', async () => {
    stubFetch(problem('Not allowed.', 403))
    const store = useProductTrialStore()

    expect(await store.fetchDefaults()).toBeNull()
    expect(store.error).toBe('Not allowed.')
  })
})
