/**
 * Tests for the tax rules store: the list loads, every write refreshes it, and a refusal lands in
 * `error` or `actionError` with the API's own sentence while the action reports failure as a value.
 */

import { describe, expect, it, vi } from 'vitest'
import { useTaxRulesStore } from '@/stores/taxRules'
import type { SaveTaxRulePayload } from '@/types/taxrule'
import { json, problem, stubFetch } from '../helpers/fetch'
import { installStoreHooks, NO_ANSWER } from '../helpers/storeHooks'

/** A rule's fields as the form sends them. */
const payload: SaveTaxRulePayload = { name: 'Germany VAT', country: 'DE', region: null, ratePercent: 19, isEnabled: true }

describe('taxRulesStore', () => {
  installStoreHooks()

  it('fetchAll fills the list', async () => {
    stubFetch(json([{ id: 1, name: 'Germany VAT' }]))
    const store = useTaxRulesStore()

    await store.fetchAll()
    expect(store.taxRules).toEqual([{ id: 1, name: 'Germany VAT' }])
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
  })

  it('fetchAll keeps the API sentence on refusal', async () => {
    stubFetch(problem('Tax rules locked.', 423))
    const store = useTaxRulesStore()

    await store.fetchAll()
    expect(store.error).toBe('Tax rules locked.')
    expect(store.loading).toBe(false)
  })

  it('create returns true and refreshes the list', async () => {
    const mock = stubFetch(json(7, 201), json([{ id: 7 }]))
    const store = useTaxRulesStore()

    expect(await store.create(payload)).toBe(true)
    expect(mock).toHaveBeenCalledTimes(2)
    expect(store.taxRules).toEqual([{ id: 7 }])
    expect(store.actionError).toBeNull()
  })

  it('create returns false and stores the API sentence on refusal', async () => {
    stubFetch(problem('A rule for this country exists.', 409))
    const store = useTaxRulesStore()

    expect(await store.create(payload)).toBe(false)
    expect(store.actionError).toBe('A rule for this country exists.')
  })

  it('update returns true and refreshes the list', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }), json([]))
    const store = useTaxRulesStore()

    expect(await store.update(7, payload)).toBe(true)
    expect(mock).toHaveBeenCalledTimes(2)
  })

  it('update returns false with the fallback when nothing answered', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('offline')))
    const store = useTaxRulesStore()

    expect(await store.update(7, payload)).toBe(false)
    expect(store.actionError).toBe(NO_ANSWER)
  })

  it('remove returns true and refreshes the list', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }), json([]))
    const store = useTaxRulesStore()

    expect(await store.remove(7)).toBe(true)
    expect(mock).toHaveBeenCalledTimes(2)
  })

  it('remove returns false and stores the API sentence on refusal', async () => {
    stubFetch(problem('The tax rule does not exist.', 404))
    const store = useTaxRulesStore()

    expect(await store.remove(7)).toBe(false)
    expect(store.actionError).toBe('The tax rule does not exist.')
  })
})
