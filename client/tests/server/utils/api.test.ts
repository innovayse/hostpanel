/**
 * Tests for the BFF transport in `server/utils/api.ts`, run against a real HTTP stub that plays
 * the C# API (and the SSO token endpoint for the refresh), over the real in-process session store.
 *
 * The language proof for the second hop: the `X-Language` the browser sent must reach the backend
 * on every call, the retry after a token refresh included. The error half: a refusal is
 * forwarded as the backend's own `detail` (never an invented sentence) with its code, title and
 * field errors in `data`, and an answer-less backend leaks no upstream address.
 *
 * @module tests/server/utils/api.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { H3Event } from 'h3'
import { ofetch } from 'ofetch'
import { startStubServer } from '../../helpers/stubServer'
import type { StubReply, StubRequest, StubServer } from '../../helpers/stubServer'
import { HttpMethod } from '~/types/httpmethod'

/** A fake H3 event: the incoming headers, cookies, and the cookies the code under test writes. */
interface FakeEvent {
  /** Incoming request headers, lower-cased. */
  headers: Record<string, string>
  /** Incoming cookies. */
  cookies: Record<string, string>
  /** Cookies written to the response, by name. */
  written: Record<string, string>
  /** Cookies deleted from the response. */
  deleted: string[]
  /** Response headers the code under test set. */
  responseHeaders: Record<string, string>
  /** The address of the connection itself, the only one a visitor cannot choose. */
  socketAddress: string | undefined
}

/** The stub playing the backend and the SSO. */
let stub: StubServer

/** The number of trusted proxies the runtime config reports; reset to none before every test. */
let trustedProxyCount = 0

/** Id of the signed-in session every event carries by default; it holds the access token `old`. */
const SIGNED_IN = 'session-signed-in'

/** Id of a session that also holds a refresh token. */
const REFRESHABLE = 'session-refreshable'

/**
 * Builds a fake event.
 *
 * @param headers - Incoming headers (lower-cased names).
 * @param cookies - Incoming cookies.
 * @param socketAddress - The address of the connection itself; none when omitted.
 * @returns The event, typed as the H3 event the code under test expects.
 */
const makeEvent = (
  headers: Record<string, string> = {},
  cookies: Record<string, string> = { portal_session: SIGNED_IN },
  socketAddress?: string
): H3Event & FakeEvent => {
  return { headers, cookies, written: {}, deleted: [], responseHeaders: {}, socketAddress } as unknown as H3Event & FakeEvent
}

vi.stubGlobal('getHeader', (event: FakeEvent, name: string) => event.headers[name.toLowerCase()])
vi.stubGlobal('getRequestIP', (event: FakeEvent) => event.socketAddress)
vi.stubGlobal('setResponseHeaders', (event: FakeEvent, headers: Record<string, string>) => {
  Object.assign(event.responseHeaders, headers)
})
vi.stubGlobal('getCookie', (event: FakeEvent, name: string) => event.cookies[name])
vi.stubGlobal('setCookie', (event: FakeEvent, name: string, value: string) => {
  event.written[name] = value
})
vi.stubGlobal('deleteCookie', (event: FakeEvent, name: string) => {
  event.deleted.push(name)
})
vi.stubGlobal('createError', (options: Record<string, unknown>) => Object.assign(new Error(String(options.statusMessage ?? '')), options))
/**
 * The runtime config the code under test reads: the running stub as backend and sign-on service,
 * and the trusted proxy count of the current test. Installed again before every test, so a test
 * that swaps the config for its own leaves nothing behind for the next one.
 *
 * @returns The runtime config.
 */
const runtimeConfig = () => ({ apiUrl: stub.baseUrl, ssoUrl: stub.baseUrl, ssoClientId: 'client', ssoClientSecret: 'secret', trustedProxyCount })

vi.stubGlobal('useRuntimeConfig', runtimeConfig)

const { backendLanguage, internalApiBinaryCall, internalApiCall } = await import('~/server/utils/api')
// No `sessionRedis` in the runtime config: the real store is the in-process one.
const { useSessionStore } = await import('~/server/utils/session-store')

beforeEach(async () => {
  trustedProxyCount = 0
  stub = await startStubServer()
  vi.stubGlobal('useRuntimeConfig', runtimeConfig)
  vi.stubGlobal('$fetch', ofetch)
  await useSessionStore().set(SIGNED_IN, { accessToken: 'old' }, 3600)
  await useSessionStore().set(REFRESHABLE, { accessToken: 'old', refreshToken: 'r' }, 3600)
})

afterEach(async () => {
  await stub.close()
})

/**
 * Requests that reached the backend path (the SSO token endpoint excluded).
 *
 * @returns The backend requests, in order.
 */
const backendRequests = (): StubRequest[] => stub.requests.filter((request) => request.url.startsWith('/api/'))

describe('backendLanguage', () => {
  it.each(['en', 'ru', 'hy', 'de', 'es', 'fr'])('passes the supported language %s through', (language) => {
    expect(backendLanguage(makeEvent({ 'x-language': language }))).toBe(language)
  })

  it('falls back to the default when the header is missing, unsupported, or the retired x-locale', () => {
    expect(backendLanguage(makeEvent())).toBe('en')
    expect(backendLanguage(makeEvent({ 'x-language': 'it' }))).toBe('en')
    expect(backendLanguage(makeEvent({ 'x-locale': 'ru' }))).toBe('en')
  })
})

describe('internalApiCall sliding session', () => {
  it('stores a renewed token from the backend in the server-side session, not in a cookie', async () => {
    stub.respondWith(() => ({ body: { ok: true }, headers: { 'X-Renewed-Access-Token': 'renewed' } }))
    const event = makeEvent()

    await internalApiCall(event, '/me/services')

    expect(await useSessionStore().get(SIGNED_IN)).toMatchObject({ accessToken: 'renewed' })
    expect(event.written['portal_session']).toBe(SIGNED_IN)
    expect(event.written['auth_token']).toBeUndefined()
  })

  it('leaves the session alone when the backend offers no renewal', async () => {
    const event = makeEvent()

    await internalApiCall(event, '/me/services')

    expect(await useSessionStore().get(SIGNED_IN)).toMatchObject({ accessToken: 'old' })
    expect(event.written).toEqual({})
  })
})

describe('internalApiCall when the session store fails while storing a renewal', () => {
  /** The call under test, loaded afresh over a session store that reads but cannot write. */
  let failingCall: typeof internalApiCall

  beforeEach(async () => {
    // Redis is the only dependency replaced: the client reads the stored session back but every
    // write transaction is rejected. The store code on top of it is the real one.
    vi.resetModules()
    vi.stubEnv('NODE_ENV', 'development')
    vi.doMock('ioredis', () => {
      /** A write transaction whose commit always fails. */
      const failingTransaction = {
        set: () => failingTransaction,
        sadd: () => failingTransaction,
        expire: () => failingTransaction,
        del: () => failingTransaction,
        srem: () => failingTransaction,
        exec: () => Promise.reject(new Error('store down')),
      }
      /** A Redis client that serves the signed-in session and refuses every write. */
      class UnwritableRedis {
        /**
         * Reads a stored session.
         *
         * @returns The signed-in session as stored JSON.
         */
        get = async (): Promise<string> => JSON.stringify({ accessToken: 'old' })

        /**
         * Opens a write transaction.
         *
         * @returns The transaction, which cannot be committed.
         */
        multi = () => failingTransaction
      }
      return { default: UnwritableRedis }
    })
    vi.stubGlobal('useRuntimeConfig', () => ({ ...runtimeConfig(), sessionRedis: 'redis://localhost:6379' }))
    failingCall = (await import('~/server/utils/api')).internalApiCall
  })

  afterEach(() => {
    vi.doUnmock('ioredis')
    vi.unstubAllEnvs()
    vi.resetModules()
  })

  it('still throws the error of the backend instead of the store failure', async () => {
    stub.respondWith(() => ({ status: 409, body: { type: 'CONFLICT', detail: 'Taken.' }, headers: { 'X-Renewed-Access-Token': 'renewed' } }))

    await expect(failingCall(makeEvent(), '/me/services')).rejects.toMatchObject({ statusCode: 409, data: { code: 'CONFLICT' } })
  })

  it('still answers a successful call when the renewal cannot be stored', async () => {
    stub.respondWith(() => ({ body: { ok: true }, headers: { 'X-Renewed-Access-Token': 'renewed' } }))

    expect(await failingCall(makeEvent(), '/me/services')).toEqual({ ok: true })
  })
})

describe('internalApiCall language', () => {
  it('sends the browser X-Language and the bearer token to the backend', async () => {
    await internalApiCall(makeEvent({ 'x-language': 'hy' }), '/clients/me')

    expect(backendRequests()[0]?.url).toBe('/api/clients/me')
    expect(backendRequests()[0]?.headers['x-language']).toBe('hy')
    expect(backendRequests()[0]?.headers['authorization']).toBe('Bearer old')
  })

  it('forwards the socket address of the visitor to the backend, and none when it is unknown', async () => {
    await internalApiCall(makeEvent({}, { portal_session: SIGNED_IN }, '203.0.113.7'), '/orders', { method: HttpMethod.Post, body: {} })
    await internalApiCall(makeEvent(), '/clients/me')

    expect(backendRequests()[0]?.headers['x-forwarded-for']).toBe('203.0.113.7')
    expect(backendRequests()[1]?.headers['x-forwarded-for']).toBeUndefined()
  })

  it('believes no X-Forwarded-For entry while no proxy is trusted: a spoofed leftmost entry never reaches the backend', async () => {
    await internalApiCall(makeEvent({ 'x-forwarded-for': '1.2.3.4' }, { portal_session: SIGNED_IN }, '198.51.100.20'), '/orders', { method: HttpMethod.Post, body: {} })

    expect(backendRequests()[0]?.headers['x-forwarded-for']).toBe('198.51.100.20')
  })

  it('reads the address the trusted proxy appended, not the entry the visitor wrote', async () => {
    trustedProxyCount = 1

    await internalApiCall(makeEvent({ 'x-forwarded-for': '1.2.3.4, 203.0.113.7' }, { portal_session: SIGNED_IN }, '10.0.0.2'), '/orders', { method: HttpMethod.Post, body: {} })

    expect(backendRequests()[0]?.headers['x-forwarded-for']).toBe('203.0.113.7')
  })

  it('sends the CSRF header on an anonymous state-changing call, which the backend refuses without it', async () => {
    await internalApiCall(makeEvent(), '/domains/check', { method: HttpMethod.Post, body: { domain: 'a.am' }, anonymous: true })

    expect(backendRequests()[0]?.headers['x-requested-with']).toBe('XMLHttpRequest')
    expect(backendRequests()[0]?.headers['authorization']).toBeUndefined()
  })

  it('sends the default language when the browser sent none or an unsupported one', async () => {
    await internalApiCall(makeEvent(), '/clients/me')
    await internalApiCall(makeEvent({ 'x-language': 'zz' }), '/clients/me')

    expect(backendRequests().map((request) => request.headers['x-language'])).toEqual(['en', 'en'])
  })

  it('keeps the language on the retry after a token refresh', async () => {
    stub.respondWith((request) => {
      if (request.url === '/connect/token') {
        return { body: { access_token: 'fresh', expires_in: 900 } }
      }
      return request.headers['authorization'] === 'Bearer fresh' ? { body: { ok: true } } : { status: 401, body: {} }
    })
    const event = makeEvent({ 'x-language': 'ru' }, { portal_session: REFRESHABLE })

    const result = await internalApiCall(event, '/me/services')

    expect(result).toEqual({ ok: true })
    expect(backendRequests().map((request) => [request.headers['authorization'], request.headers['x-language']])).toEqual([
      ['Bearer old', 'ru'],
      ['Bearer fresh', 'ru'],
    ])
    expect(await useSessionStore().get(REFRESHABLE)).toMatchObject({ accessToken: 'fresh', refreshToken: 'r' })
  })

  it('ends the session and answers 401 when the refresh is refused', async () => {
    stub.respondWith((request) => request.url === '/connect/token' ? { status: 400, body: { error: 'invalid_grant' } } : { status: 401, body: {} })
    const event = makeEvent({}, { portal_session: REFRESHABLE })

    await expect(internalApiCall(event, '/me/services')).rejects.toMatchObject({ statusCode: 401 })

    expect(await useSessionStore().get(REFRESHABLE)).toBeNull()
    expect(event.deleted).toEqual(expect.arrayContaining(['portal_session', 'authed']))
  })

  it('ends a session without a refresh token on a 401, since nothing can renew it', async () => {
    stub.respondWith(() => ({ status: 401, body: {} }))
    const event = makeEvent()

    await expect(internalApiCall(event, '/me/services')).rejects.toMatchObject({ statusCode: 401 })

    expect(stub.requests.some((request) => request.url === '/connect/token')).toBe(false)
    expect(await useSessionStore().get(SIGNED_IN)).toBeNull()
  })

  it('does not refresh, and sends no bearer, for an anonymous call', async () => {
    stub.respondWith(() => ({ status: 401, body: { type: 'INVALID_CREDENTIALS', detail: 'Wrong.' } }))
    const event = makeEvent({ 'x-language': 'ru' }, { portal_session: REFRESHABLE })

    await expect(internalApiCall(event, '/auth/login', { method: HttpMethod.Post, body: { email: 'a' }, anonymous: true })).rejects.toMatchObject({ statusCode: 401 })

    expect(stub.requests).toHaveLength(1)
    expect(stub.requests[0]?.headers['authorization']).toBeUndefined()
    expect(stub.requests[0]?.headers['x-language']).toBe('ru')
    expect(JSON.parse(stub.requests[0]?.body ?? '{}')).toEqual({ email: 'a' })
  })
})

describe('internalApiCall refusals', () => {
  /**
   * Scripts a refusal and returns what the route would rethrow.
   *
   * @param reply - The backend answer.
   * @returns The thrown error as a plain record.
   */
  const refusedWith = async (reply: StubReply): Promise<Record<string, unknown>> => {
    stub.respondWith(() => reply)
    try {
      await internalApiCall(makeEvent(), '/clients/me')
    } catch (error) {
      return error as Record<string, unknown>
    }
    throw new Error('the call was expected to be refused')
  }

  it('forwards the backend detail as the sentence and its code, title and field errors as data', async () => {
    const error = await refusedWith({
      status: 400,
      body: { type: 'VALIDATION_ERROR', title: 'Validation failed', detail: 'Проверьте поля.', errors: { email: ['Неверный email.'] } },
    })

    expect(error).toMatchObject({
      statusCode: 400,
      statusMessage: 'Проверьте поля.',
      data: { code: 'VALIDATION_ERROR', title: 'Validation failed', errors: { email: ['Неверный email.'] } },
    })
  })

  it('falls back to the title when the backend sent no detail', async () => {
    const error = await refusedWith({ status: 404, body: { type: 'NOT_FOUND', title: 'Not found' } })

    expect(error).toMatchObject({ statusCode: 404, statusMessage: 'Not found', data: { code: 'NOT_FOUND' } })
  })

  it('invents no sentence and leaks no address when the backend sent no body', async () => {
    const error = await refusedWith({ status: 500, body: '' })

    expect(error.statusCode).toBe(500)
    expect(error.statusMessage).toBeUndefined()
    expect(error.data).toEqual({ code: null, title: null, errors: null })
    expect(JSON.stringify(error)).not.toContain(stub.baseUrl)
  })

  it('passes the backend Retry-After and X-Request-Id of a refusal on to the browser response', async () => {
    stub.respondWith(() => ({
      status: 429,
      body: { type: 'RATE_LIMITED', detail: 'Slow down.' },
      headers: { 'Retry-After': '17', 'X-Request-Id': '0123456789abcdef', 'X-Backend-Detail': 'internal-host-7' }
    }))
    const event = makeEvent()

    await expect(internalApiCall(event, '/clients/me')).rejects.toMatchObject({ statusCode: 429, data: { code: 'RATE_LIMITED' } })

    expect(event.responseHeaders).toEqual({ 'Retry-After': '17', 'X-Request-Id': '0123456789abcdef' })
  })

  it('drops a Retry-After or X-Request-Id of an unexpected shape instead of echoing it', async () => {
    stub.respondWith(() => ({
      status: 503,
      body: { type: 'UNAVAILABLE', detail: 'Busy.' },
      headers: { 'Retry-After': 'soon; Set-Cookie: a=b', 'X-Request-Id': 'a b<script>' }
    }))
    const event = makeEvent()

    await expect(internalApiCall(event, '/clients/me')).rejects.toMatchObject({ statusCode: 503 })

    expect(event.responseHeaders).toEqual({})
  })

  it('sets no response header when the backend sent neither', async () => {
    stub.respondWith(() => ({ status: 404, body: { type: 'NOT_FOUND', detail: 'Missing.' } }))
    const event = makeEvent()

    await expect(internalApiCall(event, '/clients/me')).rejects.toMatchObject({ statusCode: 404 })

    expect(event.responseHeaders).toEqual({})
  })

  it('answers 502 with no sentence when the backend cannot be reached', async () => {
    vi.stubGlobal('useRuntimeConfig', () => ({ ...runtimeConfig(), apiUrl: 'http://127.0.0.1:1' }))

    await expect(internalApiCall(makeEvent(), '/clients/me')).rejects.toMatchObject({ statusCode: 502, data: { code: null } })
  })
})

describe('internalApiBinaryCall', () => {
  it('returns the file bytes with the download headers, sending the bearer token, language and visitor address', async () => {
    stub.respondWith(() => ({
      body: '%PDF-1.4 test',
      headers: { 'content-type': 'application/pdf', 'content-disposition': 'attachment; filename=invoice-7.pdf', 'cache-control': 'private, no-store' }
    }))

    const file = await internalApiBinaryCall(makeEvent({ 'x-language': 'ru' }, { portal_session: SIGNED_IN }, '203.0.113.7'), '/me/invoices/7/pdf')

    expect(file.body.toString('latin1')).toBe('%PDF-1.4 test')
    expect(file).toMatchObject({ contentType: 'application/pdf', contentDisposition: 'attachment; filename=invoice-7.pdf', cacheControl: 'private, no-store' })
    expect(backendRequests()[0]).toMatchObject({ method: 'GET', url: '/api/me/invoices/7/pdf' })
    expect(backendRequests()[0]?.headers['authorization']).toBe('Bearer old')
    expect(backendRequests()[0]?.headers['x-language']).toBe('ru')
    expect(backendRequests()[0]?.headers['x-forwarded-for']).toBe('203.0.113.7')
  })

  it('keeps a renewed access token the backend offered', async () => {
    stub.respondWith(() => ({ body: '%PDF', headers: { 'content-type': 'application/pdf', 'X-Renewed-Access-Token': 'renewed' } }))

    await internalApiBinaryCall(makeEvent(), '/me/invoices/7/pdf')

    expect(await useSessionStore().get(SIGNED_IN)).toMatchObject({ accessToken: 'renewed' })
  })

  it('reads the refusal as the JSON it is, though the answer was requested as bytes, and passes Retry-After on', async () => {
    stub.respondWith(() => ({
      status: 429,
      body: { type: 'RATE_LIMITED', detail: 'Slow down.' },
      headers: { 'Retry-After': '17', 'X-Request-Id': '0123456789abcdef' }
    }))
    const event = makeEvent()

    await expect(internalApiBinaryCall(event, '/me/invoices/7/pdf')).rejects.toMatchObject({
      statusCode: 429,
      statusMessage: 'Slow down.',
      data: { code: 'RATE_LIMITED' }
    })

    expect(event.responseHeaders).toEqual({ 'Retry-After': '17', 'X-Request-Id': '0123456789abcdef' })
  })

  it('forwards no sentence when the refusal body is not JSON', async () => {
    stub.respondWith(() => ({ status: 502, body: 'bad gateway' }))

    await expect(internalApiBinaryCall(makeEvent(), '/me/invoices/7/pdf')).rejects.toMatchObject({
      statusCode: 502,
      data: { code: null, title: null, errors: null }
    })
  })
})
