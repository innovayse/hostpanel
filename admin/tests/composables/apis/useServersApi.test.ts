/**
 * Tests for the servers API composable: paths, verbs and bodies through the real `useApi`
 * transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useServersApi } from '@/composables/apis/useServersApi'
import { ServerModule, ServerState } from '@/types/server'
import { ServerModuleSource } from '@/types/serverenums'
import { json, problem, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useServersApi servers', () => {
  it('listServers reads one page of /admin/servers with the page, its size and the filters', async () => {
    const mock = stubFetch(json({ items: [{ id: 1 }], pageNumber: 2, pageSize: 20, totalCount: 21, totalPages: 2 }))

    const page = await useServersApi().listServers({ page: 2, pageSize: 20, search: 'host', module: 'hestiacp', state: ServerState.Online })

    expect(page.items).toEqual([{ id: 1 }])
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/servers?page=2&pageSize=20&search=host&module=hestiacp&state=online', method: 'GET' })
  })

  it('listServers leaves the filters out of the address when none is set', async () => {
    const mock = stubFetch(json({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 1 }))

    await useServersApi().listServers({ page: 1, pageSize: 20 })
    expect(callOf(mock).url).toBe('/api/admin/servers?page=1&pageSize=20')
  })

  it('listServerOptions reads the unpaged lookup, with the module when one is given', async () => {
    const mock = stubFetch(json([{ id: 1 }]))
    const api = useServersApi()

    expect(await api.listServerOptions()).toEqual([{ id: 1 }])
    await api.listServerOptions(ServerModule.CPanel)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/admin/servers/options', method: 'GET' })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/admin/servers/options?module=CPanel', method: 'GET' })
  })

  it('listServerModules reads the control panel modules a server can be given', async () => {
    const modules = [
      { id: 'CPanel', name: 'CPanel', source: ServerModuleSource.BuiltIn },
      { id: 'hestiacp', name: 'Hestia Control Panel', source: ServerModuleSource.Plugin },
    ]
    const mock = stubFetch(json(modules))

    expect(await useServersApi().listServerModules()).toEqual(modules)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/servers/modules', method: 'GET' })
  })

  it('createServer, updateServer and deleteServer use POST, PUT and DELETE', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useServersApi()

    await api.createServer({ name: 'a' } as never)
    await api.updateServer(2, { name: 'b' } as never)
    await api.deleteServer(2)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/admin/servers', method: 'POST', body: { name: 'a' } })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/admin/servers/2', method: 'PUT', body: { name: 'b' } })
    expect(callOf(mock, 2)).toMatchObject({ url: '/api/admin/servers/2', method: 'DELETE' })
  })

  it('testServerConnection POSTs to test-connection and returns the result', async () => {
    const mock = stubFetch(json({ success: true }))

    expect(await useServersApi().testServerConnection(2)).toEqual({ success: true })
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/servers/2/test-connection', method: 'POST' })
  })

  it('rejects when the API refuses', async () => {
    stubFetch(problem('Server has accounts.', 409))

    await expect(useServersApi().deleteServer(1)).rejects.toBeTruthy()
  })
})

describe('useServersApi groups', () => {
  it('listServerGroups reads /admin/server-groups', async () => {
    const mock = stubFetch(json([]))

    await useServersApi().listServerGroups()
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/server-groups', method: 'GET' })
  })

  it('createServerGroup, updateServerGroup and deleteServerGroup use POST, PUT and DELETE', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useServersApi()

    await api.createServerGroup({ name: 'g' } as never)
    await api.updateServerGroup(4, { name: 'h' } as never)
    await api.deleteServerGroup(4)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/admin/server-groups', method: 'POST', body: { name: 'g' } })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/admin/server-groups/4', method: 'PUT', body: { name: 'h' } })
    expect(callOf(mock, 2)).toMatchObject({ url: '/api/admin/server-groups/4', method: 'DELETE' })
  })
})
