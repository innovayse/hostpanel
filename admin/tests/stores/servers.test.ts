/**
 * Tests for the servers store: the paged list, the unpaged option lookup, the group list, and the failure-as-state
 * actions. A write answers whether it was accepted and reads no list again: the screen reloads the page it shows.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useServersStore } from '@/stores/servers'
import { translate } from '@/lib/translate'
import { ServerModule, ServerState } from '@/types/server'
import { json, problem, stubFetch, stubOffline } from '../helpers/fetch'
import { installStoreHooks } from '../helpers/storeHooks'

/**
 * The address the last call went to.
 *
 * @param mock - The stubbed fetch.
 * @returns The request URL.
 */
const urlOf = (mock: ReturnType<typeof vi.fn>): string => String(mock.mock.calls[0]?.[0])

describe('serversStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetchServers fills the page and the total, and sends the page, its size and the trimmed filters', async () => {
    const mock = stubFetch(json({ items: [{ id: 1 }], pageNumber: 2, pageSize: 20, totalCount: 21, totalPages: 2 }))
    const store = useServersStore()

    expect(await store.fetchServers(2, { search: ' host ', module: ServerModule.Plesk, state: ServerState.Offline })).toBe(true)
    expect(urlOf(mock)).toBe('/api/admin/servers?page=2&pageSize=20&search=host&module=Plesk&state=offline')
    expect(store.servers).toEqual([{ id: 1 }])
    expect(store.totalCount).toBe(21)
    expect(store.loading).toBe(false)
  })

  it('fetchServers reports the API sentence, and the no-answer sentence offline', async () => {
    const store = useServersStore()
    stubFetch(problem('Forbidden.', 403))
    expect(await store.fetchServers(1)).toBe(false)
    expect(store.error).toBe('Forbidden.')
    expect(store.loading).toBe(false)

    stubOffline()
    expect(await store.fetchServers(1)).toBe(false)
    expect(store.error).toBe(translate('errors.noAnswer'))
  })

  it('fetchServerOptions fills the lean lookup, with the module when one is asked for', async () => {
    const mock = stubFetch(json([{ id: 1, name: 'host1' }]))
    const store = useServersStore()

    const pending = store.fetchServerOptions(ServerModule.Cwp7)
    expect(store.optionsLoading).toBe(true)
    expect(store.loading).toBe(false)
    await pending
    expect(store.optionsLoading).toBe(false)
    expect(urlOf(mock)).toBe('/api/admin/servers/options?module=Cwp7')
    expect(store.serverOptions).toEqual([{ id: 1, name: 'host1' }])

    const every = stubFetch(json([]))
    await store.fetchServerOptions()
    expect(urlOf(every)).toBe('/api/admin/servers/options')
    expect(store.serverOptions).toEqual([])
  })

  it('fetchServerOptions keeps the API sentence on refusal and leaves the lookup as it was', async () => {
    const store = useServersStore()
    stubFetch(json([{ id: 1 }]))
    await store.fetchServerOptions()

    stubFetch(problem('Not allowed.', 403))
    await store.fetchServerOptions()

    expect(store.error).toBe('Not allowed.')
    expect(store.serverOptions).toEqual([{ id: 1 }])
    expect(store.optionsLoading).toBe(false)
  })

  it('createServer returns true and reads no list again', async () => {
    const mock = stubFetch(json({}))
    const store = useServersStore()

    expect(await store.createServer({} as never)).toBe(true)
    expect(mock).toHaveBeenCalledTimes(1)
  })

  it('a refused deleteServer returns false with the API sentence', async () => {
    const mock = stubFetch(problem('Server has accounts.', 409))
    const store = useServersStore()

    expect(await store.deleteServer(1)).toBe(false)
    expect(store.actionError).toBe('Server has accounts.')
    expect(mock).toHaveBeenCalledTimes(1)
  })

  it('testConnection returns the result, null when the request is refused', async () => {
    const store = useServersStore()
    const mock = stubFetch(json({ connected: true }))
    expect(await store.testConnection(1)).toEqual({ connected: true })
    expect(mock).toHaveBeenCalledTimes(1)

    stubOffline()
    expect(await store.testConnection(1)).toBeNull()
    expect(store.actionError).toBe('Could not reach the server. Check your connection and try again.')
  })

  it('group writes answer whether they were accepted and read no list again', async () => {
    const mock = stubFetch(json({}))
    const store = useServersStore()

    expect(await store.createGroup({} as never)).toBe(true)
    expect(mock).toHaveBeenCalledTimes(1)
  })
})

describe('serversStore loads', () => {
  installStoreHooks()

  it('fetchGroups fills the groups and a refusal keeps the API sentence', async () => {
    const store = useServersStore()

    stubFetch(json([{ id: 1 }]))
    await store.fetchGroups()
    expect(store.groups).toEqual([{ id: 1 }])

    stubFetch(problem('Groups locked.', 423))
    await store.fetchGroups()
    expect(store.error).toBe('Groups locked.')
    expect(store.loading).toBe(false)
  })

  it('updateServer, updateGroup and deleteGroup send the write to the member address', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const store = useServersStore()

    expect(await store.updateServer(4, {} as never)).toBe(true)
    expect(await store.updateGroup(5, {} as never)).toBe(true)
    expect(await store.deleteGroup(5)).toBe(true)

    expect(mock.mock.calls.map(([url, init]) => `${(init as RequestInit).method} ${String(url)}`)).toEqual([
      'PUT /api/admin/servers/4',
      'PUT /api/admin/server-groups/5',
      'DELETE /api/admin/server-groups/5',
    ])
  })
})
