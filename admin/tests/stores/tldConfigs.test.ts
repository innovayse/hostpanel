/**
 * Tests for the TLD configs store: the paged load with its filters, and mutations reporting the outcome as a value.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useTldConfigsStore } from '@/stores/tldConfigs'
import { callOf, json, problem, stubFetch, stubOffline } from '../helpers/fetch'
import { installStoreHooks } from '../helpers/storeHooks'

describe('tldConfigsStore mutations', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('create returns the id and leaves the list to the screen', async () => {
    const fetchMock = stubFetch(json(8))
    const store = useTldConfigsStore()

    expect(await store.create({} as never)).toBe(8)
    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(store.configs).toEqual([])
  })

  it('update, remove and syncPrices return false with the API sentence', async () => {
    stubFetch(problem('TLD is in use.', 409))
    const store = useTldConfigsStore()

    expect(await store.update(1, {} as never)).toBe(false)
    expect(store.actionError).toBe('TLD is in use.')
    expect(await store.remove(1)).toBe(false)
    expect(await store.syncPrices()).toBe(false)
  })

  it('importFromProvider returns the counts, null offline', async () => {
    const store = useTldConfigsStore()
    stubFetch(json({ imported: 2, updated: 1 }))
    expect(await store.importFromProvider('registrar-one')).toEqual({ imported: 2, updated: 1 })

    stubOffline()
    expect(await store.importFromProvider('registrar-one')).toBeNull()
    expect(store.actionError).toBe('Could not reach the server. Check your connection and try again.')
  })
})

describe('tldConfigsStore loads', () => {
  installStoreHooks()

  it('fetchAll fills the page and the total, and sends the page, its size and the trimmed filters', async () => {
    const store = useTldConfigsStore()

    const mock = stubFetch(json({ items: [{ id: 1 }], pageNumber: 2, pageSize: 20, totalCount: 21, totalPages: 2 }))
    expect(await store.fetchAll(2, { search: ' com ', registrarModule: 'registrar-two', enabled: false })).toBe(true)
    expect(store.configs).toEqual([{ id: 1 }])
    expect(store.totalCount).toBe(21)
    expect(callOf(mock).url).toBe('/api/admin/tld-configs?page=2&pageSize=20&search=com&registrarModule=registrar-two&enabled=false')

  })

  it('fetchById returns the config', async () => {
    const store = useTldConfigsStore()

    stubFetch(json({ id: 1, tld: '.com' }))
    expect(await store.fetchById(1)).toEqual({ id: 1, tld: '.com' })
  })

  it('fetchAll reports a refusal with the API sentence and keeps the loaded page', async () => {
    const store = useTldConfigsStore()
    stubFetch(json({ items: [{ id: 1 }], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 }))
    await store.fetchAll(1)

    stubFetch(problem('Page must be at least 1.', 400))
    expect(await store.fetchAll(0)).toBe(false)
    expect(store.error).toBe('Page must be at least 1.')
    expect(store.configs).toEqual([{ id: 1 }])
  })

  it('fetchById returns undefined and keeps the API sentence on refusal', async () => {
    stubFetch(problem('No such TLD.', 404))
    const store = useTldConfigsStore()

    expect(await store.fetchById(1)).toBeUndefined()
    expect(store.error).toBe('No such TLD.')
  })

  it('importFromProvider returns the counts, or null with actionError', async () => {
    const store = useTldConfigsStore()

    stubFetch(json({ imported: 3 }))
    expect(await store.importFromProvider('registrar-two')).toEqual({ imported: 3 })

    stubFetch(problem('Provider down.', 502))
    expect(await store.importFromProvider('registrar-two')).toBeNull()
    expect(store.actionError).toBe('Provider down.')
  })
})
