/**
 * Tests for `composables/apis/useCatalogApi.ts`.
 *
 * Each endpoint function is called for real through `apiFetch` against a stub server, and the
 * test asserts on the path, verb, query and body that arrived and on how the reply was typed.
 *
 * @module tests/composables/apis/useCatalogApi.test
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { expectLastRequest, startTransport } from '../../helpers/stubTransport'
import type { ExpectedRequest } from '../../helpers/stubTransport'
import type { StubServer } from '../../helpers/stubServer'
import { useCatalogApi } from '~/composables/apis/useCatalogApi'
import { HttpMethod } from '~/types/httpmethod'

/** The stub started by the current test. */
let stub: StubServer | undefined

afterEach(async () => {
  await stub?.close()
  stub = undefined
})

/** The api surface under test. */
const api = useCatalogApi()

/** Every endpoint function with the request it must send. */
const rows: ExpectedRequest[] = [
  { name: 'fetchProducts sends the filters as a query', call: () => api.fetchProducts({ gid: 1, currency: 'USD' }), method: HttpMethod.Get, url: '/api/portal/public/products?gid=1&currency=USD' },
  { name: 'fetchProducts drops an undefined filter and sends no query at all', call: () => api.fetchProducts({ currency: undefined }), method: HttpMethod.Get, url: '/api/portal/public/products' },
  { name: 'fetchProducts sends the group list as one comma-separated value', call: () => api.fetchProducts({ gids: '1,3,4' }), method: HttpMethod.Get, url: '/api/portal/public/products?gids=1,3,4' },
  { name: 'fetchProductGroups reads the list', call: () => api.fetchProductGroups(), method: HttpMethod.Get, url: '/api/portal/public/product-groups' },
  { name: 'fetchProductBundles reads the list', call: () => api.fetchProductBundles(), method: HttpMethod.Get, url: '/api/portal/public/product-bundles' },
  { name: 'fetchProductFeatures asks for one group', call: () => api.fetchProductFeatures(7), method: HttpMethod.Get, url: '/api/portal/public/product-features?gid=7' },
  { name: 'fetchCurrencies reads the list', call: () => api.fetchCurrencies(), method: HttpMethod.Get, url: '/api/portal/public/currencies' },
  { name: 'fetchTldPricing quotes in the payer currency', call: () => api.fetchTldPricing('AMD'), method: HttpMethod.Get, url: '/api/portal/public/tld-pricing?currency=AMD' },
  { name: 'fetchTldPricing sends no query without a currency', call: () => api.fetchTldPricing(), method: HttpMethod.Get, url: '/api/portal/public/tld-pricing' },
  { name: 'fetchSlides asks for the language', call: () => api.fetchSlides('hy'), method: HttpMethod.Get, url: '/api/portal/public/slides?lang=hy' },
  { name: 'fetchCountries reads the list', call: () => api.fetchCountries(), method: HttpMethod.Get, url: '/api/portal/public/countries' },
  { name: 'checkDomain posts one domain', call: () => api.checkDomain('a.am'), method: HttpMethod.Post, url: '/api/portal/public/domain-check', body: { domain: 'a.am' } },
]

describe('useCatalogApi', () => {
  it.each(rows)('$name', async (row) => {
    stub = await startTransport()

    await row.call()

    expectLastRequest(stub, row)
  })

  it('returns the parsed reply typed as the endpoint promises', async () => {
    stub = await startTransport(() => ({ body: [{ id: 1, name: 'Starter', groupId: 1 }] }))

    const products = await api.fetchProducts({ gid: 1 })

    expect(products[0]?.name).toBe('Starter')
  })

  it('rejects with the body of a refusal so the store can read the sentence', async () => {
    stub = await startTransport(() => ({ status: 400, body: { type: 'X', detail: 'No.' } }))

    await expect(api.checkDomain('a.am')).rejects.toMatchObject({ data: { detail: 'No.' } })
  })

  it('runs every read inside the Nuxt context it was created in, so a read started after an await still works', async () => {
    const original = (globalThis as Record<string, unknown>).useNuxtApp
    const runWithContext = vi.fn(<T>(callback: () => T): T => callback())
    vi.stubGlobal('useNuxtApp', () => ({ isHydrating: false, runWithContext }))
    const scoped = useCatalogApi()
    // From here the ambient context is gone, as it is for the second read of a server render.
    vi.stubGlobal('useNuxtApp', () => {
      throw new Error('NUXT_E1001')
    })
    stub = await startTransport()

    try {
      await scoped.fetchProductGroups()
      await scoped.fetchProducts({ gids: '1,2' })
    }
    finally {
      vi.stubGlobal('useNuxtApp', original)
    }

    expect(runWithContext).toHaveBeenCalledTimes(2)
    expectLastRequest(stub, { name: 'last read', call: () => scoped.fetchProducts(), method: HttpMethod.Get, url: '/api/portal/public/products?gids=1,2' })
  })
})
