/**
 * Tests for the client billable items store: loads, invoicing and failure-as-state actions.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useClientBillableItemsStore } from '@/stores/clientBillableItems'
import { BillableItemType } from '@/types/billableitem'
import { invalid, json, problem, stubFetch, stubOffline } from '../helpers/fetch'

describe('clientBillableItemsStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetchItems returns the page and clears loading', async () => {
    const page = { uninvoicedItems: [], uninvoicedTotal: [], invoicedItems: { items: [], totalCount: 0 } }
    stubFetch(json(page))
    const store = useClientBillableItemsStore()

    expect(await store.fetchItems('7', 1, 20)).toEqual(page)
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
  })

  it('fetchItems returns null with the API sentence on refusal', async () => {
    stubFetch(problem('Not your client.', 403))
    const store = useClientBillableItemsStore()

    expect(await store.fetchItems('7', 1, 20)).toBeNull()
    expect(store.error).toBe('Not your client.')
  })

  it('invoiceSelected posts the ids and reports the outcome', async () => {
    const mock = stubFetch(json({}))
    const store = useClientBillableItemsStore()

    expect(await store.invoiceSelected('7', [1, 2])).toBe(true)
    expect(JSON.parse(mock.mock.calls[0]![1].body as string)).toEqual({ billableItemIds: [1, 2] })
  })

  it('deleteItems stops at the first refusal', async () => {
    const mock = stubFetch(json({}), problem('Already invoiced.'))
    const store = useClientBillableItemsStore()

    expect(await store.deleteItems('7', [1, 2, 3])).toBe(false)
    expect(store.error).toBe('Already invoiced.')
    expect(mock).toHaveBeenCalledTimes(2)
  })

  it('fetchServiceOptions maps services and is silent on failure', async () => {
    stubFetch(json({ items: [{ id: 5, productName: 'Hosting' }], totalCount: 1 }))
    const store = useClientBillableItemsStore()
    expect(await store.fetchServiceOptions('7')).toEqual([{ value: 5, label: 'Hosting' }])

    stubOffline()
    expect(await store.fetchServiceOptions('7')).toEqual([])
    expect(store.error).toBeNull()
  })

  it('addItem and addTimeBilling put the refusal into saveError', async () => {
    const store = useClientBillableItemsStore()
    stubFetch(problem('Amount must be positive.'))

    expect(
      await store.addItem('7', { description: 'Setup', amount: -1, currency: 'USD', type: BillableItemType.OneTime }),
    ).toBe(false)
    expect(store.saveError).toBe('Amount must be positive.')

    stubOffline()
    expect(await store.addTimeBilling('7', [])).toBe(false)
    expect(store.saveError).toBe('Could not reach the server. Check your connection and try again.')
  })

  it('clearSaveErrors forgets the sentence and the field messages of the last refused add', async () => {
    const store = useClientBillableItemsStore()
    stubFetch(invalid('Some fields are not valid.', { Amount: ['The amount cannot be negative.'] }))
    await store.addItem('7', { description: 'Setup', amount: -1, currency: 'USD', type: BillableItemType.OneTime })
    expect(store.fieldErrors).toEqual({ amount: 'The amount cannot be negative.' })

    store.clearSaveErrors()

    expect(store.saveError).toBeNull()
    expect(store.fieldErrors).toEqual({})
  })
})
