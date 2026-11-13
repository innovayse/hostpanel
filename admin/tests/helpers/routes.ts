/**
 * Helpers for screen tests that load from several endpoints: stub the global `fetch` so each
 * request is answered by the route it matches instead of by call order. Three matching styles
 * exist because the screens are scripted three ways; all go through the real `useApi` transport.
 */
import { vi } from 'vitest'
import { json } from './fetch'

/** One stubbed endpoint: the path fragment it answers and the body it returns. */
export interface FetchRoute {
  /** Fragment the request URL must contain (for example `/tickets/5`). */
  path: string
  /** JSON body returned with status 200; `null` answers 204 with no body. */
  body: unknown
}

/** What a route handler receives: the parsed JSON body of the request, undefined when it had none. It answers with a response, or a promise of one (a never-settling promise scripts a request still in flight). */
export type RouteHandler = (body: unknown) => Response | Promise<Response>

/** Handlers by `"VERB /path"` (path after the `/api` prefix, without the query string). */
export type RouteTable = Record<string, RouteHandler>

/**
 * Stubs the global `fetch` with an answer per request line, so a screen that loads and then
 * re-loads (an action followed by a refresh) is scripted by URL instead of by call order.
 *
 * @param routes - Answers keyed by the start of `METHOD url` (for example `POST /api/orders/1/accept`); the first key the request line starts with wins.
 * @returns The mock, for asserting which requests were made.
 */
export const stubRoutes = (routes: Record<string, () => Response>): ReturnType<typeof vi.fn> => {
  const mock = vi.fn((url: string, init?: RequestInit) => {
    const line = `${init?.method ?? 'GET'} ${url}`
    const key = Object.keys(routes).find((candidate) => line.startsWith(candidate))
    const answer = key === undefined ? undefined : routes[key]
    return Promise.resolve(answer ? answer() : new Response(null, { status: 404 }))
  })
  vi.stubGlobal('fetch', mock)
  return mock
}

/**
 * Stubs `fetch` with a router by path fragment: the most specific (longest) path the URL contains
 * answers; an unmatched request answers an empty page.
 *
 * @param routes - The endpoints the screen is expected to call.
 * @returns The mock, for asserting the calls made.
 */
export const stubPathRoutes = (routes: FetchRoute[]): ReturnType<typeof vi.fn> => {
  const ordered = [...routes].sort((a, b) => b.path.length - a.path.length)
  const mock = vi.fn((input: string) => {
    const route = ordered.find((candidate) => String(input).includes(candidate.path))
    if (!route) {
      return Promise.resolve(json({ items: [], totalCount: 0 }))
    }
    return Promise.resolve(route.body === null ? new Response(null, { status: 204 }) : json(route.body))
  })
  vi.stubGlobal('fetch', mock)
  return mock
}

/**
 * Stubs `fetch` to answer from a route table by verb and exact path; a request nothing handles is
 * a 404 problem body.
 *
 * @param table - Handlers by `"GET /admin/settings"`-style key.
 * @returns The mock, for asserting which calls went out.
 */
export const stubRouteTable = (table: RouteTable): ReturnType<typeof vi.fn> => {
  const mock = vi.fn((url: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET'
    const path = String(url).replace(/^\/api/, '').split('?')[0] ?? ''
    const handler = table[`${method} ${path}`]
    const body = typeof init?.body === 'string' ? JSON.parse(init.body) as unknown : undefined
    const response = handler
      ? handler(body)
      : json({ type: 'NOT_STUBBED', title: 'Not stubbed', status: 404, detail: `No stub for ${method} ${path}` }, 404)
    return Promise.resolve(response)
  })
  vi.stubGlobal('fetch', mock)
  return mock
}

/**
 * The verb and path (with query) of every call a stub received.
 *
 * @param mock - The mock returned by one of the stubs.
 * @returns One `"VERB /path?query"` string per call, in order.
 */
export const requestsOf = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map((call) => {
    const [url, init] = call as unknown as [string, RequestInit | undefined]
    return `${init?.method ?? 'GET'} ${String(url).replace(/^\/api/, '')}`
  })
