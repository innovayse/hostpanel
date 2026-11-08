/**
 * Tests for the plugins API composable: paths, verbs and the multipart install through the real
 * `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { usePluginsApi } from '@/composables/apis/usePluginsApi'
import { json, problem, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('usePluginsApi', () => {
  it('listPlugins reads /admin/plugins', async () => {
    const mock = stubFetch(json([{ id: 'p' }]))

    expect(await usePluginsApi().listPlugins()).toEqual([{ id: 'p' }])
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/plugins', method: 'GET' })
  })

  it('installPlugin posts the file as multipart form data', async () => {
    const mock = stubFetch(json({ requiresRestart: true }))
    const zip = new File(['z'], 'plugin.zip', { type: 'application/zip' })

    expect(await usePluginsApi().installPlugin(zip)).toEqual({ requiresRestart: true })
    const call = callOf(mock)
    expect(call).toMatchObject({ url: '/api/admin/plugins/install', method: 'POST' })
    expect((call.init.body as FormData).get('file')).toBeInstanceOf(File)
  })

  it('removePlugin DELETEs by id and restartServer POSTs to restart', async () => {
    const mock = stubFetch(json({ requiresRestart: true }))
    const api = usePluginsApi()

    expect(await api.removePlugin('p')).toEqual({ requiresRestart: true })
    await api.restartServer()
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/admin/plugins/p', method: 'DELETE' })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/admin/plugins/restart', method: 'POST' })
  })

  it('rejects when the API refuses', async () => {
    stubFetch(problem('Not a plugin archive.', 400))

    await expect(usePluginsApi().installPlugin(new File(['x'], 'a.zip'))).rejects.toBeTruthy()
  })

  it('getRegistryDetails reads the registry entry by id', async () => {
    const mock = stubFetch(json({ id: 'p', versions: [] }))

    expect(await usePluginsApi().getRegistryDetails('p')).toEqual({ id: 'p', versions: [] })
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/plugins/registry/p', method: 'GET' })
  })

  it('saveLicenceKey PUTs the key and removeLicenceKey DELETEs the same path', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }), new Response(null, { status: 204 }))
    const api = usePluginsApi()

    await api.saveLicenceKey('p', { key: 'abc' })
    await api.removeLicenceKey('p')
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/admin/plugins/registry/p/licence', method: 'PUT' })
    expect(JSON.parse(String(callOf(mock, 0).init.body))).toEqual({ key: 'abc' })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/admin/plugins/registry/p/licence', method: 'DELETE' })
  })

  it('installFromRegistry posts the version and the downgrade flag', async () => {
    const mock = stubFetch(json({ requiresRestart: false }))

    await usePluginsApi().installFromRegistry('p', { version: '1.0.0', allowDowngrade: true })
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/plugins/registry/p/install', method: 'POST' })
    expect(JSON.parse(String(callOf(mock).init.body))).toEqual({ version: '1.0.0', allowDowngrade: true })
  })

  it('encodes every id placed in a path', async () => {
    const mock = stubFetch(json({ requiresRestart: false }))
    const api = usePluginsApi()

    await api.removePlugin('a/b c')
    await api.getRegistryDetails('x?y')
    await api.installFromRegistry('a#b', { version: '1.0.0', allowDowngrade: false })
    await api.saveLicenceKey('a b', { key: 'k' })
    await api.listSlotWidgets('p/q')
    await api.saveSlotWidgetProducts('p/q', 's/1', 'w 2', { productIds: [] })

    expect(callOf(mock, 0).url).toBe('/api/admin/plugins/a%2Fb%20c')
    expect(callOf(mock, 1).url).toBe('/api/admin/plugins/registry/x%3Fy')
    expect(callOf(mock, 2).url).toBe('/api/admin/plugins/registry/a%23b/install')
    expect(callOf(mock, 3).url).toBe('/api/admin/plugins/registry/a%20b/licence')
    expect(callOf(mock, 4).url).toBe('/api/admin/plugins/p%2Fq/slot-widgets')
    expect(callOf(mock, 5).url).toBe('/api/admin/plugins/p%2Fq/slot-widgets/s%2F1/w%202')
  })
})

describe('usePluginsApi registration', () => {
  it('getRegistryInstance reads the state and retryRegistryInstance POSTs to retry', async () => {
    const mock = stubFetch(json({ state: 'Verified' }), json({ state: 'Pending' }))
    const api = usePluginsApi()

    expect(await api.getRegistryInstance()).toEqual({ state: 'Verified' })
    expect(await api.retryRegistryInstance()).toEqual({ state: 'Pending' })
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/admin/plugins/registry-instance', method: 'GET' })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/admin/plugins/registry-instance/retry', method: 'POST' })
  })
})
