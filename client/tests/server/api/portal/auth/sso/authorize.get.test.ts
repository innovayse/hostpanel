/**
 * Tests for `GET /api/portal/auth/sso/authorize`: it starts the authorization-code flow with PKCE,
 * seals the verifier, state, return path and kind of attempt into one short-lived httpOnly transaction
 * cookie, and redirects to the SSO.
 *
 * @module tests/server/api/portal/auth/sso/authorize.get.test
 */

import { createHash } from 'node:crypto'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, setRuntimeConfig, startBackend, refusal } from '../../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../../helpers/bffHarness'
import type { StubServer } from '../../../../../helpers/stubServer'
import { writtenTransaction } from '../../../../../helpers/signonAttempt'
import { SSO_FLOW_COOKIE_MAX_AGE } from '~/server/utils/auth-cookie'
import { transactionCookieName } from '~/server/utils/signon-transaction'
import { SignonTransactionKind } from '~/types/signontransactionkind'

/** The stub standing in for the SSO (the route only redirects to it). */
let backend: StubServer

/** The route under test. */
let route: RouteModule

/**
 * The return path the route sealed into its transaction.
 *
 * @param event - The event the route handled.
 * @returns The sealed return path, or null when none was remembered.
 */
const returnToOf = (event: ReturnType<typeof makeEvent>): string | null => {
  const state = new URL(event.redirectedTo ?? '').searchParams.get('state')
  return writtenTransaction(event, state)?.returnTo ?? null
}

beforeEach(async () => {
  backend = await startBackend()
  setRuntimeConfig({
    ssoClientId: 'client',
    ssoCallbackUrl: 'https://portal.test/api/portal/auth/sso/callback',
    public: { ssoPublicUrl: 'https://sso.test' },
  })
  route = await import('~/server/api/portal/auth/sso/authorize.get')
})

afterEach(async () => {
  await backend.close()
})

describe('GET /api/portal/auth/sso/authorize', () => {
  it('redirects to the SSO authorize endpoint with a PKCE challenge that matches the stored verifier', async () => {
    const event = makeEvent()

    await route.default(event)

    const target = new URL(event.redirectedTo ?? '')
    expect(target.origin + target.pathname).toBe('https://sso.test/connect/authorize')
    expect(target.searchParams.get('client_id')).toBe('client')
    expect(target.searchParams.get('response_type')).toBe('code')
    expect(target.searchParams.get('redirect_uri')).toBe('https://portal.test/api/portal/auth/sso/callback')
    expect(target.searchParams.get('code_challenge_method')).toBe('S256')
    const state = target.searchParams.get('state')
    const transaction = writtenTransaction(event, state)
    expect(transaction?.state).toBe(state)
    expect(target.searchParams.get('code_challenge')).toBe(createHash('sha256').update(transaction?.verifier ?? '').digest('base64url'))
    expect(event.written[transactionCookieName(state ?? '')]?.options).toMatchObject({ httpOnly: true, maxAge: SSO_FLOW_COOKIE_MAX_AGE })
    expect(Object.keys(event.written)).toHaveLength(1)
  })

  it('adds the prompt parameter when the query carries one', async () => {
    const event = makeEvent({ query: { prompt: 'select_account' } })

    await route.default(event)

    expect(new URL(event.redirectedTo ?? '').searchParams.get('prompt')).toBe('select_account')
  })

  it('records a prompt=none request as a silent attempt, and anything else as an interactive one', async () => {
    const silent = makeEvent({ query: { prompt: 'none', returnTo: '/pricing' } })
    const interactive = makeEvent({ query: { prompt: 'select_account' } })
    const plain = makeEvent()

    await route.default(silent)
    await route.default(interactive)
    await route.default(plain)

    const sealedBy = (event: ReturnType<typeof makeEvent>) =>
      writtenTransaction(event, new URL(event.redirectedTo ?? '').searchParams.get('state'))
    expect(new URL(silent.redirectedTo ?? '').searchParams.get('prompt')).toBe('none')
    expect(sealedBy(silent)).toMatchObject({ kind: SignonTransactionKind.Silent, returnTo: '/pricing' })
    expect(sealedBy(interactive)?.kind).toBe(SignonTransactionKind.Interactive)
    expect(sealedBy(plain)?.kind).toBe(SignonTransactionKind.Interactive)
  })

  it('passes an authuser address on as login_hint', async () => {
    const event = makeEvent({ query: { authuser: ' person@example.test ' } })

    await route.default(event)

    expect(new URL(event.redirectedTo ?? '').searchParams.get('login_hint')).toBe('person@example.test')
  })

  it('sends no login_hint without an authuser, or for one too long to be an address', async () => {
    const plain = makeEvent()
    const oversized = makeEvent({ query: { authuser: `${'a'.repeat(250)}@example.test` } })

    await route.default(plain)
    await route.default(oversized)

    expect(new URL(plain.redirectedTo ?? '').searchParams.has('login_hint')).toBe(false)
    expect(new URL(oversized.redirectedTo ?? '').searchParams.has('login_hint')).toBe(false)
  })

  it('remembers a local returnTo path', async () => {
    const event = makeEvent({ query: { returnTo: '/client/invoices' } })

    await route.default(event)

    expect(returnToOf(event)).toBe('/client/invoices')
  })

  it('falls back to the referer path when no returnTo is given', async () => {
    const event = makeEvent({ headers: { referer: 'https://portal.test/client/tickets?x=1' } })

    await route.default(event)

    expect(returnToOf(event)).toBe('/client/tickets')
  })

  it('refuses to remember an api path or an off-site address as the return path', async () => {
    const api = makeEvent({ query: { returnTo: '/api/portal/x' } })
    const offSite = makeEvent({ query: { returnTo: 'https://evil.test/x' } })

    await route.default(api)
    await route.default(offSite)

    expect(returnToOf(api)).toBeNull()
    expect(returnToOf(offSite)).toBeNull()
  })
})

describe('GET /api/portal/auth/sso/authorize with unsafe input', () => {
  it.each(['prompt', 'authuser', 'returnTo'])('refuses a repeated %s key with 400 and sets no cookie', async (key) => {
    const event = makeEvent({ query: { [key]: ['a', 'b'] as unknown as string } })

    const error = await refusal(() => route.default(event))

    expect(error).toMatchObject({ statusCode: 400, data: { code: 'BAD_QUERY' } })
    expect(event.redirectedTo).toBeUndefined()
    expect(event.written).toEqual({})
  })

  it.each(['//evil.test/x', '/\\evil.test', '/ok\u0000x'])('does not remember the return path %j', async (returnTo) => {
    const event = makeEvent({ query: { returnTo } })

    await route.default(event)

    expect(returnToOf(event)).toBeNull()
  })

  it('writes the transaction cookie httpOnly, Secure outside development, SameSite=Lax', async () => {
    const event = makeEvent({ query: { returnTo: '/client' } })

    await route.default(event)

    const [name] = Object.keys(event.written)
    expect(name).toMatch(/^__Host-sso_tx_[0-9a-f]{16}$/)
    expect(event.written[name ?? '']?.options).toMatchObject({ httpOnly: true, secure: true, sameSite: 'lax', path: '/', maxAge: SSO_FLOW_COOKIE_MAX_AGE })
  })
})

describe('GET /api/portal/auth/sso/authorize when the SSO address is not configured', () => {
  it('refuses with 503 and sets no cookie when the public SSO address is empty', async () => {
    setRuntimeConfig({ ssoCallbackUrl: 'https://portal.test/api/portal/auth/sso/callback', public: { ssoPublicUrl: '' } })
    const event = makeEvent()

    const error = await refusal(() => route.default(event))

    expect(error).toMatchObject({ statusCode: 503, data: { code: 'SSO_NOT_CONFIGURED' } })
    expect(event.redirectedTo).toBeUndefined()
    expect(event.written).toEqual({})
  })

  it('refuses with 503 when the callback address is empty', async () => {
    setRuntimeConfig({ ssoCallbackUrl: '', public: { ssoPublicUrl: 'https://sso.test' } })
    const event = makeEvent()

    const error = await refusal(() => route.default(event))

    expect(error).toMatchObject({ statusCode: 503, data: { code: 'SSO_NOT_CONFIGURED' } })
    expect(event.redirectedTo).toBeUndefined()
  })
})
