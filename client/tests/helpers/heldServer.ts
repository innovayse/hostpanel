/**
 * A JSON server whose answers a test releases by hand, for the cases the stub server cannot play: a slow answer that
 * arrives after a newer one. `$fetch` is pointed at it, so the store, the API composable and the transport run for real.
 *
 * @module tests/helpers/heldServer
 */

import { createServer } from 'node:http'
import type { ServerResponse } from 'node:http'
import type { AddressInfo } from 'node:net'
import { ofetch } from 'ofetch'
import { vi } from 'vitest'

/**
 * The answer to a cross-origin request, as the stub server gives it: the code under test runs in a DOM environment
 * that enforces the same-origin policy and preflights the `X-Language` header, while in production it talks to its
 * own origin.
 */
const CORS_HEADERS = {
  'access-control-allow-origin': '*',
  'access-control-allow-methods': 'GET, POST, PUT, PATCH, DELETE, OPTIONS',
  'access-control-expose-headers': '*',
}

/** One request the server is holding. */
export interface HeldRequest {
  /** The request URL, query included. */
  url: string
  /**
   * Answers the request with a JSON body.
   *
   * @param body - The body.
   */
  answer: (body: unknown) => void
}

/** The running server. */
export interface HeldServer {
  /** Every request received and not yet answered, oldest first. */
  held: HeldRequest[]
  /**
   * Closes the server, dropping whatever it still holds.
   *
   * @returns Resolves once it has closed.
   */
  close: () => Promise<void>
}

/**
 * Starts the server and points `$fetch` at it.
 *
 * @returns The running server.
 */
export const startHeldServer = async (): Promise<HeldServer> => {
  const held: HeldRequest[] = []
  const server = createServer((req, res: ServerResponse) => {
    // A preflight is answered at once and never held: only the real request is the test's to release.
    if (req.method === 'OPTIONS') {
      res.writeHead(204, { ...CORS_HEADERS, 'access-control-allow-headers': String(req.headers['access-control-request-headers'] ?? '*') })
      res.end()
      return
    }
    held.push({
      url: req.url ?? '/',
      answer: (body) => {
        res.writeHead(200, { ...CORS_HEADERS, 'Content-Type': 'application/json' })
        res.end(JSON.stringify(body))
      },
    })
  })
  await new Promise<void>(resolve => server.listen(0, '127.0.0.1', resolve))
  vi.stubGlobal('$fetch', ofetch.create({ baseURL: `http://127.0.0.1:${(server.address() as AddressInfo).port}` }))
  return {
    held,
    close: async () => {
      server.closeAllConnections()
      await new Promise<void>(resolve => server.close(() => resolve()))
    },
  }
}
