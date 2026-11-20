/**
 * Tests for the VAT rates store: a page loads with its search, a write leaves the re-read to the screen, and a refusal
 * lands in `error` or `actionError` with the API's own sentence while the action reports failure as a value.
 */

import { describe, expect, it, vi } from 'vitest'
import { useVatRatesStore } from '@/stores/vatRates'
import { VatRateKind, VatRateStatus } from '@/types/vatrate'
import type { SaveVatRatePayload } from '@/types/vatrate'
import { callOf, json, problem, stubFetch } from '../helpers/fetch'
import { installStoreHooks, NO_ANSWER } from '../helpers/storeHooks'

/** A row's fields as the form sends them. */
const payload: SaveVatRatePayload = { country: 'DE', kind: VatRateKind.Standard, ratePercent: 19, validFrom: '2024-01-01' }

describe('vatRatesStore', () => {
  installStoreHooks()

  it('fetchAll fills the page and the total, and sends the page, its size and the trimmed search', async () => {
    const mock = stubFetch(json({ items: [{ id: 1, country: 'DE' }], pageNumber: 2, pageSize: 20, totalCount: 21, totalPages: 2 }))
    const store = useVatRatesStore()

    expect(await store.fetchAll(2, { search: ' de ' })).toBe(true)
    expect(callOf(mock).url).toBe('/api/admin/vat-rates?page=2&pageSize=20&search=de')
    expect(store.vatRates).toEqual([{ id: 1, country: 'DE' }])
    expect(store.totalCount).toBe(21)
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
  })

  it('fetchAll sends the kind and status filters', async () => {
    const mock = stubFetch(json({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 }))

    await useVatRatesStore().fetchAll(1, { kind: VatRateKind.Reduced, status: VatRateStatus.Superseded })

    expect(callOf(mock).url).toBe('/api/admin/vat-rates?page=1&pageSize=20&kind=Reduced&status=Superseded')
  })

  it('fetchAll keeps the API sentence on refusal', async () => {
    stubFetch(problem('VAT rates locked.', 423))
    const store = useVatRatesStore()

    expect(await store.fetchAll(1)).toBe(false)
    expect(store.error).toBe('VAT rates locked.')
    expect(store.loading).toBe(false)
  })

  it('create returns true and leaves the list to the screen', async () => {
    const mock = stubFetch(json(7, 201))
    const store = useVatRatesStore()

    expect(await store.create(payload)).toBe(true)
    expect(mock).toHaveBeenCalledTimes(1)
    expect(store.vatRates).toEqual([])
    expect(store.actionError).toBeNull()
  })

  it('create returns false and stores the API sentence on refusal', async () => {
    stubFetch(problem('A VAT rate for this day exists.', 409))
    const store = useVatRatesStore()

    expect(await store.create(payload)).toBe(false)
    expect(store.actionError).toBe('A VAT rate for this day exists.')
  })

  it('update returns true and leaves the list to the screen', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const store = useVatRatesStore()

    expect(await store.update(7, payload)).toBe(true)
    expect(mock).toHaveBeenCalledTimes(1)
  })

  it('update returns false with the fallback when nothing answered', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('offline')))
    const store = useVatRatesStore()

    expect(await store.update(7, payload)).toBe(false)
    expect(store.actionError).toBe(NO_ANSWER)
  })

  it('remove returns true and leaves the list to the screen', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const store = useVatRatesStore()

    expect(await store.remove(7)).toBe(true)
    expect(mock).toHaveBeenCalledTimes(1)
  })

  it('remove returns false and stores the API sentence on refusal', async () => {
    stubFetch(problem('The VAT rate does not exist.', 404))
    const store = useVatRatesStore()

    expect(await store.remove(7)).toBe(false)
    expect(store.actionError).toBe('The VAT rate does not exist.')
  })
})
