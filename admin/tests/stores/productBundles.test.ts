/**
 * Tests for the product bundles store: the load fills the list, every write re-reads it, and a
 * refusal lands in the matching error state while the action reports failure as a value.
 */

import { describe, expect, it } from 'vitest'
import { translate } from '@/lib/translate'
import { useProductBundlesStore } from '@/stores/productBundles'
import type { ProductBundle, ProductBundlePayload } from '@/types/productbundle'
import { BundleDiscountType } from '@/types/productenums'
import { callsOf, invalid, json, problem, stubFetch, stubOffline } from '../helpers/fetch'
import { installStoreHooks } from '../helpers/storeHooks'

/** A bundle as the create and update calls take it. */
const payload: ProductBundlePayload = {
  name: 'Starter pack',
  description: null,
  sortOrder: 0,
  includesDomain: false,
  domainYears: null,
  discountType: BundleDiscountType.None,
  discountAmount: 0,
  discountCurrency: null,
  items: [{ productId: 3, isPrimary: true }],
}

/** One bundle as the list answers it. */
const bundle: ProductBundle = {
  id: 1,
  name: 'Starter pack',
  description: null,
  isActive: true,
  sortOrder: 0,
  includesDomain: false,
  domainYears: null,
  discountType: BundleDiscountType.None,
  discountAmount: 0,
  discountCurrency: null,
  components: [{ productId: 3, name: 'Starter', isPrimary: true }],
}

describe('useProductBundlesStore', () => {
  installStoreHooks()

  it('fetchAll fills the list', async () => {
    stubFetch(json([bundle]))
    const store = useProductBundlesStore()

    expect(await store.fetchAll()).toBe(true)

    expect(store.bundles).toEqual([bundle])
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
  })

  it('keeps the bundles it had after a refused or unanswered load, with the matching sentence', async () => {
    const store = useProductBundlesStore()
    stubFetch(json([bundle]))
    await store.fetchAll()

    stubFetch(problem('Locked.', 423))
    expect(await store.fetchAll()).toBe(false)
    expect(store.error).toBe('Locked.')

    stubOffline()
    expect(await store.fetchAll()).toBe(false)
    expect(store.error).toBe(translate('errors.noAnswer'))
    expect(store.bundles).toEqual([bundle])
  })

  it('create posts, then re-reads the list', async () => {
    const mock = stubFetch(json(1, 201), json([bundle]))
    const store = useProductBundlesStore()

    expect(await store.create(payload)).toBe(true)
    expect(callsOf(mock).map(call => `${call.method} ${call.url}`)).toEqual([
      'POST /api/products/bundles',
      'GET /api/products/bundles',
    ])
    expect(store.bundles).toEqual([bundle])
    expect(store.saving).toBe(false)
  })

  it('a refused create returns false with the sentence and the field messages', async () => {
    stubFetch(invalid('Validation failed', { name: ['Name is required.'] }))
    const store = useProductBundlesStore()

    expect(await store.create(payload)).toBe(false)
    expect(store.saveError).toBe('Validation failed')
    expect(store.fieldErrors.name).toBe('Name is required.')

    store.resetSaveError()

    expect(store.saveError).toBeNull()
    expect(store.fieldErrors).toEqual({})
  })

  it('update puts, then re-reads the list', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }), json([bundle]))
    const store = useProductBundlesStore()

    expect(await store.update(1, payload)).toBe(true)
    expect(callsOf(mock).map(call => `${call.method} ${call.url}`)).toEqual([
      'PUT /api/products/bundles/1',
      'GET /api/products/bundles',
    ])
  })

  it('remove re-reads the list; a refusal sets actionError', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }), json([]))
    const store = useProductBundlesStore()

    expect(await store.remove(1)).toBe(true)
    expect(callsOf(mock).map(call => call.method)).toEqual(['DELETE', 'GET'])

    stubFetch(problem('Bundle not found.', 404))

    expect(await store.remove(1)).toBe(false)
    expect(store.actionError).toBe('Bundle not found.')

    store.clearActionError()

    expect(store.actionError).toBeNull()
  })
})
