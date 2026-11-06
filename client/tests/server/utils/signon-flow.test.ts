/**
 * Tests for `server/utils/signon-flow.ts`: the shared start of the sign-on flow seals a fresh PKCE
 * pair, `state`, return path and kind of attempt into a transaction cookie of its own, builds the
 * authorize query, and fails closed before any cookie.
 *
 * @module tests/server/utils/signon-flow.test
 */

import { createHash } from 'node:crypto'
import { beforeEach, describe, expect, it } from 'vitest'
import { TEST_SESSION_KEY, makeEvent, refusal, setRuntimeConfig } from '../../helpers/bffHarness'
import { startAttempt } from '../../helpers/signonAttempt'
import { MAX_RETURN_PATH_BYTES, safeReturnPath, startSignonFlow } from '~/server/utils/signon-flow'
import { openTransaction, transactionCookieName } from '~/server/utils/signon-transaction'
import { SignonTransactionKind } from '~/types/signontransactionkind'
import { SSO_FLOW_COOKIE_MAX_AGE, SSO_MAX_LIVE_TRANSACTIONS } from '~/server/utils/auth-cookie'

beforeEach(() => {
  setRuntimeConfig({
    sessionEncryptionKey: TEST_SESSION_KEY,
    ssoClientId: 'client',
    ssoCallbackUrl: 'https://portal.test/api/portal/auth/sso/callback',
    public: { ssoPublicUrl: 'https://sso.test' },
  })
})

describe('startSignonFlow', () => {
  it('returns the provider address and an authorize query whose challenge and state belong to the sealed transaction', () => {
    const event = makeEvent()

    const { ssoPublicUrl, params } = startSignonFlow(event, null)

    expect(ssoPublicUrl).toBe('https://sso.test')
    expect(params.get('client_id')).toBe('client')
    expect(params.get('response_type')).toBe('code')
    expect(params.get('redirect_uri')).toBe('https://portal.test/api/portal/auth/sso/callback')
    expect(params.get('scope')).toBe('openid profile email offline_access')
    expect(params.get('code_challenge_method')).toBe('S256')
    const state = params.get('state') ?? ''
    expect(state).toMatch(/^[0-9a-f]{32}$/)
    const cookies = { [transactionCookieName(state)]: event.written[transactionCookieName(state)]?.value ?? '' }
    const lookup = openTransaction(makeEvent({ cookies }), state)
    expect(lookup.transaction?.state).toBe(state)
    const verifier = lookup.transaction?.verifier ?? ''
    expect(params.get('code_challenge')).toBe(createHash('sha256').update(verifier).digest('base64url'))
  })

  it('sends a fresh nonce on every start and seals the same one into the transaction', () => {
    const first = makeEvent()
    const second = makeEvent()

    const firstParams = startSignonFlow(first, null).params
    const secondParams = startSignonFlow(second, null).params

    const nonce = firstParams.get('nonce') ?? ''
    expect(nonce).toMatch(/^[0-9a-f]{32}$/)
    expect(secondParams.get('nonce')).not.toBe(nonce)
    expect(nonce).not.toBe(firstParams.get('state'))
    const name = transactionCookieName(firstParams.get('state') ?? '')
    const lookup = openTransaction(makeEvent({ cookies: { [name]: first.written[name]?.value ?? '' } }), firstParams.get('state') ?? '')
    expect(lookup.transaction?.nonce).toBe(nonce)
    expect(first.written[name]?.value).not.toContain(nonce)
  })

  it('writes one sealed transaction cookie httpOnly, Secure, SameSite=Lax for the sign-in attempt lifetime, and no shared cookie', () => {
    const event = makeEvent()

    const { params } = startSignonFlow(event, '/client/invoices')

    const name = transactionCookieName(params.get('state') ?? '')
    expect(Object.keys(event.written)).toEqual([name])
    expect(event.written[name]?.options).toMatchObject({ httpOnly: true, secure: true, sameSite: 'lax', path: '/', maxAge: SSO_FLOW_COOKIE_MAX_AGE })
    expect(event.written[name]?.value).not.toContain('/client/invoices')
  })

  it('seals the return path and the kind of attempt into the transaction', () => {
    const interactive = makeEvent()
    const silent = makeEvent()

    const first = startSignonFlow(interactive, '/client/invoices').params.get('state') ?? ''
    const second = startSignonFlow(silent, null, SignonTransactionKind.Silent).params.get('state') ?? ''

    /**
     * Opens the transaction a start wrote to its response.
     *
     * @param event - The event the start wrote to.
     * @param state - The state of the attempt.
     * @returns The transaction, or null when none can be found.
     */
    const open = (event: ReturnType<typeof makeEvent>, state: string) => {
      const name = transactionCookieName(state)
      return openTransaction(makeEvent({ cookies: { [name]: event.written[name]?.value ?? '' } }), state).transaction
    }
    expect(open(interactive, first)).toMatchObject({ returnTo: '/client/invoices', kind: SignonTransactionKind.Interactive })
    expect(open(silent, second)).toMatchObject({ returnTo: null, kind: SignonTransactionKind.Silent })
  })

  it('uses a new verifier and state on every start, and a second start does not replace the first attempt', () => {
    const first = startAttempt()
    const second = startAttempt(first.cookies)

    expect(second.state).not.toBe(first.state)
    expect(second.challenge).not.toBe(first.challenge)
    expect(second.deleted).toEqual([])
    expect(Object.keys(second.cookies).sort()).toEqual([first.cookieName, second.cookieName].sort())
  })

  it.each([
    [{ ssoCallbackUrl: 'https://portal.test/cb', public: { ssoPublicUrl: '' } }],
    [{ ssoCallbackUrl: '', public: { ssoPublicUrl: 'https://sso.test' } }],
  ])('refuses with a coded 503 and writes no cookie when an address is empty (%j)', async (config) => {
    setRuntimeConfig(config)
    const event = makeEvent()

    const error = await refusal(() => startSignonFlow(event, '/client'))

    expect(error).toMatchObject({ statusCode: 503, data: { code: 'SSO_NOT_CONFIGURED' } })
    expect(event.written).toEqual({})
  })
})

describe('safeReturnPath', () => {
  it('keeps a path inside the app', () => {
    expect(safeReturnPath('/client/domains')).toBe('/client/domains')
  })

  it('limits the path by UTF-8 bytes, 512 of them', () => {
    expect(MAX_RETURN_PATH_BYTES).toBe(512)
    expect(safeReturnPath(`/${'a'.repeat(511)}`)).not.toBeNull()
    expect(safeReturnPath(`/${'a'.repeat(512)}`)).toBeNull()
  })

  it('counts multibyte characters by their bytes, not as one each', () => {
    // 1 + 170 x 3 bytes = 511 fits; 1 + 171 x 3 = 514 does not, though it is only 172 characters.
    expect(safeReturnPath(`/${'€'.repeat(170)}`)).not.toBeNull()
    expect(safeReturnPath(`/${'€'.repeat(171)}`)).toBeNull()
    // 1 + 255 x 2 bytes = 511 fits; 1 + 256 x 2 = 513 does not.
    expect(safeReturnPath(`/${'я'.repeat(255)}`)).not.toBeNull()
    expect(safeReturnPath(`/${'я'.repeat(256)}`)).toBeNull()
  })

  it('falls back to no return path for an oversized one, without an error', () => {
    const event = makeEvent()

    const { params } = startSignonFlow(event, safeReturnPath(`/${'a'.repeat(2000)}`))

    const state = params.get('state') ?? ''
    const name = transactionCookieName(state)
    const lookup = openTransaction(makeEvent({ cookies: { [name]: event.written[name]?.value ?? '' } }), state)
    expect(lookup.transaction?.returnTo).toBeNull()
  })

  it('keeps the pages the app builds, with a long query, under the limit', () => {
    const invite = `/client/accept-invite?token=${'t'.repeat(86)}`
    const filtered = '/client/invoices?status=unpaid&page=12&sort=dueDate&order=desc&search=example.test'

    expect(safeReturnPath(invite)).toBe(invite)
    expect(safeReturnPath(filtered)).toBe(filtered)
  })

  it.each([undefined, 42, '', 'https://elsewhere.test/x', '//elsewhere.test', '/\\elsewhere.test', '/api/portal/x'])(
    'refuses %p',
    (candidate) => {
      expect(safeReturnPath(candidate)).toBeNull()
    },
  )
})

describe('cookies of the worst case', () => {
  /** The limit of a request header on the servers and proxies in front of the app. */
  const HEADER_LIMIT_BYTES = 8192

  /** The session cookie as large as the app writes plus the readable flags, about 1 KB. */
  const SESSION_COOKIES = { portal_session: 's'.repeat(900), authed: '1', sso_silent_tried: '1' }

  /**
   * The size of the `Cookie` request header a browser sends for these cookies.
   *
   * @param cookies - The cookies held.
   * @returns The header size in bytes.
   */
  const headerBytes = (cookies: Record<string, string>): number => {
    return Buffer.byteLength(Object.entries(cookies).map(([name, value]) => `${name}=${value}`).join('; '), 'utf8')
  }

  it.each([
    ['ASCII', `/${'a'.repeat(1023)}`],
    ['multibyte (3 bytes each)', `/${'€'.repeat(1023)}`],
    ['multibyte (2 bytes each)', `/${'я'.repeat(1023)}`],
    ['the longest ASCII path that is kept', `/${'a'.repeat(511)}`],
    ['the longest multibyte path that is kept', `/${'€'.repeat(170)}`],
  ])('keeps five live attempts and the session cookies under 8192 bytes (%s)', (_label, candidate) => {
    let cookies: Record<string, string> = { ...SESSION_COOKIES }
    for (let index = 0; index < SSO_MAX_LIVE_TRANSACTIONS; index += 1) {
      cookies = startAttempt(cookies, safeReturnPath(candidate)).cookies
    }

    expect(Object.keys(cookies).filter(name => name.includes('sso_tx_'))).toHaveLength(SSO_MAX_LIVE_TRANSACTIONS)
    expect(headerBytes(cookies)).toBeLessThan(HEADER_LIMIT_BYTES)
  })
})
