/**
 * Tests for `composables/apis/usePluginsApi.ts`: each endpoint (index, page, slot index, slot widget, routes) is called for real against a stub
 * server, and the stream is opened with `fetch` and read as it arrives.
 *
 * @module tests/composables/apis/usePluginsApi.test
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { expectLastRequest, startTransport } from '../../helpers/stubTransport'
import type { ExpectedRequest } from '../../helpers/stubTransport'
import type { StubServer } from '../../helpers/stubServer'
import { usePluginsApi } from '~/composables/apis/usePluginsApi'
import { HttpMethod } from '~/types/httpmethod'

/** The stub started by the current test. */
let stub: StubServer | undefined

/** The real `fetch`, which the stream test points at the stub. */
const realFetch = globalThis.fetch

afterEach(async () => {
  vi.stubGlobal('fetch', realFetch)
  await stub?.close()
  stub = undefined
})

/** The api surface under test. */
const api = usePluginsApi()

/** Every JSON endpoint function with the request it must send. */
const rows: ExpectedRequest[] = [
  { name: 'fetchUi reads the index', call: () => api.fetchUi(), method: HttpMethod.Get, url: '/api/portal/client/plugins/ui' },
  { name: 'fetchPageByPath reads the page an address names', call: () => api.fetchPageByPath('acme-sites', '/կայք/сайт'), method: HttpMethod.Get, url: `/api/portal/client/plugins/acme-sites/page?path=${encodeURIComponent('/կայք/сайт')}` },
  { name: 'fetchSlot reads a slot\'s index with its service', call: () => api.fetchSlot('client.service.tabs', { serviceId: '4' }), method: HttpMethod.Get, url: '/api/portal/client/plugins/slots/client.service.tabs?serviceId=4' },
  { name: 'fetchSlot reads a slot\'s index with no context', call: () => api.fetchSlot('client.dashboard.cards', {}), method: HttpMethod.Get, url: '/api/portal/client/plugins/slots/client.dashboard.cards' },
  { name: 'fetchSlotWidget reads one widget\'s view', call: () => api.fetchSlotWidget('client.dashboard.cards', 'acme', 'card', {}), method: HttpMethod.Get, url: '/api/portal/client/plugins/slots/client.dashboard.cards/widgets/acme/card' },
  { name: 'fetchSlotWidget reads one widget\'s view with its service', call: () => api.fetchSlotWidget('client.service.tabs', 'acme', 'tab', { serviceId: '4' }), method: HttpMethod.Get, url: '/api/portal/client/plugins/slots/client.service.tabs/widgets/acme/tab?serviceId=4' },
  { name: 'callRoute reads a GET route', call: () => api.callRoute('acme-sites', HttpMethod.Get, 'sites/7?page=2'), method: HttpMethod.Get, url: '/api/portal/client/plugins/acme-sites/routes/sites/7?page=2' },
  { name: 'callRoute posts to a route', call: () => api.callRoute('acme-sites', HttpMethod.Post, 'sites/7/publish', { now: true }), method: HttpMethod.Post, url: '/api/portal/client/plugins/acme-sites/routes/sites/7/publish', body: { now: true } },
]

describe('usePluginsApi', () => {
  it.each(rows)('$name', async (row) => {
    stub = await startTransport()
    await row.call()
    expectLastRequest(stub, row)
  })

  it('opens a stream with the language and the CSRF header and hands back its body before it ends', async () => {
    stub = await startTransport(() => ({ headers: { 'content-type': 'text/event-stream' }, body: 'event: done\ndata: {}\n\n' }))
    const base = stub.baseUrl
    vi.stubGlobal('fetch', (url: string, init: RequestInit) => realFetch(`${base}${url}`, init))

    const body = await api.openStream('acme-sites', 'sites/7/chat', { message: 'hi' }, new AbortController().signal)

    expect(await new Response(body).text()).toBe('event: done\ndata: {}\n\n')
    const sent = stub.requests.at(-1)!
    expect(sent.method).toBe('POST')
    expect(sent.url).toBe('/api/portal/client/plugins/acme-sites/stream/sites/7/chat')
    expect(sent.headers['x-language']).toBe('en')
    expect(sent.headers['x-requested-with']).toBe('XMLHttpRequest')
    expect(JSON.parse(sent.body)).toEqual({ message: 'hi' })
  })

  it('throws a refused stream with the error body, as `$fetch` would', async () => {
    stub = await startTransport(() => ({ status: 403, body: { statusMessage: 'Not allowed.', data: { code: 'CLIENT_PERMISSION_DENIED' } } }))
    const base = stub.baseUrl
    vi.stubGlobal('fetch', (url: string, init: RequestInit) => realFetch(`${base}${url}`, init))

    await expect(api.openStream('acme-sites', 'chat', {}, new AbortController().signal))
      .rejects.toMatchObject({ statusCode: 403, data: { statusMessage: 'Not allowed.' } })
  })
})
