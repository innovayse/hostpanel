/**
 * Tests for `server/utils/signon-flow.ts`: the shared start of the sign-on flow seals a fresh PKCE
 * pair, `state`, return path and kind of attempt into a transaction cookie of its own, builds the
 * authorize query, and fails closed before any cookie.
 *
 * @module tests/server/utils/signon-flow.test
 */

import { createHash } from 'node:crypto'
import { beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, setRuntimeConfig } from '../../helpers/bffHarness'
import { startAttempt } from '../../helpers/signonAttempt'
import { MAX_RETURN_PATH_LENGTH, safeReturnPath, startSignonFlow } from '~/server/utils/signon-flow'
import { openTransaction, transactionCookieName } from '~/server/utils/signon-transaction'
import { SignonTransactionKind } from '~/types/signontransactionkind'
import { SSO_FLOW_COOKIE_MAX_AGE } from '~/server/utils/auth-cookie'

beforeEach(() => {
  setRuntimeConfig({
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

  it('refuses a path longer than the sealed cookie can safely hold', () => {
    expect(safeReturnPath(`/${'a'.repeat(MAX_RETURN_PATH_LENGTH)}`)).toBeNull()
    expect(safeReturnPath(`/${'a'.repeat(MAX_RETURN_PATH_LENGTH - 1)}`)).not.toBeNull()
  })

  it.each([undefined, 42, '', 'https://elsewhere.test/x', '//elsewhere.test', '/\\elsewhere.test', '/api/portal/x'])(
    'refuses %p',
    (candidate) => {
      expect(safeReturnPath(candidate)).toBeNull()
    },
  )
})
