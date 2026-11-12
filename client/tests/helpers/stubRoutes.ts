/**
 * A reply table for the real stub server: a page or store test answers each BFF path it expects,
 * by method and path, and anything else is the BFF's own 404. The page, its stores, the API
 * composables and the transport all run for real; only the wire answers come from the test.
 *
 * @module tests/helpers/stubRoutes
 */

import { HttpMethod } from '~/types/httpmethod'
import type { StubHandler, StubReply, StubRequest } from './stubServer'

/** Replies by `METHOD /path` (path without its query string). */
export type StubRouteTable = Record<string, StubReply | ((request: StubRequest) => StubReply | Promise<StubReply>)>

/** What an unscripted request is answered with: the BFF's own not found. */
const NOT_SCRIPTED: StubReply = { status: 404, body: { type: 'NOT_FOUND', title: 'Not Found', status: 404, detail: 'Not found.' } }

/**
 * The key of a read in a stub route table.
 *
 * @param path - The path of the read.
 * @returns The `GET` key for the route table.
 */
export const readOf = (path: string): string => `${HttpMethod.Get} ${path}`

/**
 * The key of a write in a stub route table.
 *
 * @param path - The path of the write.
 * @param method - The verb of the write; a `POST` unless the write is another one.
 * @returns The `METHOD /path` key for the route table.
 */
export const writeOf = (path: string, method: HttpMethod = HttpMethod.Post): string => `${method} ${path}`

/**
 * The reply of a backend that refuses a request with a sentence the screen shows.
 *
 * @param detail - The sentence.
 * @returns The failure reply.
 */
export const refusal = (detail: string): StubReply => ({ status: 422, body: { detail } })

/**
 * The path of a request without its query string.
 *
 * @param request - The request the stub received.
 * @returns The path.
 */
export const pathOf = (request: StubRequest): string => request.url.split('?')[0] ?? request.url

/**
 * The query of a request.
 *
 * @param request - The request the stub received.
 * @returns The query parameters.
 */
export const queryOf = (request: StubRequest): Record<string, string> =>
  Object.fromEntries(new URLSearchParams(request.url.split('?')[1] ?? ''))

/**
 * Builds a stub reply function from a table.
 *
 * @param table - The replies; a later `Object.assign` onto the same table changes them for the next requests.
 * @returns The reply function to hand to `startTransport` or `respondWith`.
 */
export const routeTable = (table: StubRouteTable): StubHandler => (request: StubRequest) => {
  const entry = table[`${request.method} ${pathOf(request)}`]
  if (entry === undefined) {
    return NOT_SCRIPTED
  }
  return typeof entry === 'function' ? entry(request) : entry
}

/**
 * The requests made to one path.
 *
 * @param requests - Every request the stub received.
 * @param path - The path, without query.
 * @returns The matching requests, oldest first.
 */
export const requestsTo = (requests: StubRequest[], path: string): StubRequest[] =>
  requests.filter(request => pathOf(request) === path)

/** A reply a test holds back and releases when it chooses. */
export interface HeldReply {
  /** The reply function to put in a table: it answers once {@link release} is called. */
  reply: () => Promise<StubReply>
  /** How many requests are waiting on it. */
  waiting: () => number
  /**
   * Answers every waiting request with the reply.
   *
   * @param reply - What to answer.
   * @returns Nothing.
   */
  release: (reply: StubReply) => void
}

/**
 * Builds a reply the test holds back: a request waits until the test releases it, the way a slow
 * backend answers late. Out-of-order tests hold the first reply, answer the second, then release.
 *
 * @returns The held reply.
 */
export const heldReply = (): HeldReply => {
  const waiting: ((reply: StubReply) => void)[] = []
  return {
    reply: () => new Promise<StubReply>((resolve) => {
      waiting.push(resolve)
    }),
    waiting: () => waiting.length,
    release: (reply) => {
      for (const resolve of waiting.splice(0)) {
        resolve(reply)
      }
    },
  }
}
