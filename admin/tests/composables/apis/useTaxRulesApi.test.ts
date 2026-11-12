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
  it('listTaxRules reads /admin/tax-rules', async () => {
    const mock = stubFetch(json([{ id: 1, name: 'Germany VAT' }]))

    expect(await useTaxRulesApi().listTaxRules()).toEqual([{ id: 1, name: 'Germany VAT' }])
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/tax-rules', method: 'GET' })
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
