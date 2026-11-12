/**
 * Mounts plugin blocks the way a plugin page renders them: inside a real `PluginView`, with the real kit, the real
 * Pinia plugin store and every plugin component registered under the name Nuxt auto-imports it as.
 *
 * Nothing of ours is mocked. JSON calls go through the real store, api composable and `apiFetch` to a real stub
 * HTTP server (`startTransport`); a stream is opened by the real `apiStream`, whose `fetch` (the browser's network
 * boundary) is replaced by one that answers with a stream the test writes to, event by event.
 *
 * @module tests/helpers/mountPlugin
 */

import type { VueWrapper } from '@vue/test-utils'
import type { Component } from 'vue'
import { vi } from 'vitest'
import PluginView from '~/components/plugin/View.vue'
import { usePluginsStore } from '~/stores/plugins'
import { mountClient, nuxtComponentName } from '~/tests/helpers/mountClient'
import type { MountWithKitOptions } from '~/tests/helpers/mountWithKit'
import type { StubReply, StubRequest, StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import type { PluginBlock } from '~/types/pluginblock'
import type { PluginUiPage } from '~/types/pluginuipage'
import type { PluginViewRoute } from '~/types/pluginviewroute'

/** Every plugin component file, loaded once. */
const PLUGIN_MODULES = import.meta.glob<{ default: Component }>('../../components/plugin/*.vue', { eager: true })

/** The plugin components by their auto-import names, e.g. `PluginChatBlock`. */
export const PLUGIN_COMPONENTS: Record<string, Component> = Object.fromEntries(
  Object.entries(PLUGIN_MODULES).map(([path, module]) =>
    // A glob key always ends in a file name.
    [nuxtComponentName(['plugin'], path.split('/').pop()!.replace(/\.vue$/, '')), module.default]),
)

/** The plugin id every mounted view belongs to. */
export const TEST_PLUGIN = 'acme-sites'

/** The BFF prefix of the test plugin's JSON routes. */
export const ROUTES_URL = `/api/portal/client/plugins/${TEST_PLUGIN}/routes`

/** The BFF prefix of the test plugin's streaming routes. */
export const STREAM_URL = `/api/portal/client/plugins/${TEST_PLUGIN}/stream`

/** Route references as a view names them, `{ method, path }` exactly as the manifest declares them. */
export const TEST_ROUTES = {
  site: { method: 'get', path: '/sites/{siteId}' },
  history: { method: 'get', path: '/sites/{siteId}/history' },
  publish: { method: 'post', path: '/sites/{siteId}/publish' },
  chat: { method: 'post', path: '/sites/{siteId}/chat' },
} as const satisfies Record<string, PluginViewRoute>

/** The pages of the test plugin's index. */
export const TEST_PAGES: PluginUiPage[] = [
  { pluginId: TEST_PLUGIN, id: 'site', path: '/sites/{siteId}', title: 'Site' },
]

/** The view context a test may set. */
export interface PluginMountContext {
  /** The values route parameters are filled from; `{ siteId: '7' }` when omitted. */
  values?: Record<string, string>
  /** The allowed link origins. */
  externalOrigins?: string[]
  /** The exact origins a preview block may frame. */
  frameOrigins?: string[]
  /** Whether the view is embedded in a host page, as a slot widget is. */
  embedded?: boolean
  /** Further mount options. */
  options?: MountWithKitOptions
}

/**
 * Mounts blocks in a plugin view, with the test plugin's pages in the store's index.
 *
 * @param blocks - The blocks, in order.
 * @param context - The view's values and origins.
 * @returns The mounted view.
 */
export const mountBlocks = (blocks: PluginBlock[], context: PluginMountContext = {}): VueWrapper => {
  const wrapper = mountClient(PluginView, {
    ...context.options,
    props: {
      pluginId: TEST_PLUGIN,
      view: { schema: 1, blocks },
      values: context.values ?? { siteId: '7' },
      externalOrigins: context.externalOrigins ?? ['https://*.sites.example.test'],
      frameOrigins: context.frameOrigins ?? [],
      embedded: context.embedded ?? false,
    },
    components: { ...PLUGIN_COMPONENTS, ...context.options?.components },
  })
  usePluginsStore().pages = TEST_PAGES
  return wrapper
}

/**
 * Starts the stub API the blocks call, answering by `METHOD url`.
 *
 * @param replies - Reply per `METHOD url`; anything else answers 404 with a problem body.
 * @returns The running stub; close it after the test.
 */
export const startPluginApi = (replies: Record<string, StubReply | ((request: StubRequest) => StubReply | Promise<StubReply>)>): Promise<StubServer> =>
  startTransport((request) => {
    const reply = replies[`${request.method} ${request.url}`]
    if (reply === undefined) {
      return { status: 404, body: { type: 'PLUGIN_ROUTE_NOT_FOUND', title: 'Not Found', status: 404, detail: 'No such route.' } }
    }
    return typeof reply === 'function' ? reply(request) : reply
  })

/**
 * A problem body as the API answers a refusal.
 *
 * @param status - The status.
 * @param detail - The localised sentence.
 * @param errors - Field sentences, by camelCase field.
 * @returns The reply.
 */
export const problem = (status: number, detail: string, errors?: Record<string, string[]>): StubReply => ({
  status,
  body: { type: 'plugin.acme-sites.refused', title: 'Refused', status, detail, ...(errors ? { errors } : {}) },
})

/** A stream the test writes to, event by event, and what the panel sent to open it. */
export interface StreamControl {
  /** The calls `apiStream` made to `fetch`: the URL and the request. */
  calls: { url: string, init: RequestInit }[]
  /** Writes one event. */
  send: (type: string, data: unknown) => void
  /** Writes a heartbeat comment. */
  keepalive: () => void
  /** Ends the stream. */
  close: () => void
  /** Breaks the connection. */
  fail: () => void
  /** Whether the reader cancelled the stream. */
  cancelled: () => boolean
}

/**
 * Replaces the browser's `fetch` with one that answers a streaming call with a stream the test controls, or with
 * the given refusal. Undone by `vi.unstubAllGlobals()`. Only the stream address is answered here: `$fetch` (the JSON
 * calls, such as a chat's history) runs on `fetch` too, and a stub created after this one must still reach its
 * server, so every other address goes to the real `fetch`.
 *
 * @param refusal - A status and problem body to answer instead of a stream.
 * @returns The stream's controls.
 */
export const stubStream = (refusal?: { status: number, body: unknown }): StreamControl => {
  const encoder = new TextEncoder()
  const calls: StreamControl['calls'] = []
  let source: ReadableStreamDefaultController<Uint8Array> | undefined
  let cancelled = false
  const stream = new ReadableStream<Uint8Array>({
    start(controller) {
      source = controller
    },
    cancel() {
      cancelled = true
    },
  })
  const realFetch = globalThis.fetch
  vi.stubGlobal('fetch', vi.fn(async (url: string, init: RequestInit) => {
    if (!String(url).startsWith(STREAM_URL)) {
      return realFetch(url, init)
    }
    calls.push({ url, init })
    if (refusal) {
      return new Response(JSON.stringify(refusal.body), { status: refusal.status, headers: { 'content-type': 'application/problem+json' } })
    }
    return new Response(stream, { status: 200, headers: { 'content-type': 'text/event-stream' } })
  }))
  return {
    calls,
    send: (type, data) => source?.enqueue(encoder.encode(`event: ${type}\ndata: ${JSON.stringify(data)}\n\n`)),
    keepalive: () => source?.enqueue(encoder.encode(': keepalive\n\n')),
    close: () => source?.close(),
    fail: () => source?.error(new TypeError('network error')),
    cancelled: () => cancelled,
  }
}
