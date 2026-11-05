/**
 * The plugin BFF played by a real stub server, for the plugin store, the view wiring and the plugin page: the
 * `/api/portal/client/plugins/**` paths answer from a table a test fills, and `fetch` (which `apiStream` opens a
 * stream with) is pointed at the same stub, so every layer from the store down runs for real.
 *
 * @module tests/helpers/pluginTransport
 */

import { vi } from 'vitest'
import { startTransport } from './stubTransport'
import type { StubReply, StubRequest, StubServer } from './stubServer'

/** The real `fetch`, restored by {@link stopPluginTransport}. */
const realFetch = globalThis.fetch

/** A reply chosen by request: the first entry whose method and path prefix match answers. */
export interface PluginStubRoute {
  /** The HTTP method, e.g. `GET`. */
  method: string
  /** A prefix of the request URL, query included, e.g. `/api/portal/client/plugins/ui`. */
  url: string
  /** The reply. */
  reply: StubReply
}

/**
 * Starts the stub and points `$fetch` and `fetch` at it.
 *
 * @param routes - The replies, tried in order; anything unmatched answers 599 so a stray call fails its test.
 * @returns The running stub; close it with {@link stopPluginTransport}.
 */
export const startPluginTransport = async (routes: PluginStubRoute[]): Promise<StubServer> => {
  const stub = await startTransport((request: StubRequest): StubReply =>
    routes.find(route => route.method === request.method && request.url.startsWith(route.url))?.reply ?? { status: 599, body: {} })
  const base = stub.baseUrl
  vi.stubGlobal('fetch', (url: string, init: RequestInit) => realFetch(`${base}${url}`, init))
  return stub
}

/**
 * Closes the stub and gives `fetch` back.
 *
 * @param stub - The stub {@link startPluginTransport} started, or undefined when none was.
 * @returns Resolves once the stub has closed.
 */
export const stopPluginTransport = async (stub: StubServer | undefined): Promise<void> => {
  vi.stubGlobal('fetch', realFetch)
  await stub?.close()
}
