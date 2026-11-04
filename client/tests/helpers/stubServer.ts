/**
 * A real `node:http` stub server for tests that cross the network layer.
 *
 * Api composables and BFF utilities are tested against this, never by mocking `useApi()`: the
 * code under test makes a real request and the test asserts on what arrived (path, headers,
 * body) and on how the reply was read.
 *
 * @module tests/helpers/stubServer
 */

import { createServer } from 'node:http'
import type { IncomingHttpHeaders, Server } from 'node:http'
import type { AddressInfo } from 'node:net'

/** Headers that let a cross-origin test client read the stub's replies. */
const CORS_HEADERS = {
  'access-control-allow-origin': '*',
  'access-control-allow-methods': 'GET, POST, PUT, PATCH, DELETE, OPTIONS',
  'access-control-expose-headers': '*',
}

/** One request the stub received. */
export interface StubRequest {
  /** HTTP method. */
  method: string
  /** Path and query string. */
  url: string
  /** Request headers, names lower-cased by Node. */
  headers: IncomingHttpHeaders
  /** Raw request body (empty string when none). */
  body: string
}

/** What the stub answers with. */
export interface StubReply {
  /** HTTP status; defaults to 200. */
  status?: number
  /** Body; objects are sent as JSON, strings as text. */
  body?: unknown
  /** Extra response headers. */
  headers?: Record<string, string>
  /**
   * Never answers: the request stays open until the stub closes. Stands in for a dependency that is too
   * slow, so a caller's own time limit can be tested against a real socket.
   */
  hold?: boolean
}

/** A reply function; a promise answers when it settles, so a test can hold a reply back and release it later. */
export type StubHandler = (request: StubRequest) => StubReply | Promise<StubReply>

/** A running stub. */
export interface StubServer {
  /** Base URL, e.g. `http://127.0.0.1:41233`; pass it as the `$fetch` base. */
  baseUrl: string
  /** Every request received, in order. */
  requests: StubRequest[]
  /** Replaces the handler for the following requests. */
  respondWith: (handler: StubHandler) => void
  /** Stops the server. */
  close: () => Promise<void>
}

/**
 * Starts a stub on a free port.
 *
 * @param handler Initial reply function; defaults to `200 {}`.
 * @returns The running stub.
 */
export const startStubServer = async (
  handler: StubHandler = () => ({ body: {} }),
): Promise<StubServer> => {
  const requests: StubRequest[] = []
  let respond = handler

  const server: Server = createServer((req, res) => {
    // The code under test calls the stub from a DOM environment that enforces the same-origin
    // policy, while in production it talks to its own origin. Answering permissively (and the
    // preflight for custom headers such as `X-Language` without recording it) stands in for that.
    if (req.method === 'OPTIONS') {
      res.writeHead(204, { ...CORS_HEADERS, 'access-control-allow-headers': String(req.headers['access-control-request-headers'] ?? '*') })
      res.end()
      return
    }
    const chunks: Buffer[] = []
    req.on('data', (chunk: Buffer) => {
      chunks.push(chunk)
    })
    req.on('end', async () => {
      const request: StubRequest = {
        method: req.method ?? 'GET',
        url: req.url ?? '/',
        headers: req.headers,
        body: Buffer.concat(chunks).toString('utf8'),
      }
      requests.push(request)
      const reply = await respond(request)
      if (reply.hold || res.destroyed) {
        // Left open on purpose; `close` ends it with the rest of the connections.
        return
      }
      const isText = typeof reply.body === 'string'
      res.writeHead(reply.status ?? 200, {
        ...CORS_HEADERS,
        'content-type': isText ? 'text/plain' : 'application/json',
        ...reply.headers,
      })
      res.end(reply.body === undefined ? '' : isText ? (reply.body as string) : JSON.stringify(reply.body))
    })
  })

  await new Promise<void>((resolve) => {
    server.listen(0, '127.0.0.1', resolve)
  })
  // `address()` is an AddressInfo (not a pipe path or null) once listening on a TCP port.
  const { port } = server.address() as AddressInfo

  return {
    baseUrl: `http://127.0.0.1:${port}`,
    requests,
    respondWith: (next) => {
      respond = next
    },
    close: () => new Promise<void>((resolve, reject) => {
      server.close((error) => (error ? reject(error) : resolve()))
      // A held request keeps its connection open, and `close` alone would wait for it forever.
      server.closeAllConnections()
    }),
  }
}
