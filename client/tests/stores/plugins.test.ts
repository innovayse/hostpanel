/**
 * Tests for `stores/plugins.ts`, through the real API composable and transport against a stub server:
 * the index read once and resolved to menu links, a page read for an address with its parameter
 * values, the API's not-found answer, route calls that hand their outcome to the caller and never
 * call for a reference the page cannot fill, streams read event by event, stopped by an abort, a reset
 * on sign-out, and the slot reads, whose failures are silent by design and touch no page state.
 *
 * Nothing of our own is replaced: the stub plays the BFF, so a refusal arrives in the BFF's own
 * error shape (`statusMessage` and `data.code`) and goes through `lib/errorHandler` as in the browser.
 *
 * @module tests/stores/plugins.test
 */

import { createServer } from 'node:http'
import type { IncomingMessage, Server, ServerResponse } from 'node:http'
import type { AddressInfo } from 'node:net'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { startHeldServer } from '../helpers/heldServer'
import { startTransport } from '../helpers/stubTransport'
import type { StubReply, StubRequest, StubServer } from '../helpers/stubServer'
import { usePluginsStore } from '~/stores/plugins'
import type { PluginStreamEvent } from '~/types/pluginstreamevent'
import type { PluginUi } from '~/types/pluginui'
import { PluginSlotId } from '~/types/pluginslotid'
import { SERVICE_NOT_FOUND, slotIndex, slotPath, slotWidgetPath, WIDGET_NOT_FOUND, workingView } from '~/tests/helpers/pluginSlotFixture'
import { queryOf, requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'

/** The stub playing the BFF. */
let stub: StubServer

/** The real `fetch`, which the stream tests point at a local server. */
const realFetch = globalThis.fetch

/**
 * Wraps a streaming server's handler so it answers like the BFF would to a DOM client: a preflight (the store sends
 * the CSRF and language headers) at once, and every answer with a permissive origin header, standing in for the
 * same origin the stream has in production.
 *
 * @param handler - The handler of the real request.
 * @returns The handler the server is created with.
 */
const crossOrigin = (handler: (request: IncomingMessage, res: ServerResponse) => void) =>
  (request: IncomingMessage, res: ServerResponse): void => {
    res.setHeader('access-control-allow-origin', '*')
    if (request.method === 'OPTIONS') {
      res.writeHead(204, { 'access-control-allow-methods': 'POST', 'access-control-allow-headers': String(request.headers['access-control-request-headers'] ?? '*') })
      res.end()
      return
    }
    handler(request, res)
  }

/** The index the BFF answers: a list page and a page that takes a site id, one menu item each. */
const INDEX: PluginUi = {
  menus: [
    { pluginId: 'acme-sites', id: 'site', label: 'One site', page: 'site', icon: null, order: 5 },
    { pluginId: 'acme-sites', id: 'sites', label: 'My sites', page: 'sites', icon: 'globe', order: 1 },
  ],
  pages: [
    { pluginId: 'acme-sites', id: 'sites', path: '/sites', title: 'My sites' },
    { pluginId: 'acme-sites', id: 'site', path: '/sites/{siteId}', title: 'One site' },
  ],
}

/** The page view the BFF answers for `/sites/{siteId}`. */
const SITE_PAGE = {
  pluginId: 'acme-sites',
  id: 'site',
  path: '/sites/{siteId}',
  title: 'One site',
  view: { schema: 1, blocks: [] },
  externalOrigins: [],
}

/** A GET route reference of the test plugin, as a view names it. */
const GET_SITE = { method: 'get', path: '/sites/{siteId}' }

/** The streaming route reference of the test plugin. */
const CHAT = { method: 'post', path: '/sites/{siteId}/chat' }

/**
 * Whether a request reads a plugin page through the BFF's page lookup by address.
 *
 * @param request - The request the stub received.
 * @returns True for a page read of the test plugin.
 */
const isPageRead = (request: StubRequest): boolean =>
  request.method === 'GET' && /^\/api\/portal\/client\/plugins\/acme-sites\/page\?/.test(request.url)

/**
 * Answers the index and the page; anything else answers `fallback`.
 *
 * @param fallback - The answer for a request that is neither.
 * @returns The stub's reply function.
 */
const answering = (fallback: (request: StubRequest) => StubReply = () => ({ body: {} })) => (request: StubRequest): StubReply => {
  if (request.url === '/api/portal/client/plugins/ui') {
    return { body: INDEX }
  }
  if (isPageRead(request)) {
    return { body: SITE_PAGE }
  }
  return fallback(request)
}

/**
 * The requests the stub received for a plugin's routes.
 *
 * @returns The route calls, oldest first.
 */
const routeCalls = (): StubRequest[] => stub.requests.filter(request => request.url.includes('/routes/'))

beforeEach(async () => {
  setActivePinia(createPinia())
  stub = await startTransport(answering())
})

afterEach(async () => {
  vi.stubGlobal('fetch', realFetch)
  await stub.close()
})

describe('stores/plugins: the index', () => {
  it('reads the index once and lists its menu items as page links, by order', async () => {
    const store = usePluginsStore()

    await store.loadUi()
    await store.loadUi()

    expect(stub.requests.filter(request => request.url === '/api/portal/client/plugins/ui')).toHaveLength(1)
    expect(store.pages).toEqual(INDEX.pages)
    // The page that takes a site id cannot be opened from a menu, so only the list page is linked.
    expect(store.menu).toEqual([{ label: 'My sites', icon: 'globe', url: '/client/apps/acme-sites/sites' }])
  })

  it('reads again when forced, and empties the menu when the read is refused', async () => {
    const store = usePluginsStore()
    await store.loadUi()

    stub.respondWith(() => ({ status: 404, body: { statusMessage: 'No client.', data: { code: 'CLIENT_PROFILE_NOT_FOUND' } } }))
    await store.loadUi(true)

    expect(store.menu).toEqual([])
    expect(store.pages).toEqual([])
    expect(store.uiLoaded).toBe(true)
  })
})

describe('stores/plugins: a page', () => {
  it('drops the answer to an older read when a newer one was asked for meanwhile', async () => {
    const server = await startHeldServer()
    try {
      const store = usePluginsStore()
      const first = store.loadPage('acme-sites', ['sites', 'old'])
      const second = store.loadPage('acme-sites', ['sites', 'new'])
      await vi.waitFor(() => expect(server.held).toHaveLength(2))

      server.held[1]?.answer({ ...SITE_PAGE, title: 'New' })
      await second
      server.held[0]?.answer({ ...SITE_PAGE, title: 'Old' })
      await first

      expect(store.page?.title).toBe('New')
      expect(store.pageValues).toEqual({ siteId: 'new' })
      expect(store.pageLoading).toBe(false)
    } finally {
      await server.close()
    }
  })

  it('reads the page an address names, with the parameter values from the address', async () => {
    const store = usePluginsStore()

    await store.loadPage('acme-sites', ['sites', 'blog'])

    expect(store.page).toEqual(SITE_PAGE)
    expect(store.pageValues).toEqual({ siteId: 'blog' })
    expect(store.pageNotFound).toBe(false)
    expect(store.pageError).toBe('')
    expect(store.pageLoading).toBe(false)
  })

  it('sends a non-Latin address decoded, so it is encoded once on the way', async () => {
    const store = usePluginsStore()

    await store.loadPage('acme-sites', ['sites', 'կայք'])
    await store.loadPage('acme-sites', ['sites', 'сайт'])

    const reads = stub.requests.filter(isPageRead).map(request => new URL(request.url, 'http://h').searchParams.get('path'))
    expect(reads).toEqual(['/sites/կայք', '/sites/сайт'])
    expect(store.pageValues).toEqual({ siteId: 'сайт' })
  })

  it('treats an address the panel\'s server refuses as a missing page', async () => {
    stub.respondWith(request => isPageRead(request)
      ? { status: 400, body: { data: { code: 'BAD_ROUTE_PARAM' } } }
      : answering()(request))
    const store = usePluginsStore()

    await store.loadPage('acme-sites', ['sites', '..'])

    expect(store.pageNotFound).toBe(true)
  })

  it('asks nothing for a segment holding a slash, which names no page', async () => {
    const store = usePluginsStore()

    await store.loadPage('acme-sites', ['sites', 'a/b'])

    expect(store.pageNotFound).toBe(true)
    expect(store.pageLoading).toBe(false)
    expect(stub.requests.filter(isPageRead)).toHaveLength(0)
  })

  it('reads the page in the reader\'s language', async () => {
    const store = usePluginsStore()

    await store.loadPage('acme-sites', ['sites', 'blog'])

    expect(stub.requests.find(isPageRead)?.headers['x-language']).toBe('en')
  })

  it('shows the API\'s own sentence for a page that is not there, and no page', async () => {
    stub.respondWith(request => isPageRead(request)
      ? { status: 404, body: { statusMessage: 'This page does not exist.', data: { code: 'PLUGIN_PAGE_NOT_FOUND' } } }
      : answering()(request))
    const store = usePluginsStore()

    await store.loadPage('acme-sites', ['nothing', 'here'])

    expect(store.page).toBeNull()
    expect(store.pageNotFound).toBe(true)
    expect(store.pageError).toBe('This page does not exist.')
  })

  it('tells a failed read from a missing page, and clears the last page on the next read', async () => {
    const store = usePluginsStore()
    await store.loadPage('acme-sites', ['sites', 'blog'])
    stub.respondWith(request => isPageRead(request)
      ? { status: 503, body: { statusMessage: 'Try again shortly.', data: { code: 'IDENTITY_PLUGIN_UNAVAILABLE' } } }
      : answering()(request))

    await store.loadPage('acme-sites', ['sites', 'blog'])

    expect(store.page).toBeNull()
    expect(store.pageValues).toEqual({})
    expect(store.pageNotFound).toBe(false)
    expect(store.pageError).toBe('Try again shortly.')
  })
})

describe('stores/plugins: route calls', () => {
  it('fills a route from the values, sends a query on a read and no body', async () => {
    stub.respondWith(answering(() => ({ body: { status: 'live' } })))
    const store = usePluginsStore()

    const result = await store.callRoute('acme-sites', GET_SITE, { siteId: 'a/b' }, { ignored: true }, { page: '2' })

    expect(result).toEqual({ ok: true, data: { status: 'live' }, error: '', fieldErrors: {}, unavailable: false })
    expect(routeCalls()[0]).toMatchObject({ method: 'GET', url: '/api/portal/client/plugins/acme-sites/routes/sites/a%2Fb?page=2', body: '' })
  })

  it.each([
    ['post', 'POST'],
    ['put', 'PUT'],
    ['patch', 'PATCH'],
    ['POST', 'POST'],
  ])('sends a body with %s and calls it %s', async (method: string, wire: string) => {
    stub.respondWith(answering(() => ({ status: 204 })))
    const store = usePluginsStore()

    const result = await store.callRoute('acme-sites', { method, path: '/sites/{siteId}' }, { siteId: '7' }, { name: 'Blog' })

    expect(result.ok).toBe(true)
    expect(result.data).toBeNull()
    expect(routeCalls()[0]?.method).toBe(wire)
    expect(JSON.parse(routeCalls()[0]?.body ?? '')).toEqual({ name: 'Blog' })
  })

  it('sends an empty object to a write that was given no body, and none with a delete', async () => {
    stub.respondWith(answering(() => ({ status: 204 })))
    const store = usePluginsStore()

    await store.callRoute('acme-sites', { method: 'post', path: '/sites/{siteId}/publish' }, { siteId: '7' })
    await store.callRoute('acme-sites', { method: 'delete', path: '/sites/{siteId}' }, { siteId: '7' }, { ignored: true })

    expect(JSON.parse(routeCalls()[0]?.body ?? '')).toEqual({})
    expect(routeCalls()[1]).toMatchObject({ method: 'DELETE', body: '' })
  })

  it('counts the writes that ended, a refused one included, and never a read', async () => {
    let refuse = false
    stub.respondWith(answering(() => (refuse ? { status: 400, body: { statusMessage: 'No.', data: { code: 'VALIDATION_ERROR' } } } : { status: 204 })))
    const store = usePluginsStore()
    expect(store.writes).toBe(0)

    await store.callRoute('acme-sites', GET_SITE, { siteId: '7' })
    expect(store.writes).toBe(0)

    await store.callRoute('acme-sites', { method: 'post', path: '/sites/{siteId}/publish' }, { siteId: '7' })
    refuse = true
    await store.callRoute('acme-sites', { method: 'delete', path: '/sites/{siteId}' }, { siteId: '7' })

    expect(store.writes).toBe(2)
  })

  it('hands back a refusal with the API\'s sentence and its field errors', async () => {
    stub.respondWith(answering(() => ({ status: 400, body: { statusMessage: 'Check the form.', data: { code: 'VALIDATION_ERROR', errors: { name: ['Too long.'] } } } })))
    const store = usePluginsStore()

    const result = await store.callRoute('acme-sites', { method: 'post', path: '/sites' }, {}, { name: 'x' })

    expect(result).toMatchObject({ ok: false, data: null, fieldErrors: { name: 'Too long.' }, unavailable: false })
    expect(result.error).not.toBe('')
  })

  it.each([
    [GET_SITE, {}, 'a parameter the address does not carry'],
    [GET_SITE, { siteId: '' }, 'an empty parameter'],
    [{ method: 'head', path: '/sites' }, {}, 'a method the host does not call'],
    [{ method: 'toString', path: '/sites' }, {}, 'an inherited name'],
  ])('calls nothing for %j with %j (%s) and says nothing', async (route, values, _reason: string) => {
    const store = usePluginsStore()

    const result = await store.callRoute('acme-sites', route, values as Record<string, string>)

    expect(result).toEqual({ ok: false, data: null, error: '', fieldErrors: {}, unavailable: true })
    expect(routeCalls()).toHaveLength(0)
  })
})

describe('stores/plugins: streams', () => {
  /**
   * Points the browser's `fetch` at the stub, for the stream the store opens with it.
   */
  const fetchFromStub = (): void => {
    vi.stubGlobal('fetch', (url: string, init: RequestInit) => realFetch(`${stub.baseUrl}${url}`, init))
  }

  it('reads a stream event by event and ends without a failure', async () => {
    stub.respondWith(() => ({ headers: { 'content-type': 'text/event-stream' }, body: 'event: delta\ndata: {"text":"Hi"}\n\nevent: done\ndata: {}\n\n' }))
    fetchFromStub()
    const store = usePluginsStore()
    const events: PluginStreamEvent[] = []

    const result = await store.streamRoute('acme-sites', CHAT, { siteId: '7' }, { message: 'hi' }, event => events.push(event), new AbortController().signal)

    expect(result).toMatchObject({ ok: true, error: '', unavailable: false })
    expect(events).toEqual([{ type: 'delta', data: { text: 'Hi' } }, { type: 'done', data: {} }])
    // A stream is a turn of work, so it counts as a write once it ended.
    expect(store.writes).toBe(1)
    const sent = stub.requests.at(-1)!
    expect(sent.url).toBe('/api/portal/client/plugins/acme-sites/stream/sites/7/chat')
    expect(JSON.parse(sent.body)).toEqual({ message: 'hi' })
  })

  it('answers a refused stream with the API\'s sentence', async () => {
    stub.respondWith(() => ({ status: 429, body: { statusMessage: 'Too many requests.', data: { code: 'RATE_LIMITED' } } }))
    fetchFromStub()
    const store = usePluginsStore()

    const result = await store.streamRoute('acme-sites', CHAT, { siteId: '7' }, {}, () => undefined, new AbortController().signal)

    expect(result).toMatchObject({ ok: false, error: 'Too many requests.', unavailable: false })
    expect(store.writes).toBe(1)
  })

  it('opens nothing for a send route that is not a POST, or that the address cannot fill', async () => {
    fetchFromStub()
    const store = usePluginsStore()

    const notPost = await store.streamRoute('acme-sites', { method: 'get', path: '/chat' }, {}, {}, () => undefined, new AbortController().signal)
    const unfilled = await store.streamRoute('acme-sites', CHAT, {}, {}, () => undefined, new AbortController().signal)

    expect(notPost.unavailable).toBe(true)
    expect(unfilled.unavailable).toBe(true)
    expect(stub.requests.filter(request => request.url.includes('/stream/'))).toHaveLength(0)
  })

  it('stops on abort with no failure, hands over nothing after it, and closes the connection', async () => {
    /** The open response of the streaming server. */
    let response: ServerResponse | undefined
    /** Whether the server saw the connection close. */
    let closed = false
    const server: Server = createServer(crossOrigin((_request, res) => {
      res.writeHead(200, { 'content-type': 'text/event-stream' })
      res.write('event: delta\ndata: {"text":"a"}\n\n')
      res.on('close', () => {
        closed = true
      })
      response = res
    }))
    await new Promise<void>(resolve => server.listen(0, '127.0.0.1', resolve))
    const base = `http://127.0.0.1:${(server.address() as AddressInfo).port}`
    vi.stubGlobal('fetch', (url: string, init: RequestInit) => realFetch(`${base}${url}`, init))
    const store = usePluginsStore()
    const controller = new AbortController()
    const events: PluginStreamEvent[] = []
    try {
      const result = await store.streamRoute('acme-sites', CHAT, { siteId: '7' }, {}, (event) => {
        events.push(event)
        controller.abort()
        // Written after the abort: the reader must not hand it over.
        response?.write('event: delta\ndata: {"text":"b"}\n\n')
      }, controller.signal)

      expect(result).toMatchObject({ ok: true, error: '' })
      expect(events).toEqual([{ type: 'delta', data: { text: 'a' } }])
      await vi.waitFor(() => expect(closed).toBe(true))
    } finally {
      server.closeAllConnections()
      await new Promise<void>(resolve => server.close(() => resolve()))
    }
  })

  it('answers a connection that breaks mid-stream with a sentence, after the events that arrived', async () => {
    const server: Server = createServer(crossOrigin((_request, res) => {
      res.writeHead(200, { 'content-type': 'text/event-stream' })
      res.write('event: delta\ndata: {"text":"a"}\n\n')
      setTimeout(() => res.destroy(), 20)
    }))
    await new Promise<void>(resolve => server.listen(0, '127.0.0.1', resolve))
    const base = `http://127.0.0.1:${(server.address() as AddressInfo).port}`
    vi.stubGlobal('fetch', (url: string, init: RequestInit) => realFetch(`${base}${url}`, init))
    const store = usePluginsStore()
    const events: PluginStreamEvent[] = []
    try {
      const result = await store.streamRoute('acme-sites', CHAT, { siteId: '7' }, {}, event => events.push(event), new AbortController().signal)

      expect(events).toEqual([{ type: 'delta', data: { text: 'a' } }])
      expect(result.ok).toBe(false)
      // Nothing answered for the broken part, so the one client-side "no answer" sentence is shown.
      expect(result.error).not.toBe('')
    } finally {
      server.closeAllConnections()
      await new Promise<void>(resolve => server.close(() => resolve()))
    }
  })
})

describe('stores/plugins: reset', () => {
  it('forgets the index and the page, so the next person\'s menu is read afresh', async () => {
    const store = usePluginsStore()
    await store.loadUi()
    await store.loadPage('acme-sites', ['sites', 'blog'])
    expect(store.menu).not.toEqual([])

    store.reset()

    expect(store.menu).toEqual([])
    expect(store.pages).toEqual([])
    expect(store.page).toBeNull()
    expect(store.pageValues).toEqual({})
    await store.loadUi()
    expect(stub.requests.filter(request => request.url === '/api/portal/client/plugins/ui')).toHaveLength(2)
  })
})

describe('stores/plugins: slots', () => {
  /** The replies of the current test. */
  let table: StubRouteTable

  beforeEach(async () => {
    await stub.close()
    table = {}
    stub = await startTransport(routeTable(table))
  })

  it('reads a slot\'s widgets with its context', async () => {
    table[`GET ${slotPath(PluginSlotId.ServiceTabs)}`] = { body: slotIndex(PluginSlotId.ServiceTabs, ['tab']) }

    const widgets = await usePluginsStore().loadSlot(PluginSlotId.ServiceTabs, { serviceId: '4' })

    expect(widgets.map(widget => widget.id)).toEqual(['tab'])
    const [read] = requestsTo(stub.requests, slotPath(PluginSlotId.ServiceTabs))
    expect(queryOf(read!)).toEqual({ serviceId: '4' })
    expect(read?.headers['x-language']).toBe('en')
  })

  it.each([
    ['a service that is not the reader\'s', SERVICE_NOT_FOUND],
    ['an unknown slot', { status: 404, body: { type: 'PLUGIN_SLOT_NOT_FOUND', title: 'Not Found', status: 404, detail: 'No such slot.' } }],
    ['an outage', { status: 503, body: { type: 'SERVICE_UNAVAILABLE', title: 'x', status: 503, detail: 'Down.' } }],
  ])('gives no widgets for %s, and no page error', async (_name, reply) => {
    table[`GET ${slotPath(PluginSlotId.ServiceTabs)}`] = reply
    const store = usePluginsStore()

    expect(await store.loadSlot(PluginSlotId.ServiceTabs, { serviceId: '9' })).toEqual([])
    // A slot never writes the page's sentence.
    expect(store.pageError).toBe('')
    expect(store.page).toBeNull()
  })

  it('reads one widget\'s view with its context', async () => {
    table[`GET ${slotWidgetPath(PluginSlotId.ServiceTabs, 'ok')}`] = { body: workingView() }

    const view = await usePluginsStore().loadSlotWidget(PluginSlotId.ServiceTabs, 'acme-sites', 'ok', { serviceId: '4' })

    expect(view).toEqual(workingView())
    expect(queryOf(requestsTo(stub.requests, slotWidgetPath(PluginSlotId.ServiceTabs, 'ok'))[0]!)).toEqual({ serviceId: '4' })
  })

  it.each([
    ['a widget the backend would not list', WIDGET_NOT_FOUND],
    ['a service that is not the reader\'s', SERVICE_NOT_FOUND],
  ])('gives no view for %s, and no page error', async (_name, reply) => {
    table[`GET ${slotWidgetPath(PluginSlotId.ServiceTabs, 'ok')}`] = reply
    const store = usePluginsStore()

    expect(await store.loadSlotWidget(PluginSlotId.ServiceTabs, 'acme-sites', 'ok', { serviceId: '9' })).toBeNull()
    expect(store.pageError).toBe('')
  })
})
