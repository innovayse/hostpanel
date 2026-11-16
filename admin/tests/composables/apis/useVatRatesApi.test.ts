/**
 * Tests for the VAT rates API composable: paths, verbs and bodies through the real `useApi`
 * transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useVatRatesApi } from '@/composables/apis/useVatRatesApi'
import { VatRateKind } from '@/types/vatrate'
import type { SaveVatRatePayload } from '@/types/vatrate'
import { callOf, json, problem, stubFetch } from '../../helpers/fetch'

/** A row's fields as the form sends them. */
const payload: SaveVatRatePayload = { country: 'DE', kind: VatRateKind.Standard, ratePercent: 19, validFrom: '2024-01-01' }

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useVatRatesApi', () => {
  it('listVatRates sends the page, its size and the search', async () => {
    const page = { items: [{ id: 1, country: 'DE' }], pageNumber: 2, pageSize: 20, totalCount: 21, totalPages: 2 }
    const mock = stubFetch(json(page))

    expect(await useVatRatesApi().listVatRates({ page: 2, pageSize: 20, search: 'de' })).toEqual(page)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/vat-rates?page=2&pageSize=20&search=de', method: 'GET' })
  })

  it('listVatRates leaves out a search that is not set', async () => {
    const mock = stubFetch(json({ items: [] }))

    await useVatRatesApi().listVatRates({ page: 1, pageSize: 20 })
    expect(callOf(mock).url).toBe('/api/admin/vat-rates?page=1&pageSize=20')
  })

  it('createVatRate POSTs the payload and returns the new id', async () => {
    const mock = stubFetch(json(7, 201))

    expect(await useVatRatesApi().createVatRate(payload)).toBe(7)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/vat-rates', method: 'POST', body: payload })
  })

  it('updateVatRate PUTs the payload under the id', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useVatRatesApi().updateVatRate(7, payload)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/vat-rates/7', method: 'PUT', body: payload })
  })

  it('deleteVatRate DELETEs the id', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useVatRatesApi().deleteVatRate(7)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/vat-rates/7', method: 'DELETE' })
  })

  it('rejects when the API refuses', async () => {
    stubFetch(problem('Not allowed.', 409))

    await expect(useVatRatesApi().createVatRate(payload)).rejects.toBeTruthy()
  })
})
