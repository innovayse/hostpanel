/**
 * Tests for `server/utils/signon-oidc.ts`: an ID token is trusted only when its signature verifies
 * against the provider's published keys and its issuer, audience, expiry, issue time and nonce are
 * right; the issuer of an authorization response must equal the configured one. The provider is a
 * real stub that serves a discovery document and a key set, and the tokens are really signed.
 *
 * @module tests/server/utils/signon-oidc.test
 */

import { SignJWT } from 'jose'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { setRuntimeConfig, startBackend } from '../../helpers/bffHarness'
import {
  TEST_CLIENT_ID,
  TEST_DISCOVERY_PATH,
  TEST_ISSUER,
  TEST_JWKS_PATH,
  countRequests,
  createSigner,
  providerReplies,
  signIdToken
} from '../../helpers/idToken'
import type { TestSigner } from '../../helpers/idToken'
import type { StubServer } from '../../helpers/stubServer'
import { ID_TOKEN_LEEWAY_SECONDS, configuredIssuer, issuerMatches, resetSigningKeyCache, verifyIdToken } from '~/server/utils/signon-oidc'

/** The nonce the attempt sealed. */
const NONCE = 'nonce-1'

/** Another issuer a deployment may configure. */
const OTHER_ISSUER = 'https://issuer.test/realm'

/** The stub playing the provider. */
let backend: StubServer

/** The provider's signing key. */
let signer: TestSigner

/** The keys the provider currently publishes. */
let published: TestSigner[]

/**
 * The current time in whole seconds.
 *
 * @returns Seconds since the epoch.
 */
const nowSeconds = (): number => Math.floor(Date.now() / 1000)

/**
 * Makes the stub serve a provider that publishes the current keys.
 *
 * @param extra - Changes to the provider's behaviour.
 */
const serveProvider = (extra: { issuer?: string, discoveryStatus?: number, keysStatus?: number } = {}): void => {
  backend.respondWith(providerReplies({ keys: () => published.map((key) => key.jwk), ...extra }))
}

beforeEach(async () => {
  backend = await startBackend()
  setRuntimeConfig({ ssoIssuer: '', public: { ssoPublicUrl: TEST_ISSUER } })
  resetSigningKeyCache()
  signer = await createSigner()
  published = [signer]
  serveProvider()
})

afterEach(async () => {
  vi.useRealTimers()
  await backend.close()
})

describe('configuredIssuer and issuerMatches', () => {
  it('uses the public address of the provider when no issuer is set', () => {
    expect(configuredIssuer()).toBe(TEST_ISSUER)
  })

  it('prefers the issuer setting over the public address', () => {
    setRuntimeConfig({ ssoIssuer: OTHER_ISSUER })

    expect(configuredIssuer()).toBe(OTHER_ISSUER)
    expect(issuerMatches(OTHER_ISSUER)).toBe(true)
    expect(issuerMatches(TEST_ISSUER)).toBe(false)
  })

  it('compares exactly: another host, a trailing slash and an empty value do not match', () => {
    expect(issuerMatches(TEST_ISSUER)).toBe(true)
    expect(issuerMatches('https://evil.test')).toBe(false)
    expect(issuerMatches(`${TEST_ISSUER}/`)).toBe(false)
    expect(issuerMatches('')).toBe(false)
  })

  it('matches nothing when no issuer is configured at all', () => {
    setRuntimeConfig({ ssoIssuer: '', public: { ssoPublicUrl: '' } })

    expect(configuredIssuer()).toBe('')
    expect(issuerMatches('')).toBe(false)
    expect(issuerMatches(TEST_ISSUER)).toBe(false)
  })
})

describe('verifyIdToken accepts', () => {
  it('a token signed by a published key with every claim right', async () => {
    expect(await verifyIdToken(await signIdToken(signer), NONCE)).toBe(true)
  })

  it('a token whose audience list names the client and whose authorized party is the client', async () => {
    const token = await signIdToken(signer, { aud: [TEST_CLIENT_ID, 'other'], azp: TEST_CLIENT_ID })

    expect(await verifyIdToken(token, NONCE)).toBe(true)
  })

  it('a token that expired within the leeway or was issued within it', async () => {
    const token = await signIdToken(signer, { exp: nowSeconds() - 10, iat: nowSeconds() + 10 })

    expect(await verifyIdToken(token, NONCE)).toBe(true)
  })

  it('a token whose issuer is the configured issuer setting', async () => {
    setRuntimeConfig({ ssoIssuer: OTHER_ISSUER })
    serveProvider({ issuer: OTHER_ISSUER })

    expect(await verifyIdToken(await signIdToken(signer, { iss: OTHER_ISSUER }), NONCE)).toBe(true)
  })
})

describe('verifyIdToken refuses', () => {
  it('a token signed by a key the provider does not publish', async () => {
    const stranger = await createSigner('key-1')

    expect(await verifyIdToken(await signIdToken(stranger), NONCE)).toBe(false)
  })

  it('a token whose payload was changed after signing', async () => {
    const [header = '', , signature = ''] = (await signIdToken(signer)).split('.')
    const forgedPayload = Buffer.from(JSON.stringify({
      iss: TEST_ISSUER, aud: TEST_CLIENT_ID, sub: 'someone-else', nonce: NONCE, iat: nowSeconds(), exp: nowSeconds() + 300,
    })).toString('base64url')

    expect(await verifyIdToken(`${header}.${forgedPayload}.${signature}`, NONCE)).toBe(false)
  })

  it('an unsigned token (alg none) and a token signed with a symmetric algorithm', async () => {
    const now = nowSeconds()
    const claims = { iss: TEST_ISSUER, aud: TEST_CLIENT_ID, sub: 's', nonce: NONCE, iat: now, exp: now + 300 }
    const unsigned = `${Buffer.from(JSON.stringify({ alg: 'none', kid: signer.kid })).toString('base64url')}.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.`
    const symmetric = await new SignJWT(claims)
      .setProtectedHeader({ alg: 'HS256', kid: signer.kid })
      .sign(new TextEncoder().encode('a-shared-secret-of-sufficient-length'))

    expect(await verifyIdToken(unsigned, NONCE)).toBe(false)
    expect(await verifyIdToken(symmetric, NONCE)).toBe(false)
  })

  it.each([
    ['another issuer', { iss: 'https://evil.test' }],
    ['another audience', { aud: 'someone-else' }],
    ['several audiences without the client as authorized party', { aud: [TEST_CLIENT_ID, 'other'] }],
    ['several audiences with another authorized party', { aud: [TEST_CLIENT_ID, 'other'], azp: 'other' }],
    ['a token that expired beyond the leeway', { exp: nowSeconds() - ID_TOKEN_LEEWAY_SECONDS - 30 }],
    ['a token issued in the future beyond the leeway', { iat: nowSeconds() + ID_TOKEN_LEEWAY_SECONDS + 30 }],
    ['a token with no expiry', { exp: undefined }],
    ['a token with no issue time', { iat: undefined }],
    ['a token with no subject', { sub: undefined }],
    ['a token with no nonce', { nonce: undefined }],
    ['a token with another nonce', { nonce: 'nonce-of-another-attempt' }],
    ['a token with a nonce that is not text', { nonce: 1 }],
  ])('%s', async (_name, claims) => {
    expect(await verifyIdToken(await signIdToken(signer, claims), NONCE)).toBe(false)
  })

  it.each([undefined, null, '', 42, {}, 'not.a.token', 'garbage'])('the value %j', async (value) => {
    expect(await verifyIdToken(value, NONCE)).toBe(false)
  })

  it('every token when the attempt sealed no nonce', async () => {
    expect(await verifyIdToken(await signIdToken(signer), '')).toBe(false)
  })

  it('every token when no issuer is configured', async () => {
    setRuntimeConfig({ ssoIssuer: '', public: { ssoPublicUrl: '' } })

    expect(await verifyIdToken(await signIdToken(signer), NONCE)).toBe(false)
  })

  it('every token when no client id is configured', async () => {
    setRuntimeConfig({ ssoClientId: '' })

    expect(await verifyIdToken(await signIdToken(signer), NONCE)).toBe(false)
  })
})

describe('verifyIdToken when the keys cannot be trusted or fetched', () => {
  it('refuses when the discovery document declares another issuer', async () => {
    serveProvider({ issuer: 'https://evil.test' })

    expect(await verifyIdToken(await signIdToken(signer), NONCE)).toBe(false)
  })

  it.each([[500], [404]])('refuses when the discovery document answers %i', async (status) => {
    serveProvider({ discoveryStatus: status })

    expect(await verifyIdToken(await signIdToken(signer), NONCE)).toBe(false)
  })

  it('refuses when the key set cannot be fetched, and tries again on the next token', async () => {
    serveProvider({ keysStatus: 500 })
    expect(await verifyIdToken(await signIdToken(signer), NONCE)).toBe(false)

    serveProvider()
    expect(await verifyIdToken(await signIdToken(signer), NONCE)).toBe(true)
  })

  it('refuses when the key set is empty', async () => {
    published = []

    expect(await verifyIdToken(await signIdToken(signer), NONCE)).toBe(false)
  })

  it('fetches the keys by path on the internal address, never from the public one in the document', async () => {
    await verifyIdToken(await signIdToken(signer), NONCE)

    expect(backend.requests.map((request) => request.url)).toEqual([TEST_DISCOVERY_PATH, TEST_JWKS_PATH])
  })
})

describe('verifyIdToken key caching', () => {
  it('fetches the keys once for several tokens', async () => {
    await verifyIdToken(await signIdToken(signer), NONCE)
    await verifyIdToken(await signIdToken(signer), NONCE)

    expect(countRequests(backend.requests, TEST_JWKS_PATH)).toBe(1)
  })

  it('shares one fetch between tokens verified at the same moment', async () => {
    const tokens = [await signIdToken(signer), await signIdToken(signer), await signIdToken(signer)]

    const results = await Promise.all(tokens.map((token) => verifyIdToken(token, NONCE)))

    expect(results).toEqual([true, true, true])
    expect(countRequests(backend.requests, TEST_JWKS_PATH)).toBe(1)
  })

  it('fetches the keys again once the cached set is stale', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    await verifyIdToken(await signIdToken(signer), NONCE)

    vi.setSystemTime(Date.now() + 11 * 60 * 1000)
    await verifyIdToken(await signIdToken(signer), NONCE)

    expect(countRequests(backend.requests, TEST_JWKS_PATH)).toBe(2)
  })

  it('follows a key rotation: a token signed by a new key refreshes the set once and is accepted', async () => {
    await verifyIdToken(await signIdToken(signer), NONCE)
    const rotated = await createSigner('key-2')
    published = [signer, rotated]

    expect(await verifyIdToken(await signIdToken(rotated), NONCE)).toBe(true)
    expect(countRequests(backend.requests, TEST_JWKS_PATH)).toBe(2)
  })

  it('does not refetch for every token that names an unknown key', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    await verifyIdToken(await signIdToken(signer), NONCE)
    const forged = await createSigner('never-published')

    expect(await verifyIdToken(await signIdToken(forged), NONCE)).toBe(false)
    expect(await verifyIdToken(await signIdToken(forged), NONCE)).toBe(false)
    expect(await verifyIdToken(await signIdToken(forged), NONCE)).toBe(false)

    expect(countRequests(backend.requests, TEST_JWKS_PATH)).toBe(2)
  })

  it('drops the cached keys when the configured issuer changes', async () => {
    await verifyIdToken(await signIdToken(signer), NONCE)
    setRuntimeConfig({ ssoIssuer: OTHER_ISSUER })
    serveProvider({ issuer: OTHER_ISSUER })

    expect(await verifyIdToken(await signIdToken(signer, { iss: OTHER_ISSUER }), NONCE)).toBe(true)
    expect(countRequests(backend.requests, TEST_JWKS_PATH)).toBe(2)
  })
})
