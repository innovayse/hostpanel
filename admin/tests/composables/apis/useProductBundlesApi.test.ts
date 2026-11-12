/**
 * Tests for the product bundles API composable: paths, verbs and bodies through the real
 * `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useProductBundlesApi } from '@/composables/apis/useProductBundlesApi'
import { ApiError } from '@/composables/useApi'
import type { ProductBundlePayload } from '@/types/productbundle'
import { BundleDiscountType } from '@/types/productenums'
import { callOf, callsOf, json, problem, stubFetch } from '../../helpers/fetch'

/** A bundle as the create and update calls take it. */
const payload: ProductBundlePayload = {
  name: 'Starter pack',
  description: null,
  sortOrder: 0,
  includesDomain: true,
  domainYears: 1,
  discountType: BundleDiscountType.Percentage,
  discountAmount: 10,
  discountCurrency: null,
  items: [{ productId: 3, isPrimary: true }],
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useProductBundlesApi', () => {
  it('listBundles reads the collection', async () => {
    const mock = stubFetch(json([{ id: 1 }]))

    expect(await useProductBundlesApi().listBundles()).toEqual([{ id: 1 }])
    expect(callOf(mock).url).toBe('/api/products/bundles')
    expect(callOf(mock).method).toBe('GET')
  })

  it('createBundle POSTs the fields and answers the new id', async () => {
    const mock = stubFetch(json(9, 201))

    expect(await useProductBundlesApi().createBundle(payload)).toBe(9)
    expect(callOf(mock)).toMatchObject({ url: '/api/products/bundles', method: 'POST', body: payload })
  })

  it('updateBundle PUTs the fields to the bundle and removeBundle DELETEs it', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useProductBundlesApi()

    await api.updateBundle(4, { ...payload, isActive: false })
    await api.removeBundle(4)

    expect(callsOf(mock).map(call => [call.method, call.url, call.body])).toEqual([
      ['PUT', '/api/products/bundles/4', { ...payload, isActive: false }],
      ['DELETE', '/api/products/bundles/4', undefined],
    ])
  })

  it('rejects with the API refusal', async () => {
    stubFetch(problem('Bundle not found.', 404))

    await expect(useProductBundlesApi().removeBundle(4)).rejects.toBeInstanceOf(ApiError)
  })
})
