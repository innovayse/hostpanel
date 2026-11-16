/**
 * Tests for the tax rules store: a page loads with its filters, a write leaves the re-read to the screen, and a refusal
 * lands in `error` or `actionError` with the API's own sentence while the action reports failure as a value.
 */

import { describe, expect, it, vi } from 'vitest'
import { useTaxRulesStore } from '@/stores/taxRules'
import type { SaveTaxRulePayload } from '@/types/taxrule'
import { callOf, json, problem, stubFetch } from '../helpers/fetch'
import { installStoreHooks, NO_ANSWER } from '../helpers/storeHooks'

/** A rule's fields as the form sends them. */
const payload: SaveTaxRulePayload = { name: 'Germany VAT', country: 'DE', region: null, ratePercent: 19, isEnabled: true }

describe('taxRulesStore', () => {
  installStoreHooks()

  it('fetchAll fills the page and the total, and sends the page, its size and the trimmed filters', async () => {
    const mock = stubFetch(json({ items: [{ id: 1, name: 'Germany VAT' }], pageNumber: 2, pageSize: 20, totalCount: 21, totalPages: 2 }))
    const store = useTaxRulesStore()

    expect(await store.fetchAll(2, { search: ' vat ', enabled: true })).toBe(true)
    expect(callOf(mock).url).toBe('/api/admin/tax-rules?page=2&pageSize=20&search=vat&enabled=true')
    expect(store.taxRules).toEqual([{ id: 1, name: 'Germany VAT' }])
    expect(store.totalCount).toBe(21)
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
  })

  it('fetchAll keeps the API sentence on refusal', async () => {
    stubFetch(problem('Tax rules locked.', 423))
    const store = useTaxRulesStore()

    expect(await store.fetchAll(1)).toBe(false)
    expect(store.error).toBe('Tax rules locked.')
    expect(store.loading).toBe(false)
  })

  it('create returns true and leaves the list to the screen', async () => {
    const mock = stubFetch(json(7, 201))
    const store = useTaxRulesStore()

    expect(await store.create(payload)).toBe(true)
    expect(mock).toHaveBeenCalledTimes(1)
    expect(store.taxRules).toEqual([])
    expect(store.actionError).toBeNull()
  })

  it('create returns false and stores the API sentence on refusal', async () => {
    stubFetch(problem('A rule for this country exists.', 409))
    const store = useTaxRulesStore()

    expect(await store.create(payload)).toBe(false)
    expect(store.actionError).toBe('A rule for this country exists.')
  })

  it('update returns true and leaves the list to the screen', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const store = useTaxRulesStore()

    expect(await store.update(7, payload)).toBe(true)
    expect(mock).toHaveBeenCalledTimes(1)
  })

  it('update returns false with the fallback when nothing answered', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('offline')))
    const store = useTaxRulesStore()

    expect(await store.update(7, payload)).toBe(false)
    expect(store.actionError).toBe(NO_ANSWER)
  })

  it('remove returns true and leaves the list to the screen', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const store = useTaxRulesStore()

    expect(await store.remove(7)).toBe(true)
    expect(mock).toHaveBeenCalledTimes(1)
  })

  it('remove returns false and stores the API sentence on refusal', async () => {
    stubFetch(problem('The tax rule does not exist.', 404))
    const store = useTaxRulesStore()

    expect(await store.remove(7)).toBe(false)
    expect(store.actionError).toBe('The tax rule does not exist.')
  })
})
