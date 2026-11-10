/**
 * Tests for the plugins store: list, install, remove and restart through the real `useApi`
 * transport. A refused load keeps the API's own sentence in `error`, a refused install or remove in
 * `actionError`; an offline call keeps the shared
 * "no answer" sentence; nothing throws.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { usePluginsStore } from '@/stores/plugins'
import type { PluginListItemDto } from '@/types/plugin'
import { json, problem, stubFetch, stubOffline } from '../helpers/fetch'
import { stubRoutes } from '../helpers/routes'

/** The sentence shown when nothing answered. */
const NO_ANSWER = 'Could not reach the server. Check your connection and try again.'

/** A ZIP standing in for the picked plugin archive. */
const zip = new File(['z'], 'plugin.zip', { type: 'application/zip' })

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('pluginsStore.fetchAll', () => {
  it('keeps the list and clears loading', async () => {
    stubFetch(json([{ id: 'a' }]))
    const store = usePluginsStore()

    await store.fetchAll()
    expect(store.plugins).toEqual([{ id: 'a' }])
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
  })

  it('keeps the API sentence on refusal', async () => {
    stubFetch(problem('Plugins unavailable.', 503))
    const store = usePluginsStore()

    await store.fetchAll()
    expect(store.error).toBe('Plugins unavailable.')
    expect(store.plugins).toEqual([])
  })

  it('keeps the no-answer sentence offline', async () => {
    stubOffline()
    const store = usePluginsStore()

    await store.fetchAll()
    expect(store.error).toBe(NO_ANSWER)
  })
})

describe('pluginsStore.install', () => {
  it('flags the restart and reloads the list', async () => {
    const mock = stubFetch(json({ requiresRestart: true }), json([{ id: 'a' }]))
    const store = usePluginsStore()

    await store.install(zip)
    expect(store.requiresRestart).toBe(true)
    expect(store.plugins).toEqual([{ id: 'a' }])
    expect(mock).toHaveBeenCalledTimes(2)
  })

  it('keeps the API sentence and does not reload on refusal', async () => {
    const mock = stubFetch(problem('Not a plugin archive.', 400))
    const store = usePluginsStore()

    expect(await store.install(zip)).toBe(false)
    expect(store.actionError).toBe('Not a plugin archive.')
    expect(store.error).toBeNull()
    expect(store.requiresRestart).toBe(false)
    expect(mock).toHaveBeenCalledTimes(1)
  })
})

/** What the server reads out of a package that asks for SSH access, as the inspect endpoint answers it. */
const preview = {
  id: 'example-backup',
  name: 'Example Backup',
  version: '1.1.0',
  author: 'Example',
  description: 'Backups',
  hostAccess: ['ssh'],
  capabilities: [],
  changelog: 'Faster restores.',
  minHostVersion: '1.0.0',
  maxHostVersion: null,
  hostVersion: '2.0.0',
  compatible: true,
}

describe('pluginsStore.inspect', () => {
  it('keeps the preview and the archive without installing it', async () => {
    const mock = stubFetch(json(preview))
    const store = usePluginsStore()

    expect(await store.inspect(zip)).toBe(true)
    expect(store.preview).toEqual(preview)
    expect(store.pendingFile).toBe(zip)
    expect(String(mock.mock.calls[0]?.[0])).toBe('/api/admin/plugins/inspect')
    expect(mock).toHaveBeenCalledTimes(1)
  })

  it('keeps the API sentence and no preview on refusal', async () => {
    stubFetch(problem('Not a plugin archive.', 400))
    const store = usePluginsStore()

    expect(await store.inspect(zip)).toBe(false)
    expect(store.actionError).toBe('Not a plugin archive.')
    expect(store.preview).toBeNull()
    expect(store.pendingFile).toBeNull()
  })

  it('installs the waiting archive on confirmation and clears the preview', async () => {
    const mock = stubFetch(json(preview), json({ requiresRestart: true }), json([{ id: 'a' }]))
    const store = usePluginsStore()
    await store.inspect(zip)

    expect(await store.confirmInstall()).toBe(true)
    expect(String(mock.mock.calls[1]?.[0])).toBe('/api/admin/plugins/install')
    expect(store.requiresRestart).toBe(true)
    expect(store.preview).toBeNull()
    expect(store.pendingFile).toBeNull()
  })

  it('installs nothing when the preview is dismissed', async () => {
    const mock = stubFetch(json(preview))
    const store = usePluginsStore()
    await store.inspect(zip)

    store.dismissPreview()

    expect(await store.confirmInstall()).toBe(false)
    expect(mock).toHaveBeenCalledTimes(1)
  })
})

describe('pluginsStore.remove', () => {
  it('marks only that plugin busy while the removal runs and clears it afterwards', async () => {
    let seen: boolean[] = []
    const store = usePluginsStore()
    stubRoutes({
      'DELETE /api/admin/plugins/a': () => {
        seen = [store.isBusy('a'), store.isBusy('b')]
        return json({ requiresRestart: false })
      },
      'GET /api/admin/plugins': () => json([]),
    })

    await store.remove('a')

    expect(seen).toEqual([true, false])
    expect(store.isBusy('a')).toBe(false)
  })

  it('clears the busy mark when the removal is refused', async () => {
    stubFetch(problem('Plugin is in use.', 409))
    const store = usePluginsStore()

    await store.remove('a')

    expect(store.isBusy('a')).toBe(false)
  })

  it('reloads the list and flags the restart', async () => {
    stubFetch(json({ requiresRestart: true }), json([{ id: 'b', status: 'Loaded' }]))
    const store = usePluginsStore()
    store.plugins.push({ id: 'a' } as never, { id: 'b' } as never)

    await store.remove('a')
    expect(store.plugins.map(p => p.id)).toEqual(['b'])
    expect(store.requiresRestart).toBe(true)
  })

  it('reloads the registry listing and the update badges after a removal once the registry was browsed', async () => {
    const mock = stubRoutes({
      'DELETE /api/admin/plugins/a': () => json({ requiresRestart: true }),
      'GET /api/admin/plugins/registry/updates': () => json([]),
      'GET /api/admin/plugins/registry?': () => json({ items: [], total: 0, pageSize: 20 }),
      'GET /api/admin/plugins': () => json([]),
    })
    const store = usePluginsStore()
    store.registryPageSize = 20
    store.registryItems = [{ id: 'a', installed: true } as never]

    await store.remove('a')

    const urls = mock.mock.calls.map(([url]) => String(url))
    expect(urls.some(url => url.startsWith('/api/admin/plugins/registry?'))).toBe(true)
    expect(urls.some(url => url === '/api/admin/plugins/registry/updates')).toBe(true)
    expect(store.registryItems).toEqual([])
  })

  it('does not touch the registry after a removal when it was never browsed', async () => {
    const mock = stubRoutes({
      'DELETE /api/admin/plugins/a': () => json({ requiresRestart: true }),
      'GET /api/admin/plugins': () => json([]),
    })
    const store = usePluginsStore()

    await store.remove('a')

    expect(mock.mock.calls.map(([url]) => String(url)).some(url => url.includes('/registry'))).toBe(false)
  })

  it('lists a removed plugin whose data is kept as a purge target, not as installed', async () => {
    stubFetch(json({ requiresRestart: true }), json([{ id: 'a', status: 'Removed' }, { id: 'b', status: 'Loaded' }]))
    const store = usePluginsStore()

    await store.remove('a')
    expect(store.keptDataIds).toEqual(['a'])
    expect(store.installed.map(p => p.id)).toEqual(['b'])
  })

  it('keeps an earlier restart banner when a removal needs no restart', async () => {
    stubFetch(json({ requiresRestart: false }), json([]))
    const store = usePluginsStore()
    store.plugins.push({ id: 'a' } as never)
    store.requiresRestart = true

    await store.remove('a')
    expect(store.plugins).toHaveLength(0)
    expect(store.requiresRestart).toBe(true)
  })

  it('does not raise the restart banner when a removal needs no restart', async () => {
    stubFetch(json({ requiresRestart: false }), json([]))
    const store = usePluginsStore()
    store.plugins.push({ id: 'a' } as never)

    await store.remove('a')
    expect(store.requiresRestart).toBe(false)
  })

  it('keeps the plugin and the API sentence on refusal', async () => {
    stubFetch(problem('Plugin is in use.', 409))
    const store = usePluginsStore()
    store.plugins.push({ id: 'a' } as never)

    expect((await store.remove('a')).ok).toBe(false)
    expect(store.plugins).toHaveLength(1)
    expect(store.actionError).toBe('Plugin is in use.')
  })
})

describe('pluginsStore.restart', () => {
  it('clears the pending restart', async () => {
    stubFetch(new Response(null, { status: 204 }))
    const store = usePluginsStore()
    store.requiresRestart = true

    await store.restart()
    expect(store.requiresRestart).toBe(false)
  })

  it('treats a dropped connection as expected, not as an error', async () => {
    stubOffline()
    const store = usePluginsStore()
    store.requiresRestart = true

    await store.restart()
    expect(store.requiresRestart).toBe(false)
    expect(store.error).toBeNull()
    expect(store.loading).toBe(false)
  })
})

/** A slot widget as the list endpoint returns it. */
const widget = {
  slotId: 'admin.service.panels', widgetId: 'usage', title: 'Usage', manifestProductIds: [], productIds: [3], isBound: true,
}

/** An installed plugin that is loaded, so its widgets are listed. */
const loadedPlugin = {
  id: 'acme', name: 'Acme', version: '1.0.0', author: 'A', description: '', type: 'hosted', category: '', color: '#000000', iconUrl: null,
  isLoaded: true, isBuiltIn: false,
} as PluginListItemDto

describe('pluginsStore.fetchSlotWidgets', () => {
  it('keeps the widgets of each loaded plugin and the products they can be bound to', async () => {
    const mock = stubFetch(json([widget]), json([{ id: 3, name: 'Shared' }]))
    const store = usePluginsStore()
    store.plugins = [loadedPlugin, { ...loadedPlugin, id: 'idle', isLoaded: false }]

    await store.fetchSlotWidgets()
    expect(store.slotWidgets).toEqual({ acme: [widget] })
    expect(store.productOptions).toEqual([{ id: 3, name: 'Shared' }])
    expect(store.slotWidgetsError).toBeNull()
    expect(String(mock.mock.calls[0]?.[0])).toBe('/api/admin/plugins/acme/slot-widgets')
  })

  it('keeps the API sentence and skips the products when the widgets are refused', async () => {
    const mock = stubFetch(problem('Widgets unavailable.', 503))
    const store = usePluginsStore()
    store.plugins = [loadedPlugin]

    await store.fetchSlotWidgets()
    expect(store.slotWidgetsError).toBe('Widgets unavailable.')
    expect(mock).toHaveBeenCalledTimes(1)
  })

  it('keeps the API sentence when the products are refused', async () => {
    stubFetch(json([widget]), problem('Products unavailable.', 503))
    const store = usePluginsStore()
    store.plugins = [loadedPlugin]

    await store.fetchSlotWidgets()
    expect(store.slotWidgetsError).toBe('Products unavailable.')
    expect(store.productOptions).toEqual([])
  })
})

describe('pluginsStore.saveSlotWidgetProducts', () => {
  it('puts the ids on the widget route, then reloads the widgets', async () => {
    const mock = stubFetch(json({ ...widget, productIds: [3, 4] }), json([{ ...widget, productIds: [3, 4] }]), json([]))
    const store = usePluginsStore()
    store.plugins = [loadedPlugin]

    const ok = await store.saveSlotWidgetProducts('acme', widget, [3, 4])
    expect(ok).toBe(true)
    const [url, init] = mock.mock.calls[0] as [string, RequestInit]
    expect(url).toBe('/api/admin/plugins/acme/slot-widgets/admin.service.panels/usage')
    expect(init.method).toBe('PUT')
    expect(JSON.parse(String(init.body))).toEqual({ productIds: [3, 4] })
    expect(store.slotWidgets['acme']?.[0]?.productIds).toEqual([3, 4])
  })

  it('keeps the API sentence in bindingError on refusal', async () => {
    stubFetch(problem('Too many products.'))
    const store = usePluginsStore()

    const ok = await store.saveSlotWidgetProducts('acme', widget, [1])
    expect(ok).toBe(false)
    expect(store.bindingError).toBe('Too many products.')
  })

  it('shows the API sentence when every chosen product is unknown (400 PLUGIN_SLOT_PRODUCTS_UNKNOWN)', async () => {
    stubFetch(json({ type: 'PLUGIN_SLOT_PRODUCTS_UNKNOWN', title: 'Unknown', status: 400, detail: 'None of the products exist.' }, 400))
    const store = usePluginsStore()

    const ok = await store.saveSlotWidgetProducts('acme', widget, [99])
    expect(ok).toBe(false)
    expect(store.bindingError).toBe('None of the products exist.')
  })

  it('keeps isBound from the reloaded widgets', async () => {
    stubFetch(json({ ...widget, productIds: [] }), json([{ ...widget, productIds: [], isBound: true }]), json([]))
    const store = usePluginsStore()
    store.plugins = [loadedPlugin]

    await store.saveSlotWidgetProducts('acme', widget, [])
    expect(store.slotWidgets['acme']?.[0]).toMatchObject({ isBound: true, productIds: [] })
  })
})

describe('pluginsStore upgrade and downgrade', () => {
  /** The refusal the server gives an upload older than the installed version. */
  const downgrade = (): Response =>
    json({ type: 'PLUGIN_DOWNGRADE_REFUSED', title: 'Refused', status: 409, detail: 'Older than installed.' }, 409)

  it('keeps the staged version the server answered with', async () => {
    stubFetch(json({ requiresRestart: true, stagedVersion: '1.3.0' }), json([]))
    const store = usePluginsStore()

    expect(await store.install(zip)).toBe(true)
    expect(store.stagedVersion).toBe('1.3.0')
    expect(store.requiresRestart).toBe(true)
  })

  it('has no staged version for a plain install', async () => {
    stubFetch(json({ requiresRestart: true }), json([]))
    const store = usePluginsStore()

    await store.install(zip)
    expect(store.stagedVersion).toBeNull()
  })

  it('keeps the refused archive and the API sentence', async () => {
    stubFetch(downgrade())
    const store = usePluginsStore()

    expect(await store.install(zip)).toBe(false)
    expect(store.actionError).toBe('Older than installed.')
    expect(store.downgradeFile).toBe(zip)
  })

  it('does not keep the archive for any other refusal', async () => {
    stubFetch(problem('Not a plugin archive.', 400))
    const store = usePluginsStore()

    await store.install(zip)
    expect(store.downgradeFile).toBeNull()
  })

  it('sends the kept archive with force and clears it', async () => {
    const mock = stubFetch(downgrade(), json({ requiresRestart: true }), json([]))
    const store = usePluginsStore()
    await store.install(zip)

    expect(await store.forceDowngrade()).toBe(true)
    expect(String(mock.mock.calls[1]?.[0])).toBe('/api/admin/plugins/install?force=true')
    expect(store.downgradeFile).toBeNull()
    expect(store.requiresRestart).toBe(true)
  })

  it('does nothing when no refused archive waits', async () => {
    const mock = stubFetch(json({ requiresRestart: true }))
    const store = usePluginsStore()

    expect(await store.forceDowngrade()).toBe(false)
    expect(mock).not.toHaveBeenCalled()
  })

  it('forgets the refused archive when the operator dismisses it', async () => {
    stubFetch(downgrade())
    const store = usePluginsStore()
    await store.install(zip)

    store.dismissDowngrade()
    expect(store.downgradeFile).toBeNull()
  })
})

describe('pluginsStore.purge', () => {
  it('deletes the kept data and reloads the list', async () => {
    const mock = stubFetch(json({ requiresRestart: false }), json([]))
    const store = usePluginsStore()
    store.plugins.push({ id: 'a', status: 'Removed' } as never)

    expect(await store.purge('a')).toBe(true)
    expect(String(mock.mock.calls[0]?.[0])).toBe('/api/admin/plugins/a/data')
    expect(mock.mock.calls[0]?.[1]?.method).toBe('DELETE')
    expect(store.keptDataIds).toEqual([])
  })

  it('keeps the row and the API sentence on refusal', async () => {
    stubFetch(problem('Plugin is still installed.', 409))
    const store = usePluginsStore()
    store.plugins.push({ id: 'a', status: 'Removed' } as never)

    expect(await store.purge('a')).toBe(false)
    expect(store.actionError).toBe('Plugin is still installed.')
    expect(store.keptDataIds).toEqual(['a'])
  })
})

describe('pluginsStore.remove outcome', () => {
  it('returns the sentence of its own call and leaves the shared loading flag alone', async () => {
    let loadingDuringCall: boolean | null = null
    const store = usePluginsStore()
    stubRoutes({
      'DELETE /api/admin/plugins/a': () => {
        loadingDuringCall = store.loading
        return problem('Plugin is in use.', 409)
      },
    })

    const outcome = await store.remove('a')

    expect(outcome).toEqual({ ok: false, stagedVersion: null, error: 'Plugin is in use.' })
    expect(loadingDuringCall).toBe(false)
  })
})
