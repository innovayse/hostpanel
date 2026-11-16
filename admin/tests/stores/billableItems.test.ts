/**
 * Tests for the billable items store: the paged, filtered load, a write that leaves the reload to the screen, and a
 * failure that lands in `error`.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useBillableItemsStore } from '@/stores/billableItems'
import { BillableItemInvoiced, BillableItemType } from '@/types/billableitem'
import { callOf, json, problem, stubFetch, stubOffline } from '../helpers/fetch'

describe('billableItemsStore.fetchAll', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('loads the page it is given and keeps the total count', async () => {
    const mock = stubFetch(json({ items: [{ id: 1 }], totalCount: 250 }))
    const store = useBillableItemsStore()

    expect(await store.fetchAll(3)).toBe(true)
    expect(store.items).toEqual([{ id: 1 }])
    expect(store.totalCount).toBe(250)
    expect(callOf(mock).url).toBe('/api/billing/billable-items?page=3&pageSize=20')
  })

  it('sends the filters and trims the search', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))
    const store = useBillableItemsStore()

    await store.fetchAll(1, { search: '  hosting ', clientId: 7, type: BillableItemType.Recurring, invoiced: BillableItemInvoiced.Yes })
    expect(callOf(mock).url).toBe(
      '/api/billing/billable-items?page=1&pageSize=20&search=hosting&clientId=7&type=Recurring&invoiced=true',
    )
  })

  it('returns false with the API sentence when the load is refused', async () => {
    stubFetch(problem('Items are unavailable.', 403))
    const store = useBillableItemsStore()

    expect(await store.fetchAll(1)).toBe(false)
    expect(store.error).toBe('Items are unavailable.')
    expect(store.loading).toBe(false)
  })
})

describe('billableItemsStore.create', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('returns true and leaves the reload to the screen', async () => {
    const mock = stubFetch(json({}))
    const store = useBillableItemsStore()

    expect(await store.create({ clientId: 1, description: 'x', amount: 1, currency: 'USD', type: BillableItemType.OneTime })).toBe(true)
    expect(mock).toHaveBeenCalledTimes(1)
    expect(store.items).toEqual([])
  })

  it('returns false and sets error when the call fails', async () => {
    stubOffline()
    const store = useBillableItemsStore()

    expect(await store.create({ clientId: 1, description: 'x', amount: 1, currency: 'USD', type: BillableItemType.OneTime })).toBe(false)
    expect(store.error).toBe('Could not reach the server. Check your connection and try again.')
  })
})

describe('billableItemsStore.removeItem', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('deletes once and leaves the reload to the screen', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const store = useBillableItemsStore()

    expect(await store.removeItem(4)).toBe(true)
    expect(mock).toHaveBeenCalledTimes(1)
    expect(callOf(mock)).toMatchObject({ url: '/api/billing/billable-items/4', method: 'DELETE' })
  })

  it('returns false with the API sentence when the delete is refused', async () => {
    stubFetch(problem('The item is already invoiced.', 409))
    const store = useBillableItemsStore()

    expect(await store.removeItem(4)).toBe(false)
    expect(store.error).toBe('The item is already invoiced.')
    expect(store.loading).toBe(false)
  })
})
