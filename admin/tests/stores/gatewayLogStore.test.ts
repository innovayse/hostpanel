/**
 * Tests for the gateway log store: a load fills the page and the total, and a refusal lands in
 * the store instead of a view.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useGatewayLogStore } from '@/stores/gatewayLog'
import { json, problem, stubFetch } from '../helpers/fetch'

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('gatewayLogStore', () => {
  it('fetchPage fills the entries and the total', async () => {
    stubFetch(json({ items: [{ id: 'tx-1' }], pageNumber: 1, pageSize: 20, totalCount: 7, totalPages: 1 }))
    const store = useGatewayLogStore()

    await store.fetchPage(1)

    expect(store.entries).toEqual([{ id: 'tx-1' }])
    expect(store.totalCount).toBe(7)
    expect(store.error).toBeNull()
    expect(store.loading).toBe(false)
  })

  it('fetchPage keeps the sentence of a refusal', async () => {
    stubFetch(problem('The log is not available.', 403))
    const store = useGatewayLogStore()

    await store.fetchPage(1)

    expect(store.error).toBe('The log is not available.')
    expect(store.entries).toEqual([])
  })
})
