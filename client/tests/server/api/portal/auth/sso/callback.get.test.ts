/**
 * Tests for `GET /api/portal/auth/sso/callback`: the attempt is found through the returned `state`
 * alone (its own sealed transaction cookie), the code is exchanged with that attempt's PKCE verifier,
 * the tokens stay in the server-side session store and the visitor is redirected back. Several
 * attempts started side by side each complete. An answer that cannot be matched to an attempt, an
 * error answer, a missing code and a failed exchange all end in a redirect to the sign-in prompt:
 * never a JSON body, never a session, and a callback that matches no attempt deletes nothing.
 *
 * @module tests/server/api/portal/auth/sso/callback.get.test
 */

import { createHash } from 'node:crypto'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { makeEvent, refusal, setRuntimeConfig, signedIn, startBackend, storedSession } from '../../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../../helpers/bffHarness'
import { startAttempt } from '../../../../../helpers/signonAttempt'
import type { StartedAttempt } from '../../../../../helpers/signonAttempt'
import type { StubServer } from '../../../../../helpers/stubServer'
import { createTransaction, transactionCookieName } from '~/server/utils/signon-transaction'
import { SSO_FLOW_COOKIE_MAX_AGE } from '~/server/utils/auth-cookie'
import { SignonTransactionKind } from '~/types/signontransactionkind'

/** The stub playing the SSO token endpoint. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

/** The tokens the stub issues for a good code. */
const goodTokens = () => ({ body: { access_token: 'at', refresh_token: 'rt', expires_in: 900, token_type: 'Bearer' } })

/**
 * Asserts that a sign-in attempt ended without a session: the visitor is redirected to the given
 * page with the sign-in prompt, no session cookie was written and the token endpoint was never called.
 *
 * @param event - The request the route handled.
 * @param expectedPath - The page the visitor is expected to land on.
 */
const expectEndedAttempt = (event: ReturnType<typeof makeEvent>, expectedPath: string): void => {
  expect(event.redirectedTo).toBe(expectedPath)
  expect(event.written['portal_session']).toBeUndefined()
  expect(event.written['authed']).toBeUndefined()
  expect(backend.requests).toHaveLength(0)
}

/**
 * Returns from the provider for an attempt.
 *
 * @param attempt - The attempt the browser started.
 * @param query - The query the provider sent back; defaults to a code for this attempt's state.
 * @param cookies - The cookies the browser holds; defaults to the ones the attempt left.
 * @returns The event after the route handled it.
 */
const returnFrom = async (
  attempt: StartedAttempt,
  query: Record<string, string> = { code: 'c1', state: attempt.state },
  cookies: Record<string, string> = attempt.cookies
) => {
  const event = makeEvent({ query, cookies })
  await route.default(event)
  return event
}

beforeEach(async () => {
  backend = await startBackend()
  setRuntimeConfig({
    ssoCallbackUrl: 'https://portal.test/api/portal/auth/sso/callback',
    public: { ssoPublicUrl: 'https://sso.test' },
  })
  route = await import('~/server/api/portal/auth/sso/callback.get')
})

afterEach(async () => {
  vi.restoreAllMocks()
  vi.useRealTimers()
  await backend.close()
})

describe('GET /api/portal/auth/sso/callback', () => {
  it('exchanges the code with the attempt\'s verifier and keeps the tokens on the server', async () => {
    backend.respondWith(goodTokens)
    const attempt = startAttempt()

    const event = await returnFrom(attempt)

    const form = new URLSearchParams(backend.requests[0]?.body ?? '')
    expect(backend.requests[0]?.url).toBe('/connect/token')
    expect(form.get('grant_type')).toBe('authorization_code')
    expect(form.get('code')).toBe('c1')
    expect(form.get('client_id')).toBe('client')
    expect(createHash('sha256').update(form.get('code_verifier') ?? '').digest('base64url')).toBe(attempt.challenge)
    const sessionId = event.written['portal_session']?.value ?? ''
    expect(await storedSession({ portal_session: sessionId })).toMatchObject({ accessToken: 'at', refreshToken: 'rt' })
    expect(event.written['portal_session']?.options).toMatchObject({ httpOnly: true, sameSite: 'lax', path: '/', maxAge: 30 * 24 * 60 * 60 })
    expect(event.written['auth_token']).toBeUndefined()
    expect(event.written['refresh_token']).toBeUndefined()
    expect(event.written['authed']?.value).toBe('1')
    expect(event.deleted).toContain('sso_silent_tried')
    expect(event.redirectedTo).toBe('/client/dashboard')
  })

  it('consumes the attempt: it deletes its transaction cookie, and the legacy flow cookies it sees, and nothing else', async () => {
    backend.respondWith(goodTokens)
    const attempt = startAttempt()
    const legacy = { oauth_state: 'old', pkce_verifier: 'old', post_login_redirect: '/x', sso_silent_return: '/y' }

    const event = await returnFrom(attempt, { code: 'c1', state: attempt.state }, { ...attempt.cookies, ...legacy })

    expect(event.deleted).toEqual(expect.arrayContaining([
      attempt.cookieName, 'oauth_state', 'pkce_verifier', 'post_login_redirect', 'sso_silent_return', 'sso_silent_tried',
    ]))
    // Starting the session also clears the retired token cookies; no other transaction cookie is touched.
    expect(event.deleted.filter((name) => name.includes('sso_tx_'))).toEqual([attempt.cookieName])
  })

  it('sets Referrer-Policy: no-referrer and Cache-Control: no-store on every kind of answer', async () => {
    backend.respondWith(goodTokens)
    const attempt = startAttempt()

    const success = await returnFrom(attempt)
    const unknown = await returnFrom(attempt, { code: 'c1', state: 'unknown' })
    const provider = await returnFrom(attempt, { error: 'access_denied', state: attempt.state })

    for (const event of [success, unknown, provider]) {
      expect(event.responseHeaders).toMatchObject({ 'Referrer-Policy': 'no-referrer', 'Cache-Control': 'no-store' })
    }
  })

  it('keeps every cookie far below the 4 KB browser limit however large the tokens are', async () => {
    // A refresh token of about 5 KB is what a browser silently drops when it is a cookie.
    const largeAccess = `h.${Buffer.from(JSON.stringify({ sub: 'person-1', pad: 'a'.repeat(1400) })).toString('base64url')}.s`
    const largeRefresh = 'r'.repeat(5000)
    backend.respondWith(() => ({ body: { access_token: largeAccess, refresh_token: largeRefresh, expires_in: 899, token_type: 'Bearer' } }))

    const event = await returnFrom(startAttempt())

    for (const [name, cookie] of Object.entries(event.written)) {
      expect(`${name}=${cookie.value}`.length, name).toBeLessThan(200)
    }
    expect(await storedSession({ portal_session: event.written['portal_session']?.value ?? '' }))
      .toMatchObject({ accessToken: largeAccess, refreshToken: largeRefresh, subject: 'person-1' })
  })

  it('ends a session the browser already had, so a sign-in never reuses an id', async () => {
    backend.respondWith(() => ({ body: { access_token: 'at', expires_in: 900, token_type: 'Bearer' } }))
    const previous = await signedIn('old')
    const attempt = startAttempt()

    const event = await returnFrom(attempt, { code: 'c1', state: attempt.state }, { ...attempt.cookies, ...previous })

    expect(event.written['portal_session']?.value).not.toBe(previous['portal_session'])
    expect(await storedSession(previous)).toBeNull()
  })

  it('stores no refresh token when the SSO issued none, and returns to the remembered path', async () => {
    backend.respondWith(() => ({ body: { access_token: 'at', expires_in: 900, token_type: 'Bearer' } }))

    const event = await returnFrom(startAttempt({}, '/client/invoices'))

    const stored = await storedSession({ portal_session: event.written['portal_session']?.value ?? '' })
    expect(stored?.refreshToken).toBeUndefined()
    expect(event.written['portal_session']?.options).toMatchObject({ maxAge: 900 })
    expect(event.redirectedTo).toBe('/client/invoices')
  })

  it('returns a silent probe that found a session to the page it started on', async () => {
    backend.respondWith(goodTokens)

    const event = await returnFrom(startAttempt({}, '/pricing', SignonTransactionKind.Silent))

    expect(event.redirectedTo).toBe('/pricing')
    expect(event.deleted).toContain('sso_silent_tried')
  })

  it('does not follow a sealed return path that is not a local one, and goes to the dashboard', async () => {
    backend.respondWith(goodTokens)
    const sealer = makeEvent()
    createTransaction(sealer, { state: 'forged-target', verifier: 'v', returnTo: '//elsewhere.test/', kind: SignonTransactionKind.Interactive })
    const cookies = { [transactionCookieName('forged-target')]: sealer.written[transactionCookieName('forged-target')]?.value ?? '' }
    const event = makeEvent({ query: { code: 'c1', state: 'forged-target' }, cookies })

    await route.default(event)

    expect(event.redirectedTo).toBe('/client/dashboard')
  })
})

describe('GET /api/portal/auth/sso/callback with attempts started side by side', () => {
  it('completes the first attempt after a second one was started, then completes the second (the interrupted sign-in)', async () => {
    backend.respondWith(goodTokens)
    const first = startAttempt({}, '/client/invoices')
    // A silent probe from another tab starts while the person is still on the provider's page.
    const second = startAttempt(first.cookies, '/pricing', SignonTransactionKind.Silent)
    expect(second.state).not.toBe(first.state)
    expect(Object.keys(second.cookies)).toHaveLength(2)

    const firstReturn = await returnFrom(first, { code: 'c1', state: first.state }, second.cookies)

    expect(firstReturn.redirectedTo).toBe('/client/invoices')
    expect(firstReturn.written['portal_session']).toBeDefined()
    expect(firstReturn.deleted).toContain(first.cookieName)
    expect(firstReturn.deleted).not.toContain(second.cookieName)

    // The browser keeps the cookies except the one the first return deleted.
    const remaining = { ...second.cookies }
    Reflect.deleteProperty(remaining, first.cookieName)
    const secondReturn = await returnFrom(second, { code: 'c2', state: second.state }, remaining)

    expect(secondReturn.redirectedTo).toBe('/pricing')
    expect(secondReturn.written['portal_session']).toBeDefined()
    expect(backend.requests).toHaveLength(2)
    const verifiers = backend.requests.map((request) => new URLSearchParams(request.body).get('code_verifier') ?? '')
    expect(createHash('sha256').update(verifiers[0] ?? '').digest('base64url')).toBe(first.challenge)
    expect(createHash('sha256').update(verifiers[1] ?? '').digest('base64url')).toBe(second.challenge)
  })

  it('completes the second attempt first when it returns first', async () => {
    backend.respondWith(goodTokens)
    const first = startAttempt({}, '/client/invoices')
    const second = startAttempt(first.cookies, '/client/tickets')

    const secondReturn = await returnFrom(second, { code: 'c2', state: second.state }, second.cookies)

    expect(secondReturn.redirectedTo).toBe('/client/tickets')
    expect(secondReturn.deleted).toEqual(expect.arrayContaining([second.cookieName]))
    expect(secondReturn.deleted).not.toContain(first.cookieName)
  })

  it('does not let a provider error for one attempt end another one that is still in progress', async () => {
    const first = startAttempt({}, '/client/invoices')
    const second = startAttempt(first.cookies, '/pricing', SignonTransactionKind.Silent)

    const event = await returnFrom(second, { error: 'login_required', state: second.state }, second.cookies)

    expect(event.redirectedTo).toBe('/pricing?signin=required')
    expect(event.deleted).toContain(second.cookieName)
    expect(event.deleted).not.toContain(first.cookieName)
  })
})

describe('GET /api/portal/auth/sso/callback when no attempt matches the state', () => {
  it('sends an error answer with an unknown state to the sign-in prompt and deletes nothing', async () => {
    const live = startAttempt({}, '/client/invoices')

    const event = await returnFrom(live, { error: 'x', state: 'never-started' })

    expectEndedAttempt(event, '/?signin=required')
    expect(event.deleted).toEqual([])
  })

  it('sends an error answer with no state at all to the sign-in prompt and deletes nothing', async () => {
    const live = startAttempt()

    const event = await returnFrom(live, { error: 'login_required' })

    expectEndedAttempt(event, '/?signin=required')
    expect(event.deleted).toEqual([])
  })

  it('refuses a code with a state this browser never started, deletes nothing and exchanges nothing', async () => {
    const live = startAttempt()

    const event = await returnFrom(live, { code: 'c1', state: 'other' })

    expectEndedAttempt(event, '/?signin=required')
    expect(event.deleted).toEqual([])
  })

  it('refuses a code that returns no state at all', async () => {
    const live = startAttempt()

    const event = await returnFrom(live, { code: 'c1' })

    expectEndedAttempt(event, '/?signin=required')
    expect(event.deleted).toEqual([])
  })

  it('refuses a transaction cookie moved under another state\'s name: a mismatch is refused, redirected, and deletes nothing', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    const victim = startAttempt()
    const other = startAttempt({}, '/pricing')
    // The attacker presents attempt A's sealed cookie under attempt B's name, and returns B's state.
    const moved = { [other.cookieName]: victim.cookies[victim.cookieName] ?? '' }

    const event = await returnFrom(other, { code: 'c1', state: other.state }, moved)

    expectEndedAttempt(event, '/?signin=required')
    expect(event.deleted).toEqual([])
    expect(warn).toHaveBeenCalledTimes(1)
  })

  it('refuses a tampered transaction cookie with a warning, and deletes nothing', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    const attempt = startAttempt()
    const value = attempt.cookies[attempt.cookieName] ?? ''
    const cookies = { [attempt.cookieName]: `${value.slice(0, -2)}${value.endsWith('AA') ? 'BB' : 'AA'}` }

    const event = await returnFrom(attempt, { code: 'c1', state: attempt.state }, cookies)

    expectEndedAttempt(event, '/?signin=required')
    expect(event.deleted).toEqual([])
    expect(warn).toHaveBeenCalledTimes(1)
  })

  it('ends an attempt that outlived its lifetime as an ordinary event, logged below warning level, deleting nothing', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    const info = vi.spyOn(console, 'info').mockImplementation(() => undefined)
    const attempt = startAttempt({}, '/client/invoices')
    vi.setSystemTime(Date.now() + SSO_FLOW_COOKIE_MAX_AGE * 1000 + 1000)

    const event = await returnFrom(attempt)

    expectEndedAttempt(event, '/?signin=required')
    expect(event.deleted).toEqual([])
    expect(warn).not.toHaveBeenCalled()
    expect(info).toHaveBeenCalledTimes(1)
    expect(info.mock.calls[0]?.[0]).toContain('expired')
  })

  it('ignores the old shared flow cookies: they cannot authorize a callback and are not deleted', async () => {
    const event = makeEvent({
      query: { code: 'c1', state: 'st' },
      cookies: { oauth_state: 'st', pkce_verifier: 'ver', post_login_redirect: '/client/invoices' },
    })

    await route.default(event)

    expectEndedAttempt(event, '/?signin=required')
    expect(event.deleted).toEqual([])
  })

  it('keeps the silent sign-in loop guard when it ends an attempt', async () => {
    const attempt = startAttempt()

    const event = await returnFrom(attempt, { code: 'c1', state: 'other' }, { ...attempt.cookies, sso_silent_tried: '1' })

    expect(event.deleted).not.toContain('sso_silent_tried')
  })

  it('logs a fixed reason and never the state, the code or a cookie', async () => {
    const info = vi.spyOn(console, 'info').mockImplementation(() => undefined)

    await returnFrom(startAttempt(), { code: 'secret-code-123', state: 'secret-state-456' })

    const logged = JSON.stringify(info.mock.calls)
    expect(logged).toContain('unknown_state')
    expect(logged).not.toContain('secret-code-123')
    expect(logged).not.toContain('secret-state-456')
  })
})

describe('GET /api/portal/auth/sso/callback for an attempt that finds no session', () => {
  it('returns a guest to the page they came from with the sign-in prompt when the SSO answers login_required', async () => {
    const attempt = startAttempt({}, '/pricing', SignonTransactionKind.Silent)

    const event = await returnFrom(attempt, { error: 'login_required', state: attempt.state })

    expect(event.redirectedTo).toBe('/pricing?signin=required')
    expect(event.deleted).toEqual([attempt.cookieName, 'oauth_state', 'pkce_verifier', 'post_login_redirect', 'sso_silent_return'])
    expect(event.written['portal_session']).toBeUndefined()
    expect(backend.requests).toHaveLength(0)
  })

  it('keeps the account hint of the page it returns to', async () => {
    const attempt = startAttempt({}, '/hosting?authuser=a%40b.test', SignonTransactionKind.Silent)

    const event = await returnFrom(attempt, { error: 'login_required', state: attempt.state })

    expect(event.redirectedTo).toBe('/hosting?authuser=a%40b.test&signin=required')
  })

  it('returns to the home page with the prompt when the attempt remembered no page', async () => {
    const attempt = startAttempt()

    const event = await returnFrom(attempt, { error: 'login_required', state: attempt.state })

    expect(event.redirectedTo).toBe('/?signin=required')
  })

  it('ends a callback without a code in a redirect to the page of the attempt, not a 400 body', async () => {
    const attempt = startAttempt({}, '/pricing')

    const event = await returnFrom(attempt, { state: attempt.state })

    expectEndedAttempt(event, '/pricing?signin=required')
    expect(event.deleted).toContain(attempt.cookieName)
  })

  it('keeps the silent sign-in loop guard when a provider error ends the attempt', async () => {
    const attempt = startAttempt({}, '/pricing', SignonTransactionKind.Silent)

    const event = await returnFrom(attempt, { error: 'login_required', state: attempt.state }, { ...attempt.cookies, sso_silent_tried: '1' })

    expect(event.deleted).not.toContain('sso_silent_tried')
  })
})

describe('GET /api/portal/auth/sso/callback when the code exchange fails', () => {
  it('redirects to the sign-in prompt, with no JSON body, no session and no detail from the provider', async () => {
    backend.respondWith(() => ({ status: 400, body: { error: 'invalid_grant', error_description: 'internal-detail-xyz' } }))
    const info = vi.spyOn(console, 'info').mockImplementation(() => undefined)
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    const error = vi.spyOn(console, 'error').mockImplementation(() => undefined)
    const attempt = startAttempt({}, '/client/invoices')

    const event = makeEvent({ query: { code: 'c1', state: attempt.state }, cookies: attempt.cookies })
    const result = await route.default(event)

    expect(event.redirectedTo).toBe('/client/invoices?signin=required')
    expect(result).toBe('/client/invoices?signin=required')
    expect(event.written['portal_session']).toBeUndefined()
    expect(event.deleted).toContain(attempt.cookieName)
    const visible = JSON.stringify([event.redirectedTo, event.written, event.responseHeaders, result, info.mock.calls, warn.mock.calls, error.mock.calls])
    expect(visible).not.toContain('invalid_grant')
    expect(visible).not.toContain('internal-detail-xyz')
    expect(info.mock.calls[0]?.[0]).toContain('exchange_failed')
  })

  it('treats an unreachable or failing provider the same way', async () => {
    backend.respondWith(() => ({ status: 500, body: 'boom' }))
    const attempt = startAttempt()

    const event = await returnFrom(attempt)

    expect(event.redirectedTo).toBe('/?signin=required')
    expect(event.written['portal_session']).toBeUndefined()
  })
})

describe('GET /api/portal/auth/sso/callback with unsafe input', () => {
  it.each(['code', 'state', 'error'])('refuses a repeated %s key with 400 and calls no token endpoint', async (key) => {
    const attempt = startAttempt()
    const query = { code: 'c1', state: attempt.state, [key]: ['a', 'b'] as unknown as string }

    const error = await refusal(() => route.default(makeEvent({ query, cookies: attempt.cookies })))

    expect(error).toMatchObject({ statusCode: 400, data: { code: 'BAD_QUERY' } })
    expect(backend.requests).toHaveLength(0)
  })
})

describe('GET /api/portal/auth/sso/callback when the SSO address is not configured', () => {
  it('refuses with 503 and calls no token endpoint when the callback address is empty', async () => {
    const attempt = startAttempt()
    setRuntimeConfig({ ssoCallbackUrl: '' })
    const event = makeEvent({ query: { code: 'c1', state: attempt.state }, cookies: attempt.cookies })

    const error = await refusal(() => route.default(event))

    expect(error).toMatchObject({ statusCode: 503, data: { code: 'SSO_NOT_CONFIGURED' } })
    expect(event.redirectedTo).toBeUndefined()
    expect(event.deleted).toEqual([])
    expect(backend.requests).toHaveLength(0)
  })
})
