/**
 * Harness for testing the Nitro BFF routes (`server/api/**`) in isolation.
 *
 * A route file is `export default defineEventHandler(async (event) => { })` and reaches the rest
 * of the runtime through Nitro auto-imports. This module stands those names in as globals and
 * points the real `internalApiCall` at a real `node:http` stub (see `stubServer.ts`), so a test
 * drives the route's handler directly and asserts on what reached the "backend" and on what the
 * route set on the response. Nothing of our own is mocked: `internalApiCall`, `requireRouteParam`
 * and the server-side session (`server/utils/portal-session.ts`, over the in-process store) are the
 * real modules.
 *
 * Import this module before dynamically importing a route, because the route calls
 * `defineEventHandler` while it is being evaluated.
 *
 * @module tests/helpers/bffHarness
 */

import { ofetch } from 'ofetch'
import { vi } from 'vitest'
import type { H3Event } from 'h3'
import { startStubServer } from './stubServer'
import type { StubReply, StubRequest, StubServer } from './stubServer'
import { HttpMethod } from '~/types/httpmethod'

/** A cookie the route under test wrote to the response, with the options it passed. */
export interface WrittenCookie {
  /** The cookie value. */
  value: string
  /** The options the route passed to `setCookie`. */
  options: Record<string, unknown>
}

/** What a test supplies to build a request. */
export interface FakeEventInit {
  /** Incoming headers, lower-cased names. */
  headers?: Record<string, string>
  /** Incoming cookies. */
  cookies?: Record<string, string>
  /** The parsed request body that `readBody` returns. */
  body?: unknown
  /** Route parameters (`[id]` segments). */
  params?: Record<string, string>
  /** The request path. */
  path?: string
  /** The parsed query string that `getQuery` returns. */
  query?: Record<string, string>
  /** The HTTP method. */
  method?: HttpMethod
  /** The address of the connection itself; empty (unknown) when a test sends none. */
  socketAddress?: string
}

/**
 * The fake request/response the globals below read and write. The method stays out of it: a route
 * reads it from the `H3Event` side, whose own string type would otherwise conflict with the enum and
 * reduce `H3Event & FakeEvent` to `never`.
 */
export interface FakeEvent extends Required<Omit<FakeEventInit, 'body' | 'method'>> {
  /** The parsed request body. */
  body: unknown
  /** Cookies written to the response, by name. */
  written: Record<string, WrittenCookie>
  /** Names of cookies deleted from the response. */
  deleted: string[]
  /** Response headers set through `setResponseHeaders`. */
  responseHeaders: Record<string, string>
  /** Status set through `setResponseStatus`, when the route set one. */
  status: number | undefined
  /** Location a `sendRedirect` was given. */
  redirectedTo: string | undefined
}

/** The runtime config the routes read; `apiUrl` and `ssoUrl` follow the running stub. */
const runtimeConfig: Record<string, unknown> = { public: {} }

/**
 * Builds a fake event.
 *
 * @param init - What the request carries.
 * @returns The event, typed as the H3 event a route expects.
 */
export const makeEvent = (init: FakeEventInit = {}): H3Event & FakeEvent => {
  return {
    headers: {},
    cookies: {},
    params: {},
    path: '/',
    query: {},
    method: HttpMethod.Get,
    socketAddress: '',
    ...init,
    body: init.body,
    written: {},
    deleted: [],
    responseHeaders: {},
    status: undefined,
    redirectedTo: undefined,
  } as unknown as H3Event & FakeEvent
}

vi.stubGlobal('defineEventHandler', <T>(handler: T): T => handler)
vi.stubGlobal('readBody', async (event: FakeEvent) => event.body)
vi.stubGlobal('getHeader', (event: FakeEvent, name: string) => event.headers[name.toLowerCase()])
vi.stubGlobal('getCookie', (event: FakeEvent, name: string) => event.cookies[name])
vi.stubGlobal('setCookie', (event: FakeEvent, name: string, value: string, options: Record<string, unknown> = {}) => {
  event.written[name] = { value, options }
})
vi.stubGlobal('parseCookies', (event: FakeEvent) => ({ ...event.cookies }))
vi.stubGlobal('deleteCookie', (event: FakeEvent, name: string) => {
  event.deleted.push(name)
})
vi.stubGlobal('getRouterParam', (event: FakeEvent, name: string) => event.params[name])
// The connection's own address, the way h3 answers when it is not asked to believe X-Forwarded-For.
vi.stubGlobal('getRequestIP', (event: FakeEvent) => event.socketAddress || undefined)
vi.stubGlobal('setResponseStatus', (event: FakeEvent, status: number) => {
  event.status = status
})
vi.stubGlobal('sendRedirect', (event: FakeEvent, location: string) => {
  event.redirectedTo = location
  return location
})
vi.stubGlobal('createError', (options: Record<string, unknown>) =>
  Object.assign(new Error(String(options['statusMessage'] ?? '')), options))
// The request's own origin, built from its `host` header (`localhost` when a test sends none).
vi.stubGlobal('getRequestURL', (event: FakeEvent) => new URL(`http://${event.headers['host'] ?? 'localhost'}${event.path}`))
vi.stubGlobal('getQuery', (event: FakeEvent) => event.query)
vi.stubGlobal('setResponseHeaders', (event: FakeEvent, headers: Record<string, string>) => {
  Object.assign(event.responseHeaders, headers)
})
// A cache wrapper only changes how often the handler runs, never what it answers, so the
// harness runs the handler on every call.
vi.stubGlobal('defineCachedEventHandler', <T>(handler: T): T => handler)
vi.stubGlobal('useRuntimeConfig', () => runtimeConfig)

const { getApiUrl, internalApiBinaryCall, internalApiCall } = await import('~/server/utils/api')
// No `sessionRedis` in the runtime config, so the real store is the in-process one.
const { useSessionStore } = await import('~/server/utils/session-store')
const { SessionCookie } = await import('~/constants/sessionCookie')
const { requireRouteParam } = await import('~/server/utils/requireRouteParam')

vi.stubGlobal('internalApiCall', internalApiCall)
vi.stubGlobal('internalApiBinaryCall', internalApiBinaryCall)
vi.stubGlobal('getApiUrl', getApiUrl)
vi.stubGlobal('requireRouteParam', requireRouteParam)

/**
 * Sets runtime-config entries a route reads (auth mode, SSO callback and public addresses).
 *
 * @param patch - Entries to merge; a `public` entry replaces the public block.
 */
export const setRuntimeConfig = (patch: Record<string, unknown>): void => {
  Object.assign(runtimeConfig, patch)
}

/**
 * Starts the stub that plays the C# API and the SSO, and points the runtime config at it.
 *
 * @param handler - Initial reply function; defaults to `200 {}`.
 * @returns The running stub. Close it in `afterEach`.
 */
export const startBackend = async (handler?: (request: StubRequest) => StubReply): Promise<StubServer> => {
  const stub = await startStubServer(handler)
  // Entries a previous test set must not leak into this one.
  for (const key of Object.keys(runtimeConfig)) {
    Reflect.deleteProperty(runtimeConfig, key)
  }
  runtimeConfig['public'] = {}
  runtimeConfig['apiUrl'] = stub.baseUrl
  runtimeConfig['ssoUrl'] = stub.baseUrl
  runtimeConfig['ssoClientId'] = 'client'
  runtimeConfig['ssoClientSecret'] = 'secret'
  vi.stubGlobal('$fetch', ofetch)
  return stub
}

/**
 * Runs a route's handler and returns what it threw instead of failing the test.
 *
 * @param run - Invokes the handler; a synchronous handler throws from the call itself.
 * @returns The thrown error as a plain record; fails when nothing was thrown.
 */
export const refusal = async (run: () => unknown): Promise<Record<string, unknown>> => {
  try {
    await run()
  } catch (error) {
    return error as Record<string, unknown>
  }
  throw new Error('the route was expected to refuse the request')
}

/** The shape of a route module: its default export is the handler. */
export interface RouteModule {
  /** The route's event handler; a handler with nothing to await answers synchronously. */
  default: (event: H3Event) => unknown
}

/** The shape of a Nitro middleware module: its default export may answer synchronously with nothing. */
export interface MiddlewareModule {
  /** The middleware handler; it returns nothing to let the request continue, or throws to refuse it. */
  default: (event: H3Event) => unknown
}

/** The tokens a test signs in with. */
export interface TestSessionTokens {
  /** The refresh token; absent for a session without one. */
  refreshToken?: string
  /** The `sub` the session belongs to. */
  subject?: string
  /** When the access token expires, in seconds since the epoch. */
  accessTokenExpiresAt?: number
}

/** Counter that keeps the ids of test sessions distinct. */
let sessionCounter = 0

/**
 * Stores a signed-in session in the real (in-process) session store and returns the cookie that
 * names it, the way a completed sign-in leaves the browser.
 *
 * @param accessToken - The access token the session holds.
 * @param tokens - The refresh token, owner and expiry, when the test needs them.
 * @returns The request cookies to pass to `makeEvent`.
 */
export const signedIn = async (accessToken: string, tokens: TestSessionTokens = {}): Promise<Record<string, string>> => {
  sessionCounter += 1
  const id = `test-session-${sessionCounter}`
  await useSessionStore().set(id, { accessToken, ...tokens }, 3600)
  return { [SessionCookie.Session]: id }
}

/**
 * Reads back the session a test's cookie names.
 *
 * @param cookies - The cookies `signedIn` returned, or the event's written session cookie value.
 * @returns The stored session, or null when the store holds none.
 */
export const storedSession = async (cookies: Record<string, string>) => {
  const id = cookies[SessionCookie.Session]
  return id ? useSessionStore().get(id) : null
}
