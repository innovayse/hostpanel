/**
 * Tests for the plugins store: list, install, remove and restart through the real `useApi`
 * transport. A refused load keeps the API's own sentence in `error`, a refused install or remove in
 * `actionError`; an offline call keeps the shared
 * "no answer" sentence; nothing throws.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { usePluginsStore } from '@/stores/plugins'
import { json, problem, stubFetch, stubOffline } from '../helpers/fetch'

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

describe('pluginsStore.remove', () => {
  it('drops the plugin and flags the restart', async () => {
    stubFetch(json({ requiresRestart: true }))
    const store = usePluginsStore()
    store.plugins.push({ id: 'a' } as never, { id: 'b' } as never)

    await store.remove('a')
    expect(store.plugins.map(p => p.id)).toEqual(['b'])
    expect(store.requiresRestart).toBe(true)
  })

  it('keeps an earlier restart banner when a removal needs no restart', async () => {
    stubFetch(json({ requiresRestart: false }))
    const store = usePluginsStore()
    store.plugins.push({ id: 'a' } as never)
    store.requiresRestart = true

    await store.remove('a')
    expect(store.plugins).toHaveLength(0)
    expect(store.requiresRestart).toBe(true)
  })

  it('does not raise the restart banner when a removal needs no restart', async () => {
    stubFetch(json({ requiresRestart: false }))
    const store = usePluginsStore()
    store.plugins.push({ id: 'a' } as never)

    await store.remove('a')
    expect(store.requiresRestart).toBe(false)
  })

  it('keeps the plugin and the API sentence on refusal', async () => {
    stubFetch(problem('Plugin is in use.', 409))
    const store = usePluginsStore()
    store.plugins.push({ id: 'a' } as never)

    expect(await store.remove('a')).toBe(false)
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
