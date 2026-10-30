/**
 * Tests for the shared refresh-token cookie lifetime in `server/utils/auth-cookie.ts`.
 *
 * @module tests/server/utils/auth-cookie.test
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  ACCESS_TOKEN_FALLBACK_MAX_AGE,
  AUTHED_FLAG_MAX_AGE,
  REFRESH_TOKEN_MAX_AGE,
  SSO_FLOW_COOKIE_MAX_AGE,
  SSO_SILENT_COOKIE_MAX_AGE,
  authCookieMaxAge,
  cookieOptions,
  readTokenExpiry,
  readTokenPayload
} from '~/server/utils/auth-cookie'

describe('REFRESH_TOKEN_MAX_AGE', () => {
  it('is thirty days in seconds, the lifetime the identity provider grants a refresh token', () => {
    expect(REFRESH_TOKEN_MAX_AGE).toBe(30 * 24 * 60 * 60)
  })
})

describe('the other cookie lifetimes', () => {
  it('keeps the flag at seven days, the sign-in round trip at fifteen minutes and the silent attempt at thirty seconds', () => {
    expect(AUTHED_FLAG_MAX_AGE).toBe(7 * 24 * 60 * 60)
    expect(SSO_FLOW_COOKIE_MAX_AGE).toBe(900)
    expect(SSO_SILENT_COOKIE_MAX_AGE).toBe(30)
  })

  it('assumes a fifteen-minute access token when none is stated', () => {
    expect(ACCESS_TOKEN_FALLBACK_MAX_AGE).toBe(900)
  })
})

/**
 * Builds an unsigned JWT-shaped string around a payload.
 *
 * @param payload - The claims.
 * @returns The compact token.
 */
const tokenWith = (payload: Record<string, unknown>): string => {
  return `h.${Buffer.from(JSON.stringify(payload)).toString('base64url')}.s`
}

describe('readTokenPayload', () => {
  it('decodes the claims of a JWT', () => {
    expect(readTokenPayload(tokenWith({ sub: 'person-1', exp: 5 }))).toEqual({ sub: 'person-1', exp: 5 })
  })

  it('answers null for an opaque token, a broken payload or a payload that is not an object', () => {
    expect(readTokenPayload('opaque')).toBeNull()
    expect(readTokenPayload('h.%%%.s')).toBeNull()
    expect(readTokenPayload(`h.${Buffer.from('42').toString('base64url')}.s`)).toBeNull()
  })
})

describe('readTokenExpiry', () => {
  it('reads a numeric exp and ignores anything else', () => {
    expect(readTokenExpiry(tokenWith({ exp: 1234 }))).toBe(1234)
    expect(readTokenExpiry(tokenWith({ exp: 'soon' }))).toBeNull()
    expect(readTokenExpiry('opaque')).toBeNull()
  })
})

describe('authCookieMaxAge', () => {
  it('equals exp minus now', () => {
    expect(authCookieMaxAge(tokenWith({ exp: 1_000_900 }), 1_000_000)).toBe(900)
  })

  it('never goes negative for an expired token', () => {
    expect(authCookieMaxAge(tokenWith({ exp: 10 }), 1_000_000)).toBe(0)
  })

  it('gives a session cookie when exp is missing or the token is not a JWT', () => {
    expect(authCookieMaxAge(tokenWith({ sub: 'x' }), 1_000_000)).toBeUndefined()
    expect(authCookieMaxAge('opaque', 1_000_000)).toBeUndefined()
  })
})

describe('cookieOptions', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
  })

  it('is httpOnly, SameSite=Lax, site-wide and Secure, with the lifetime given', () => {
    vi.stubEnv('NODE_ENV', 'production')

    expect(cookieOptions(300)).toEqual({ maxAge: 300, httpOnly: true, secure: true, sameSite: 'lax', path: '/' })
  })

  it('stays Secure in every environment but development', () => {
    for (const environment of ['production', 'test', 'staging']) {
      vi.stubEnv('NODE_ENV', environment)

      expect(cookieOptions(1).secure, environment).toBe(true)
    }
    vi.stubEnv('NODE_ENV', 'development')

    expect(cookieOptions(1).secure).toBe(false)
  })

  it('is readable by page scripts only when asked, for the authed hint', () => {
    expect(cookieOptions(AUTHED_FLAG_MAX_AGE, false)).toMatchObject({ httpOnly: false, secure: true, sameSite: 'lax', path: '/' })
  })

  it('clears a cookie with the attributes it was written with', () => {
    expect(cookieOptions(0)).toMatchObject({ maxAge: 0, httpOnly: true, secure: true, path: '/' })
  })
})
