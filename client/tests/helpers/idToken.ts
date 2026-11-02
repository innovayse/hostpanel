/**
 * Test helper: a sign-on provider that signs real ID tokens.
 *
 * A `TestSigner` is a real key pair; `signIdToken` signs a token with it, and `providerReplies`
 * answers the discovery document and the key set the way a provider does, so the code under test
 * runs the real `jose` verification against real signatures and nothing of ours is mocked.
 *
 * @module tests/helpers/idToken
 */

import { SignJWT, exportJWK, generateKeyPair } from 'jose'
import type { CryptoKey, JWK } from 'jose'
import type { StubReply, StubRequest } from './stubServer'

/** The issuer the tests configure and the provider declares. */
export const TEST_ISSUER = 'https://sso.test'

/** The client id the tests configure. */
export const TEST_CLIENT_ID = 'client'

/** The path the stub serves the key set on; the discovery document names it under the public address. */
export const TEST_JWKS_PATH = '/.well-known/jwks'

/** The path the stub serves the discovery document on. */
export const TEST_DISCOVERY_PATH = '/.well-known/openid-configuration'

/** A signing key pair. */
export interface TestSigner {
  /** The key id written into the token header and the key set. */
  kid: string
  /** The private key tokens are signed with. */
  privateKey: CryptoKey
  /** The public key as the provider publishes it. */
  jwk: JWK
}

/** What a test changes about a signed token. */
export interface SignOptions {
  /** The signing algorithm; RS256 when absent. */
  alg?: string
  /** The key id in the header; the signer's own when absent. */
  kid?: string
}

/**
 * Creates a key pair.
 *
 * @param kid - The key id.
 * @returns The signer.
 */
export const createSigner = async (kid = 'key-1'): Promise<TestSigner> => {
  const { privateKey, publicKey } = await generateKeyPair('RS256', { extractable: true })
  const jwk = await exportJWK(publicKey)
  return { kid, privateKey, jwk: { ...jwk, kid, alg: 'RS256', use: 'sig' } }
}

/**
 * Signs an ID token. Every claim a valid token carries is present unless the test overrides it; an
 * override set to `undefined` removes the claim.
 *
 * @param signer - The key to sign with.
 * @param claims - Claims to add or replace.
 * @param options - Header changes.
 * @returns The compact token.
 */
export const signIdToken = async (
  signer: TestSigner,
  claims: Record<string, unknown> = {},
  options: SignOptions = {}
): Promise<string> => {
  const now = Math.floor(Date.now() / 1000)
  const merged: Record<string, unknown> = {
    iss: TEST_ISSUER, aud: TEST_CLIENT_ID, sub: 'person-1', nonce: 'nonce-1', iat: now, exp: now + 300, ...claims,
  }
  for (const [name, value] of Object.entries(merged)) {
    if (value === undefined) {
      Reflect.deleteProperty(merged, name)
    }
  }
  return new SignJWT(merged as Record<string, never>)
    .setProtectedHeader({ alg: options.alg ?? 'RS256', kid: options.kid ?? signer.kid })
    .sign(signer.privateKey)
}

/** How a test shapes the provider's replies. */
export interface ProviderOptions {
  /** The keys the key set holds; read on every request, so a test can rotate them. */
  keys: () => JWK[]
  /** The issuer the discovery document declares; the test issuer when absent. */
  issuer?: string
  /** The status the discovery document is served with; 200 when absent. */
  discoveryStatus?: number
  /** The status the key set is served with; 200 when absent. */
  keysStatus?: number
  /** The reply to the token endpoint, when the test uses the provider for the whole callback. */
  token?: () => StubReply
}

/**
 * Builds the stub handler of a provider: discovery, key set and (optionally) the token endpoint.
 * The discovery document names the key set under the public address, which the server cannot reach:
 * only the path may be used, on the internal address.
 *
 * @param options - How the provider answers.
 * @returns The handler to give to the stub server.
 */
export const providerReplies = (options: ProviderOptions) => (request: StubRequest): StubReply => {
  if (request.url === TEST_DISCOVERY_PATH) {
    return {
      status: options.discoveryStatus ?? 200,
      body: { issuer: options.issuer ?? TEST_ISSUER, jwks_uri: `${TEST_ISSUER}${TEST_JWKS_PATH}` },
    }
  }
  if (request.url === TEST_JWKS_PATH) {
    return { status: options.keysStatus ?? 200, body: { keys: options.keys() } }
  }
  if (request.url === '/connect/token' && options.token) {
    return options.token()
  }
  return { status: 404, body: {} }
}

/**
 * How many requests the stub received for a path.
 *
 * @param requests - The stub's recorded requests.
 * @param path - The path to count.
 * @returns The count.
 */
export const countRequests = (requests: StubRequest[], path: string): number => {
  return requests.filter((request) => request.url === path).length
}
