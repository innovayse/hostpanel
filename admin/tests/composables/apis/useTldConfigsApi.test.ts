/**
 * Tests for the TLD configs API composable: paths, verbs and bodies through the real `useApi`
 * transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useTldConfigsApi } from '@/composables/apis/useTldConfigsApi'
import { json, problem, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useTldConfigsApi', () => {
  it('listTldConfigs sends the page, its size, the search and the filters, and getTldConfig reads one config', async () => {
    const mock = stubFetch(json({ items: [] }))
    const api = useTldConfigsApi()

    await api.listTldConfigs({ page: 2, pageSize: 20, search: 'com', registrarModule: 'registrar-one', enabled: true })
    await api.listTldConfigs({ page: 1, pageSize: 20 })
    await api.getTldConfig(5)
    expect(callOf(mock, 0)).toMatchObject({
      url: '/api/admin/tld-configs?page=2&pageSize=20&search=com&registrarModule=registrar-one&enabled=true',
      method: 'GET',
    })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/admin/tld-configs?page=1&pageSize=20', method: 'GET' })
    expect(callOf(mock, 2)).toMatchObject({ url: '/api/admin/tld-configs/5', method: 'GET' })
  })

  it('createTldConfig POSTs the payload and returns the id', async () => {
    const mock = stubFetch(json(8))

    expect(await useTldConfigsApi().createTldConfig({ tld: '.com' } as never)).toBe(8)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/tld-configs', method: 'POST', body: { tld: '.com' } })
  })

  it('updateTldConfig PUTs and deleteTldConfig DELETEs by id', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useTldConfigsApi()

    await api.updateTldConfig(5, { id: 5 } as never)
    await api.deleteTldConfig(5)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/admin/tld-configs/5', method: 'PUT', body: { id: 5 } })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/admin/tld-configs/5', method: 'DELETE' })
  })

  it('importTldConfigs POSTs to the module path and syncTldPrices to sync', async () => {
    const mock = stubFetch(json({ imported: 3 }))
    const api = useTldConfigsApi()

    expect(await api.importTldConfigs('registrar-two')).toEqual({ imported: 3 })
    await api.syncTldPrices()
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/admin/tld-configs/import/registrar-two', method: 'POST' })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/admin/tld-configs/sync', method: 'POST' })
  })

  it('rejects when the API refuses', async () => {
    stubFetch(problem('Provider down.', 502))

    await expect(useTldConfigsApi().syncTldPrices()).rejects.toBeTruthy()
  })
})
