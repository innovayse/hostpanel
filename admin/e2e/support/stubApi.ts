/**
 * Network-layer stub of the Hostpanel API for the Playwright specs.
 *
 * The panel talks to its API through same-origin `/api/*` calls (`composables/useApi.ts`), so a
 * single `page.route` predicate on that prefix answers all of them. Each spec lists only the
 * routes its flow needs; the session endpoints every page load makes (`/auth/mode`,
 * `/auth/setup-required`, `/auth/me`) are answered here once, statefully, so "signed out until
 * the login call succeeds" behaves like the real API. Anything nobody stubbed answers 404 and is
 * recorded in {@link StubApi.unhandled}, which is what a spec reads to see an unexpected call.
 */
import { HttpMethod } from '../../src/types/http'
import type { Page, Request } from '@playwright/test'
import { adminMe, loginOk, loginRejected, localMode, setupDone, validCredentials } from '../fixtures/auth'

/** One request the page sent to the stubbed API. */
export interface RecordedCall {
  /** HTTP method, upper-case. */
  method: string
  /** URL path, e.g. `/api/clients/7`. */
  path: string
  /** Raw query string including the leading `?`, or an empty string. */
  search: string
  /** Request `content-type` header, or an empty string. */
  contentType: string
  /** Raw request body, or null when the request had none. For multipart this is the encoded form. */
  postData: string | null
  /** Request body parsed as JSON, or null when absent or not JSON. */
  body: unknown
}

/** What a stubbed route answers with. */
export interface StubResponse {
  /** HTTP status; defaults to 200. */
  status?: number
  /** JSON body; omitted for an empty response. */
  body?: unknown
  /** A server-sent event stream answered as `text/event-stream` instead of a JSON body; see {@link changeEvents}. */
  stream?: string
}

/**
 * The answer of a streamed plugin change that ended well: the `done` event carrying the body.
 *
 * @param body - The answer the `done` event carries.
 * @returns The stub answer.
 */
export const changeEvents = (body: unknown): StubResponse => ({ stream: `event: done\ndata: ${JSON.stringify(body)}\n\n` })

/** Computes the answer for one matched request. */
export type Responder = (call: RecordedCall) => StubResponse | Promise<StubResponse>

/** One stubbed endpoint: a method, a path to match, and the answer. */
export interface StubRoute {
  /** HTTP method, upper-case. */
  method: string
  /** Exact path (`/api/clients`) or a RegExp tested against the path (`/api/clients/7`). */
  path: string | RegExp
  /** The answer. */
  respond: Responder
}

/** Options for {@link stubApi}. */
export interface StubOptions {
  /** True when the browser already holds a session. Defaults to false (signed out). */
  signedIn?: boolean
  /** The endpoints this flow uses. Checked before the built-in session endpoints. */
  routes?: StubRoute[]
}

/** The handle a spec keeps to inspect what the page sent. */
export interface StubApi {
  /** Every call the page made to `/api/*`, in order. */
  calls: RecordedCall[]
  /** Calls that matched no stubbed route (they were answered 404). */
  unhandled: RecordedCall[]
  /**
   * Calls matching a method and exact path.
   *
   * @param method - HTTP method, upper-case.
   * @param path - Exact URL path.
   * @returns The matching calls, in order.
   */
  callsTo: (method: string, path: string) => RecordedCall[]
}

/**
 * Builds a route that answers with a fixed body.
 *
 * @param method - HTTP method, upper-case.
 * @param path - Exact path or pattern.
 * @param body - JSON body to answer with.
 * @param status - HTTP status; defaults to 200.
 * @returns The route.
 */
export const json = (method: string, path: string | RegExp, body: unknown, status = 200): StubRoute => ({
  method,
  path,
  respond: () => ({ status, body }),
})

/**
 * Turns a Playwright request into a {@link RecordedCall}.
 *
 * @param request - The intercepted request.
 * @returns The call as the specs inspect it.
 */
const recordOf = (request: Request): RecordedCall => {
  const url = new URL(request.url())
  const postData = request.postData()
  let body: unknown = null
  if (postData !== null) {
    try {
      body = JSON.parse(postData)
    } catch {
      // Multipart or other non-JSON payload: the raw text stays in `postData`.
      body = null
    }
  }
  return {
    method: request.method(),
    path: url.pathname,
    search: url.search,
    contentType: request.headers()['content-type'] ?? '',
    postData,
    body,
  }
}

/**
 * Whether a route is the one for this call.
 *
 * @param route - A candidate route.
 * @param call - The recorded request.
 * @returns True when method and path both match.
 */
const matches = (route: StubRoute, call: RecordedCall): boolean => {
  if (route.method !== call.method) {
    return false
  }
  return typeof route.path === 'string' ? route.path === call.path : route.path.test(call.path)
}

/**
 * Stubs the whole `/api/*` surface of the page and starts recording.
 *
 * Call it before the first `page.goto`; the list of routes a spec passes in is the complete
 * description of the backend it runs against.
 *
 * @param page - The page under test.
 * @param options - Session state and the flow's routes.
 * @returns The recorder handle.
 */
export const stubApi = async (page: Page, options: StubOptions = {}): Promise<StubApi> => {
  let signedIn = options.signedIn ?? false
  const calls: RecordedCall[] = []
  const unhandled: RecordedCall[] = []

  const sessionRoutes: StubRoute[] = [
    json(HttpMethod.Get, '/api/auth/mode', localMode),
    json(HttpMethod.Get, '/api/auth/setup-required', setupDone),
    {
      method: HttpMethod.Get,
      path: '/api/auth/me',
      respond: () => (signedIn
        ? { body: adminMe }
        : { status: 401, body: { type: 'auth.unauthenticated', title: 'Unauthorized', status: 401 } }),
    },
    {
      method: HttpMethod.Post,
      path: '/api/auth/login',
      respond: (call) => {
        const credentials = call.body as { email?: string; password?: string } | null
        if (credentials?.email === validCredentials.email && credentials.password === validCredentials.password) {
          signedIn = true
          return { body: loginOk }
        }
        return { status: 401, body: loginRejected }
      },
    },
    json(HttpMethod.Get, '/api/settings/public', []),
    json(HttpMethod.Get, '/api/admin/dashboard/activity', []),
  ]
  const routes = [...(options.routes ?? []), ...sessionRoutes]

  await page.route((url) => url.pathname.startsWith('/api/'), async (route) => {
    const call = recordOf(route.request())
    calls.push(call)
    const hit = routes.find((candidate) => matches(candidate, call))
    if (!hit) {
      unhandled.push(call)
      await route.fulfill({
        status: 404,
        contentType: 'application/problem+json',
        body: JSON.stringify({ type: 'e2e.unstubbed', title: 'Not Found', status: 404, detail: `${call.method} ${call.path}` }),
      })
      return
    }
    const answer = await hit.respond(call)
    await route.fulfill({
      status: answer.status ?? 200,
      contentType: answer.stream === undefined ? 'application/json' : 'text/event-stream',
      body: answer.stream ?? (answer.body === undefined ? '' : JSON.stringify(answer.body)),
    })
  })

  return {
    calls,
    unhandled,
    callsTo: (method, path) => calls.filter((call) => call.method === method && call.path === path),
  }
}
