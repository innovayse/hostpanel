/**
 * Tests for the plugin ui store: the index read once (and again on request), a failed index leaving no
 * menu, the page by its address with its values, the not-found and failed page states, a late page
 * answer dropped, route calls that never throw and count writes, and a stream.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { usePluginUiStore } from '@/stores/pluginUi'
import type { ServerSentEvent } from '@/types/http'
import { json, problem, stubFetch } from '../helpers/fetch'
import { requestsOf, stubRouteTable } from '../helpers/routes'
import { eventStream, ext } from '../helpers/pluginView'

/** The index of the sample plugin. */
const index = {
  menus: [{ pluginId: 'sites', id: 'main', label: 'Sites', page: 'home', icon: 'globe', order: 1 }],
  pages: [{ pluginId: 'sites', id: 'home', path: '/', title: 'Sites' }, { pluginId: 'sites', id: 'site', path: '/sites/{siteId}', title: 'Site' }],
}

/** The page of one site. */
const sitePage = { pluginId: 'sites', id: 'site', path: '/sites/{siteId}', title: 'Site', view: { schema: 1, blocks: [] }, externalOrigins: [] }

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('pluginUi store index', () => {
  it('reads the index once into Add-ons entries, and again when forced', async () => {
    const mock = stubFetch(json(index))
    const store = usePluginUiStore()

    await store.loadUi()
    await store.loadUi()
    expect(mock).toHaveBeenCalledTimes(1)
    expect(store.menu).toEqual([{ key: 'sites:main', label: 'Sites', icon: 'globe', to: '/apps/sites' }])
    expect(store.pages).toHaveLength(2)

    await store.loadUi(true)
    expect(mock).toHaveBeenCalledTimes(2)
  })

  it('leaves the menu empty when the index cannot be read', async () => {
    stubFetch(problem('Forbidden.', 403))
    const store = usePluginUiStore()

    await store.loadUi()

    expect(store.menu).toEqual([])
    expect(store.uiLoaded).toBe(true)
  })
})

describe('pluginUi store page', () => {
  it('reads the page by its address and its values from the template', async () => {
    const mock = stubFetch(json(sitePage))
    const store = usePluginUiStore()

    await store.loadPage('sites', ['sites', '7'])

    expect(String(mock.mock.calls[0]?.[0])).toContain('by-path?path=%2Fsites%2F7')
    expect(store.page?.id).toBe('site')
    expect(store.pageValues).toEqual({ siteId: '7' })
    expect(store.pageLoading).toBe(false)
  })

  it('tells a missing page from a failed read', async () => {
    const store = usePluginUiStore()
    stubFetch(json({ type: 'PLUGIN_PAGE_NOT_FOUND', title: 'Not found', status: 404, detail: 'This page does not exist.' }, 404))
    await store.loadPage('sites', ['nope'])
    expect(store.pageNotFound).toBe(true)
    expect(store.pageError).toBe('This page does not exist.')

    stubFetch(problem('The server is busy.', 503))
    await store.loadPage('sites', [])
    expect(store.pageNotFound).toBe(false)
    expect(store.pageError).toBe('The server is busy.')
  })

  it('finds no page for a segment holding a slash, without asking', async () => {
    const mock = stubFetch(json(sitePage))
    const store = usePluginUiStore()

    await store.loadPage('sites', ['a/b'])

    expect(mock).not.toHaveBeenCalled()
    expect(store.pageNotFound).toBe(true)
  })

  it('drops the answer of a page the operator has left', async () => {
    let releaseOld: (response: Response) => void = () => undefined
    const old = new Promise<Response>((resolve) => {
      releaseOld = resolve
    })
    vi.stubGlobal('fetch', vi.fn((url: string) => (url.includes('old') ? old : Promise.resolve(json({ ...sitePage, id: 'new', path: '/new' })))))
    const store = usePluginUiStore()

    const first = store.loadPage('sites', ['old'])
    await store.loadPage('sites', ['new'])
    releaseOld(json({ ...sitePage, id: 'old', path: '/old' }))
    await first

    expect(store.page?.id).toBe('new')
  })
})

describe('pluginUi store calls', () => {
  it('hands a call outcome back without throwing, and counts every write', async () => {
    stubRouteTable({
      [`GET ${ext('state')}`]: () => json({ live: 1 }),
      [`POST ${ext('sites/7/publish')}`]: () => problem('Nothing to publish.', 409),
    })
    const store = usePluginUiStore()

    const read = await store.callRoute('sites', { method: 'get', path: '/state' }, {})
    expect(read).toEqual({ ok: true, data: { live: 1 }, error: '', fieldErrors: {}, unavailable: false })
    expect(store.writes).toBe(0)

    const write = await store.callRoute('sites', { method: 'post', path: '/sites/{siteId}/publish' }, { siteId: '7' })
    expect(write.ok).toBe(false)
    expect(write.error).toBe('Nothing to publish.')
    expect(store.writes).toBe(1)
  })

  it('sends nothing for a call the page cannot make', async () => {
    const mock = stubRouteTable({})
    const store = usePluginUiStore()

    const result = await store.callRoute('sites', { method: 'post', path: '/sites/{siteId}' }, {})

    expect(result.unavailable).toBe(true)
    expect(mock).not.toHaveBeenCalled()
  })

  it('streams events from a POST route and counts the stream as a write when it opens and ends', async () => {
    const mock = stubRouteTable({ [`POST ${ext('chat')}`]: () => eventStream([{ event: 'delta', data: { text: 'Hi' } }, { event: 'done', data: {} }]) })
    const store = usePluginUiStore()
    const events: ServerSentEvent[] = []

    const result = await store.streamRoute('sites', { method: 'post', path: '/chat' }, {}, { message: 'Hello' }, (event) => events.push(event), new AbortController().signal)

    expect(result.ok).toBe(true)
    expect(events.map((event) => event.type)).toEqual(['delta', 'done'])
    expect(store.writes).toBe(2)
    expect(requestsOf(mock)).toEqual([`POST ${ext('chat')}`])
  })

  it('refuses to stream from a route that is not a POST', async () => {
    const store = usePluginUiStore()

    const result = await store.streamRoute('sites', { method: 'get', path: '/chat' }, {}, {}, () => undefined, new AbortController().signal)

    expect(result.unavailable).toBe(true)
  })
})
