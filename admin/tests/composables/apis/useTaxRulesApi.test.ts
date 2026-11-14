/**
 * Tests for the tax rules API composable: paths, verbs and bodies through the real `useApi`
 * transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useTaxRulesApi } from '@/composables/apis/useTaxRulesApi'
import type { SaveTaxRulePayload } from '@/types/taxrule'
import { callOf, json, problem, stubFetch } from '../../helpers/fetch'

/** A rule's fields as the form sends them. */
const payload: SaveTaxRulePayload = { name: 'Germany VAT', country: 'DE', region: null, ratePercent: 19, isEnabled: true }

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useTaxRulesApi', () => {
  it('listTaxRules sends the page, its size, the search and the enabled filter', async () => {
    const page = { items: [{ id: 1, name: 'Germany VAT' }], pageNumber: 2, pageSize: 20, totalCount: 21, totalPages: 2 }
    const mock = stubFetch(json(page))

    expect(await useTaxRulesApi().listTaxRules({ page: 2, pageSize: 20, search: 'vat', enabled: false })).toEqual(page)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/tax-rules?page=2&pageSize=20&search=vat&enabled=false', method: 'GET' })
  })

  it('listTaxRules leaves out a filter that is not set', async () => {
    const mock = stubFetch(json({ items: [] }))

    await useTaxRulesApi().listTaxRules({ page: 1, pageSize: 20 })
    expect(callOf(mock).url).toBe('/api/admin/tax-rules?page=1&pageSize=20')
  })

  it('createTaxRule POSTs the payload and returns the new id', async () => {
    const mock = stubFetch(json(7, 201))

    expect(await useTaxRulesApi().createTaxRule(payload)).toBe(7)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/tax-rules', method: 'POST', body: payload })
  })

  it('updateTaxRule PUTs the payload under the id', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useTaxRulesApi().updateTaxRule(7, payload)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/tax-rules/7', method: 'PUT', body: payload })
  })

  it('deleteTaxRule DELETEs the id', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useTaxRulesApi().deleteTaxRule(7)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/tax-rules/7', method: 'DELETE' })
  })

  it('rejects when the API refuses', async () => {
    stubFetch(problem('Not allowed.', 409))

    await expect(useTaxRulesApi().createTaxRule(payload)).rejects.toBeTruthy()
  })
})
