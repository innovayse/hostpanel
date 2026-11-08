/**
 * Tests for the registry half of the plugins store: the page load with its filter, the filter reset
 * rules, the registry-disabled answer, the update list and the install from the registry. Real
 * `useApi` transport, `fetch` stubbed.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { usePluginsStore } from '@/stores/plugins'
import { PluginCategory, PluginErrorCode, PluginPricing } from '@/types/plugin'
import type { PluginRegistryItemDto, RegistryPluginDetailsDto } from '@/types/plugin'
import { callOf, json, problem, stubFetch } from '../helpers/fetch'
import { requestsOf, stubRoutes } from '../helpers/routes'

/** One registry listing. */
const listing: PluginRegistryItemDto = {
  id: 'example-pay',
  name: 'Example Pay',
  author: 'Hostpanel',
  description: 'Card payments',
  iconUrl: null,
  category: PluginCategory.Payment,
  latestVersion: '2.0.0',
  sdkVersion: '3.0.0',
  verified: true,
  pricing: PluginPricing.Free,
  installed: true,
  installedVersion: '1.0.0',
  updateAvailable: true,
}

/**
 * Builds the refusal the server answers when the registry is switched off.
 *
 * @returns The 404 problem body carrying the registry-disabled code.
 */
const disabled = (): Response =>
  json({ type: PluginErrorCode.RegistryDisabled, title: 'Disabled', status: 404, detail: 'The registry is off.' }, 404)

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('pluginsStore.fetchRegistry', () => {
  it('keeps the page and the total, and sends the filter as a query string', async () => {
    const mock = stubFetch(json({ items: [listing], total: 31, pageSize: 10 }))
    const store = usePluginsStore()

    await store.setRegistryFilter({ q: 'pay', category: PluginCategory.Payment })

    expect(store.registryItems).toEqual([listing])
    expect(store.registryTotal).toBe(31)
    expect(store.registryPageSize).toBe(10)
    expect(store.registryLoading).toBe(false)
    expect(callOf(mock).url).toBe('/api/admin/plugins/registry?q=pay&category=Payment&page=1')
  })

  it('sends no empty filter members', async () => {
    const mock = stubFetch(json({ items: [], total: 0, pageSize: 10 }))

    await usePluginsStore().fetchRegistry()

    expect(callOf(mock).url).toBe('/api/admin/plugins/registry?page=1')
  })

  it('starts again at page 1 on a new search or category, and keeps the filter on a page change', async () => {
    stubFetch(json({ items: [], total: 0, pageSize: 10 }))
    const store = usePluginsStore()

    await store.setRegistryFilter({ page: 3 })
    expect(store.registryQuery.page).toBe(3)
    await store.setRegistryFilter({ q: 'pay' })
    expect(store.registryQuery).toEqual({ q: 'pay', category: '', page: 1 })
    await store.setRegistryFilter({ page: 2 })
    expect(store.registryQuery).toEqual({ q: 'pay', category: '', page: 2 })
    await store.setRegistryFilter({ category: PluginCategory.Registrar })
    expect(store.registryQuery).toEqual({ q: 'pay', category: 'Registrar', page: 1 })
  })

  it('keeps the API sentence on refusal', async () => {
    stubFetch(problem('Registry unavailable.', 503))
    const store = usePluginsStore()

    await store.fetchRegistry()

    expect(store.registryError).toBe('Registry unavailable.')
    expect(store.registryDisabled).toBe(false)
  })

  it('clears the previous page when a later load fails', async () => {
    stubFetch(json({ items: [listing], total: 1, pageSize: 10 }), problem('Registry unavailable.', 503))
    const store = usePluginsStore()

    await store.fetchRegistry()
    expect(store.registryItems).toEqual([listing])
    await store.fetchRegistry()

    expect(store.registryItems).toEqual([])
    expect(store.registryTotal).toBe(0)
    expect(store.registryError).toBe('Registry unavailable.')
  })

  it('records the registry-disabled answer without an error', async () => {
    stubFetch(disabled())
    const store = usePluginsStore()

    await store.fetchRegistry()

    expect(store.registryDisabled).toBe(true)
    expect(store.registryError).toBeNull()
    expect(store.registryItems).toEqual([])
  })
})

describe('pluginsStore.fetchRegistryUpdates', () => {
  it('keeps the updates and maps the newer version by plugin id', async () => {
    stubFetch(json([listing]))
    const store = usePluginsStore()

    await store.fetchRegistryUpdates()

    expect(store.registryUpdates).toEqual([listing])
    expect(store.updateVersions).toEqual({ 'example-pay': '2.0.0' })
  })

  it('leaves the list empty on a refusal, with no error shown', async () => {
    stubFetch(problem('Registry unavailable.', 503))
    const store = usePluginsStore()

    await store.fetchRegistryUpdates()

    expect(store.registryUpdates).toEqual([])
    expect(store.registryError).toBeNull()
    expect(store.registryUpdatesError).toBe('Registry unavailable.')
  })

  it('records the registry-disabled answer', async () => {
    stubFetch(disabled())
    const store = usePluginsStore()

    await store.fetchRegistryUpdates()

    expect(store.registryDisabled).toBe(true)
    expect(store.registryUpdates).toEqual([])
  })
})

describe('pluginsStore.installFromRegistry', () => {
  it('posts the version, raises the restart flag and reloads the lists', async () => {
    const mock = stubRoutes({
      'POST /api/admin/plugins/registry/example-pay/install': () => json({ requiresRestart: true, stagedVersion: '2.0.0' }),
      'GET /api/admin/plugins/registry/updates': () => json([]),
      'GET /api/admin/plugins/registry': () => json({ items: [listing], total: 1, pageSize: 10 }),
      'GET /api/admin/plugins': () => json([]),
    })
    const store = usePluginsStore()

    const ok = (await store.installFromRegistry('example-pay', '2.0.0')).ok

    expect(ok).toBe(true)
    expect(store.requiresRestart).toBe(true)
    expect(store.stagedVersion).toBe('2.0.0')
    expect(store.registryInstallingId).toBeNull()
    const post = mock.mock.calls.find(([, init]) => init?.method === 'POST')
    expect(JSON.parse(String(post?.[1]?.body))).toEqual({ version: '2.0.0', allowDowngrade: false })
    expect(requestsOf(mock)).toEqual(expect.arrayContaining([
      'GET /admin/plugins',
      'GET /admin/plugins/registry?page=1',
      'GET /admin/plugins/registry/updates',
    ]))
  })

  it('leaves the restart flag down when no restart is needed', async () => {
    stubRoutes({
      'POST /api/admin/plugins/registry/example-pay/install': () => json({ requiresRestart: false }),
      'GET /api/admin/plugins/registry/updates': () => json([]),
      'GET /api/admin/plugins/registry': () => json({ items: [], total: 0, pageSize: 10 }),
      'GET /api/admin/plugins': () => json([]),
    })
    const store = usePluginsStore()

    expect((await store.installFromRegistry('example-pay', '2.0.0')).ok).toBe(true)
    expect(store.requiresRestart).toBe(false)
    expect(store.stagedVersion).toBeNull()
  })

  it('keeps the API sentence and changes nothing when refused', async () => {
    stubFetch(problem('That publisher is not trusted.', 403))
    const store = usePluginsStore()

    const ok = (await store.installFromRegistry('example-pay', '2.0.0')).ok

    expect(ok).toBe(false)
    expect(store.registryInstallError).toBe('That publisher is not trusted.')
    expect(store.requiresRestart).toBe(false)
    expect(store.registryInstallingId).toBeNull()
  })
})

/** Details of the example plugin, as the details endpoint answers. */
const details: RegistryPluginDetailsDto = {
  ...listing,
  stagedVersion: null,
  requiresLicence: true,
  hasLicenceKey: false,
  buyUrl: null,
  documentationUrl: null,
  supportUrl: null,
  repositoryUrl: null,
  licence: null,
  versions: [],
}

describe('pluginsStore.fetchRegistryDetails', () => {
  it('keeps the details by plugin id with the load state', async () => {
    const mock = stubFetch(json(details))
    const store = usePluginsStore()

    await store.fetchRegistryDetails('example-pay')

    expect(store.registryDetails['example-pay']).toEqual({ details, loading: false, error: null, errorCode: null, notFound: false })
    expect(callOf(mock).url).toBe('/api/admin/plugins/registry/example-pay')
  })

  it('keeps the sentence for that plugin only and the earlier details on show', async () => {
    stubFetch(json(details), problem('Registry unavailable.', 503))
    const store = usePluginsStore()

    await store.fetchRegistryDetails('example-pay')
    await store.fetchRegistryDetails('example-pay')

    expect(store.registryDetails['example-pay']).toEqual({ details, loading: false, error: 'Registry unavailable.', errorCode: 'REFUSED', notFound: false })
    expect(store.registryDetails.other).toBeUndefined()
  })

  it('marks a 404 answer as not found, without a retryable sentence or stale details', async () => {
    stubFetch(json(details), problem('No such plugin.', 404))
    const store = usePluginsStore()

    await store.fetchRegistryDetails('example-pay')
    await store.fetchRegistryDetails('example-pay')

    expect(store.registryDetails['example-pay']).toEqual({ details: null, loading: false, error: null, errorCode: null, notFound: true })
  })

  it('marks the registry not-found code as not found whatever the status', async () => {
    stubFetch(json({ type: PluginErrorCode.RegistryPluginNotFound, title: 'Not found', status: 400, detail: 'No such plugin.' }, 400))
    const store = usePluginsStore()

    await store.fetchRegistryDetails('example-pay')

    expect(store.registryDetails['example-pay']?.notFound).toBe(true)
  })

  it.each([PluginErrorCode.RegistryLicenceRequired, PluginErrorCode.RegistryLicenceInvalid])(
    'keeps the %s code with the sentence so the page can offer the licence key',
    async code => {
      stubFetch(json({ type: code, title: 'Licence', status: 422, detail: 'A licence key is needed.' }, 422))
      const store = usePluginsStore()

      await store.fetchRegistryDetails('example-pay')

      expect(store.registryDetails['example-pay']).toEqual({
        details: null,
        loading: false,
        error: 'A licence key is needed.',
        errorCode: code,
        notFound: false,
      })
    },
  )
})

describe('pluginsStore licence key', () => {
  it('saves the key and reloads the details', async () => {
    const mock = stubRoutes({
      'PUT /api/admin/plugins/registry/example-pay/licence': () => new Response(null, { status: 204 }),
      'GET /api/admin/plugins/registry/example-pay': () => json({ ...details, hasLicenceKey: true }),
    })
    const store = usePluginsStore()

    expect((await store.saveLicenceKey('example-pay', 'abc')).ok).toBe(true)

    const put = mock.mock.calls.find(([, init]) => init?.method === 'PUT')
    expect(JSON.parse(String(put?.[1]?.body))).toEqual({ key: 'abc' })
    expect(store.registryDetails['example-pay']?.details?.hasLicenceKey).toBe(true)
    expect(store.isBusy('example-pay')).toBe(false)
  })

  it('removes the key and reloads the details', async () => {
    const mock = stubRoutes({
      'DELETE /api/admin/plugins/registry/example-pay/licence': () => new Response(null, { status: 204 }),
      'GET /api/admin/plugins/registry/example-pay': () => json(details),
    })
    const store = usePluginsStore()

    expect((await store.removeLicenceKey('example-pay')).ok).toBe(true)
    expect(requestsOf(mock)).toEqual(expect.arrayContaining(['DELETE /admin/plugins/registry/example-pay/licence']))
  })

  it('keeps the API sentence when refused', async () => {
    stubFetch(problem('That key is not valid.', 400))
    const store = usePluginsStore()

    expect((await store.saveLicenceKey('example-pay', 'bad')).ok).toBe(false)
    expect(store.licenceError).toBe('That key is not valid.')
    expect(store.isBusy('example-pay')).toBe(false)
  })
})

describe('pluginsStore busy state', () => {
  it('marks only the installing plugin busy and sends the downgrade flag', async () => {
    let seen: boolean[] = []
    const store = usePluginsStore()
    const mock = stubRoutes({
      'POST /api/admin/plugins/registry/example-pay/install': () => {
        seen = [store.isBusy('example-pay'), store.isBusy('other'), store.registryInstallingId === 'example-pay']
        return json({ requiresRestart: false })
      },
      'GET /api/admin/plugins/registry/updates': () => json([]),
      'GET /api/admin/plugins/registry': () => json({ items: [], total: 0, pageSize: 10 }),
      'GET /api/admin/plugins': () => json([]),
    })

    await store.installFromRegistry('example-pay', '1.0.0', true)

    expect(seen).toEqual([true, false, true])
    expect(store.isBusy('example-pay')).toBe(false)
    expect(store.registryInstallingId).toBeNull()
    const post = mock.mock.calls.find(([, init]) => init?.method === 'POST')
    expect(JSON.parse(String(post?.[1]?.body))).toEqual({ version: '1.0.0', allowDowngrade: true })
  })

  it('reloads the details of a plugin that is on show', async () => {
    const mock = stubRoutes({
      'POST /api/admin/plugins/registry/example-pay/install': () => json({ requiresRestart: false }),
      'GET /api/admin/plugins/registry/updates': () => json([]),
      'GET /api/admin/plugins/registry/example-pay': () => json(details),
      'GET /api/admin/plugins/registry': () => json({ items: [], total: 0, pageSize: 10 }),
      'GET /api/admin/plugins': () => json([]),
    })
    const store = usePluginsStore()
    await store.fetchRegistryDetails('example-pay')

    await store.installFromRegistry('example-pay', '2.0.0')

    const reads = requestsOf(mock).filter(line => line === 'GET /admin/plugins/registry/example-pay')
    expect(reads).toHaveLength(2)
  })
})

describe('pluginsStore per call outcomes', () => {
  it('gives two parallel installs each its own result', async () => {
    stubRoutes({
      'POST /api/admin/plugins/registry/slow-one/install': () => json({ requiresRestart: true, stagedVersion: '3.0.0' }),
      'POST /api/admin/plugins/registry/bad-one/install': () => problem('That publisher is not trusted.', 403),
      'GET /api/admin/plugins/registry/updates': () => json([]),
      'GET /api/admin/plugins/registry': () => json({ items: [], total: 0, pageSize: 10 }),
      'GET /api/admin/plugins': () => json([]),
    })
    const store = usePluginsStore()

    const [first, second] = await Promise.all([
      store.installFromRegistry('slow-one', '3.0.0'),
      store.installFromRegistry('bad-one', '1.0.0'),
    ])

    expect(first).toEqual({ ok: true, stagedVersion: '3.0.0', error: null })
    expect(second).toEqual({ ok: false, stagedVersion: null, error: 'That publisher is not trusted.' })
  })

  it('returns the sentence of a refused licence save and removal', async () => {
    stubFetch(problem('That key is not valid.', 400))
    const store = usePluginsStore()

    expect((await store.saveLicenceKey('example-pay', 'bad')).error).toBe('That key is not valid.')
    expect((await store.removeLicenceKey('example-pay')).error).toBe('That key is not valid.')
  })
})

describe('pluginsStore registry registration', () => {
  /** A verified registration state as the server answers it. */
  const verified = {
    state: 'Verified',
    instanceId: '7a2b6d1e-0000-4000-8000-000000000001',
    baseUrl: 'https://panel.example.com',
    errorCode: null,
    message: null,
    verifiedAt: '2026-11-08T12:00:00Z',
    nextAttemptAt: null,
  }

  it('keeps the registration state the server answers', async () => {
    const mock = stubFetch(json(verified))
    const store = usePluginsStore()

    await store.fetchRegistryInstance()

    expect(store.registryInstance).toEqual(verified)
    expect(store.registryInstanceBusy).toBe(false)
    expect(store.registryInstanceError).toBeNull()
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/plugins/registry-instance', method: 'GET' })
  })

  it('leaves the state empty when the registry is switched off, with no error shown', async () => {
    stubFetch(disabled())
    const store = usePluginsStore()

    await store.fetchRegistryInstance()

    expect(store.registryInstance).toBeNull()
    expect(store.registryInstanceError).toBeNull()
    expect(store.registryDisabled).toBe(true)
  })

  it('keeps the sentence of a refused load and the state it had', async () => {
    stubFetch(json(verified), problem('Registry unavailable.', 503))
    const store = usePluginsStore()
    await store.fetchRegistryInstance()

    await store.fetchRegistryInstance()

    expect(store.registryInstanceError).toBe('Registry unavailable.')
    expect(store.registryInstance).toEqual(verified)
  })

  it('posts the retry and keeps the state it answers, a failed run included', async () => {
    const failed = { ...verified, state: 'Error', errorCode: 'PLUGIN_REGISTRY_INSTANCE_CHALLENGE_UNREACHABLE', message: 'Not reachable.', verifiedAt: null }
    const mock = stubFetch(json(failed))
    const store = usePluginsStore()

    await store.retryRegistryInstance()

    expect(callOf(mock)).toMatchObject({ url: '/api/admin/plugins/registry-instance/retry', method: 'POST' })
    expect(store.registryInstance).toEqual(failed)
    expect(store.registryInstanceError).toBeNull()
    expect(store.registryInstanceBusy).toBe(false)
  })

  it('keeps the sentence of a refused retry', async () => {
    stubFetch(problem('That is not allowed.', 403))
    const store = usePluginsStore()

    await store.retryRegistryInstance()

    expect(store.registryInstance).toBeNull()
    expect(store.registryInstanceError).toBe('That is not allowed.')
  })
})
