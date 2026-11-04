/**
 * Tests for the registry half of the plugins store: the page load with its filter, the filter reset
 * rules, the registry-disabled answer, the update list and the install from the registry. Real
 * `useApi` transport, `fetch` stubbed.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { usePluginsStore } from '@/stores/plugins'
import { PluginCategory, PluginErrorCode, PluginPricing } from '@/types/plugin'
import type { PluginRegistryItemDto } from '@/types/plugin'
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

    const ok = await store.installFromRegistry('example-pay', '2.0.0')

    expect(ok).toBe(true)
    expect(store.requiresRestart).toBe(true)
    expect(store.stagedVersion).toBe('2.0.0')
    expect(store.registryInstallingId).toBeNull()
    const post = mock.mock.calls.find(([, init]) => init?.method === 'POST')
    expect(JSON.parse(String(post?.[1]?.body))).toEqual({ version: '2.0.0' })
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

    expect(await store.installFromRegistry('example-pay', '2.0.0')).toBe(true)
    expect(store.requiresRestart).toBe(false)
    expect(store.stagedVersion).toBeNull()
  })

  it('keeps the API sentence and changes nothing when refused', async () => {
    stubFetch(problem('That publisher is not trusted.', 403))
    const store = usePluginsStore()

    const ok = await store.installFromRegistry('example-pay', '2.0.0')

    expect(ok).toBe(false)
    expect(store.registryInstallError).toBe('That publisher is not trusted.')
    expect(store.requiresRestart).toBe(false)
    expect(store.registryInstallingId).toBeNull()
  })
})
