/**
 * Tests for the silent single-sign-on middleware: a signed-out page navigation by a browser in SSO
 * mode is sent through one `prompt=none` probe, once; clients that keep no cookies (bots, previews,
 * monitors, scripted clients) and non-document requests are never probed, so nothing loops.
 *
 * @module tests/server/middleware/sso-silent.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, setRuntimeConfig, startBackend } from '../../helpers/bffHarness'
import { startAttempt } from '../../helpers/signonAttempt'
import type { MiddlewareModule } from '../../helpers/bffHarness'
import type { StubServer } from '../../helpers/stubServer'
import { AuthMode } from '~/types/authmode'
import { HttpMethod } from '~/types/httpmethod'
import { SignonTransactionKind } from '~/types/signontransactionkind'

/** The stub the harness points the runtime config at (the middleware itself calls nothing). */
let backend: StubServer

/** The middleware under test. */
let middleware: MiddlewareModule

/** The user agent of an ordinary desktop browser. */
const BROWSER_AGENT = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'

/** The headers a browser sends when it opens a page. */
const navigation = { 'user-agent': BROWSER_AGENT, 'sec-fetch-dest': 'document', accept: 'text/html,application/xhtml+xml' }

beforeEach(async () => {
  backend = await startBackend()
  setRuntimeConfig({
    authMode: AuthMode.Sso,
    ssoClientId: 'client',
    ssoCallbackUrl: 'https://portal.test/api/portal/auth/sso/callback',
    public: { ssoPublicUrl: 'https://sso.test' },
  })
  middleware = await import('~/server/middleware/sso-silent')
})

afterEach(async () => {
  await backend.close()
})

describe('sso-silent middleware', () => {
  it('redirects a signed-out page navigation to the silent authorize probe and hands it where it was going', () => {
    const event = makeEvent({ path: '/client/dashboard', headers: navigation })

    middleware.default(event)

    expect(event.redirectedTo).toBe('/api/portal/auth/sso/authorize?prompt=none&returnTo=%2Fclient%2Fdashboard')
    expect(event.written['sso_silent_return']).toBeUndefined()
    expect(event.written['sso_silent_tried']).toMatchObject({ value: '1', options: { httpOnly: true, maxAge: 30, secure: true } })
  })

  it('probes a browser that sends only an HTML Accept header', () => {
    const event = makeEvent({ path: '/hosting', headers: { 'user-agent': BROWSER_AGENT, accept: 'text/html' } })

    middleware.default(event)

    expect(event.redirectedTo).toBe('/api/portal/auth/sso/authorize?prompt=none&returnTo=%2Fhosting')
  })

  it('does nothing in local authentication mode', () => {
    setRuntimeConfig({ authMode: AuthMode.Local })
    const event = makeEvent({ path: '/client/dashboard', headers: navigation })

    middleware.default(event)

    expect(event.redirectedTo).toBeUndefined()
    expect(event.written).toEqual({})
  })

  it('leaves api calls, build assets and files alone', () => {
    for (const path of ['/api/portal/client/me', '/_nuxt/entry.js', '/__nuxt_error', '/favicon.ico']) {
      const event = makeEvent({ path, headers: navigation })

      middleware.default(event)

      expect(event.redirectedTo).toBeUndefined()
      expect(event.written).toEqual({})
    }
  })

  it('does nothing for a signed-in visitor', () => {
    const event = makeEvent({ path: '/client/dashboard', headers: navigation, cookies: { authed: '1' } })

    middleware.default(event)

    expect(event.redirectedTo).toBeUndefined()
  })

  it('does not probe a second time once the probe was tried, so a login_required answer cannot loop', () => {
    const event = makeEvent({ path: '/client/dashboard', headers: navigation, cookies: { sso_silent_tried: '1' } })

    middleware.default(event)

    expect(event.redirectedTo).toBeUndefined()
    expect(event.written).toEqual({})
  })

  it('hands an authuser address on the page to the probe, even though the address has dots', () => {
    const event = makeEvent({
      path: '/hosting?authuser=person%40example.test',
      headers: navigation,
      query: { authuser: 'person@example.test' },
    })

    middleware.default(event)

    expect(event.redirectedTo).toBe(
      '/api/portal/auth/sso/authorize?prompt=none&returnTo=%2Fhosting%3Fauthuser%3Dperson%2540example.test&authuser=person%40example.test'
    )
  })

  it('does not probe while this browser has an interactive sign-in under way, and sets no loop guard', () => {
    const { cookies } = startAttempt()
    const event = makeEvent({ path: '/client/dashboard', headers: navigation, cookies })

    middleware.default(event)

    expect(event.redirectedTo).toBeUndefined()
    expect(event.written).toEqual({})
  })

  it('still probes when the only attempt this browser holds is a silent one, or one that is not authentic', () => {
    const silent = startAttempt({}, null, SignonTransactionKind.Silent)
    const forged = { [silent.cookieName.replace(/.$/, (digit) => (digit === '0' ? '1' : '0'))]: 'forged' }

    for (const cookies of [silent.cookies, forged]) {
      const event = makeEvent({ path: '/pricing', headers: navigation, cookies })

      middleware.default(event)

      expect(event.redirectedTo).toBe('/api/portal/auth/sso/authorize?prompt=none&returnTo=%2Fpricing')
    }
  })
})

describe('sso-silent middleware with clients that are not a person opening a page', () => {
  it.each([
    ['a crawler', { 'user-agent': 'Mozilla/5.0 (compatible; Googlebot/2.1)', accept: 'text/html' }],
    ['a link preview fetcher', { 'user-agent': 'facebookexternalhit/1.1', accept: 'text/html' }],
    ['an uptime monitor', { 'user-agent': 'UptimeRobot/2.0', accept: 'text/html' }],
    ['curl', { 'user-agent': 'curl/8.4.0', accept: '*/*' }],
    ['a client with no user agent', { accept: 'text/html' }],
    ['a browser fetching a page into a frame', { 'user-agent': BROWSER_AGENT, 'sec-fetch-dest': 'iframe', accept: 'text/html' }],
    ['a browser asking for JSON', { 'user-agent': BROWSER_AGENT, accept: 'application/json' }],
  ])('is not probed: %s', (_name, headers) => {
    const event = makeEvent({ path: '/', headers })

    middleware.default(event)

    expect(event.redirectedTo).toBeUndefined()
    expect(event.written).toEqual({})
  })

  it('is not probed on a HEAD request', () => {
    const event = makeEvent({ path: '/', headers: navigation, method: HttpMethod.Head })

    middleware.default(event)

    expect(event.redirectedTo).toBeUndefined()
    expect(event.written).toEqual({})
  })

  it('is never sent round the redirect: a cookieless client gets the page on every pass', () => {
    const headers = { 'user-agent': 'curl/8.4.0', accept: '*/*' }

    for (let pass = 0; pass < 3; pass += 1) {
      const event = makeEvent({ path: '/', headers })

      middleware.default(event)

      expect(event.redirectedTo).toBeUndefined()
    }
  })

  it('does not probe the page a failed probe came back to, even for a browser that keeps no cookies', () => {
    const first = makeEvent({ path: '/pricing', headers: navigation })
    middleware.default(first)
    expect(first.redirectedTo).toBe('/api/portal/auth/sso/authorize?prompt=none&returnTo=%2Fpricing')

    // Second pass, no cookies: the callback returned to the page flagged with the sign-in prompt.
    const second = makeEvent({ path: '/pricing?signin=required', headers: navigation, query: { signin: 'required' } })
    middleware.default(second)

    expect(second.redirectedTo).toBeUndefined()
    expect(second.written).toEqual({})
  })
})
