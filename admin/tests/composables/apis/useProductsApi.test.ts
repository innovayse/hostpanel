/**
 * Tests for the products API composable: the path and verb of the free-trial defaults through the
 * real `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useProductsApi } from '@/composables/apis/useProductsApi'
import { json, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useProductsApi', () => {
  it('getTrialDefaults reads the defaults and limits of the trial settings', async () => {
    const mock = stubFetch(json({ days: 14, maxDays: 365 }))

    expect(await useProductsApi().getTrialDefaults()).toEqual({ days: 14, maxDays: 365 })
    expect(callOf(mock)).toMatchObject({ url: '/api/products/trial-defaults', method: 'GET' })
  })
})
