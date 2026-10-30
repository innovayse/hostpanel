/**
 * Unit tests for `constants/sessionCookie.ts`.
 *
 * @module tests/constants/sessionCookie.test
 */

import { describe, expect, it } from 'vitest'
import {
  HOST_COOKIE_PREFIX,
  SSO_TRANSACTION_COOKIE_STEM,
  SSO_TRANSACTION_NAME_HASH_LENGTH,
  SessionCookie
} from '~/constants/sessionCookie'

describe('SessionCookie', () => {
  it('keeps the wire names of the session cookies', () => {
    expect(SessionCookie.Session).toBe('portal_session')
    expect(SessionCookie.Authed).toBe('authed')
  })

  it('keeps the wire names of the legacy token cookies it still clears', () => {
    expect(SessionCookie.LegacyAuthToken).toBe('auth_token')
    expect(SessionCookie.LegacyRefreshToken).toBe('refresh_token')
  })

  it('keeps the wire name of the silent sign-in loop guard', () => {
    expect(SessionCookie.SsoSilentTried).toBe('sso_silent_tried')
  })

  it('keeps the wire names of the old shared sign-in cookies it still clears', () => {
    expect(SessionCookie.LegacyPkceVerifier).toBe('pkce_verifier')
    expect(SessionCookie.LegacyOauthState).toBe('oauth_state')
    expect(SessionCookie.LegacyPostLoginRedirect).toBe('post_login_redirect')
    expect(SessionCookie.LegacySsoSilentReturn).toBe('sso_silent_return')
  })

  it('keeps the parts of the sign-in transaction cookie name', () => {
    expect(SSO_TRANSACTION_COOKIE_STEM).toBe('sso_tx_')
    expect(SSO_TRANSACTION_NAME_HASH_LENGTH).toBe(16)
    expect(HOST_COOKIE_PREFIX).toBe('__Host-')
  })

  it('keeps the wire names of the legacy account cookie', () => {
    expect(SessionCookie.PendingAccount).toBe('inno_pending_account')
  })

  it('gives every cookie a distinct name', () => {
    const names = Object.values(SessionCookie)
    expect(new Set(names).size).toBe(names.length)
  })
})
