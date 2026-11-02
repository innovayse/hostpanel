/**
 * The checks that bind a sign-in response to the provider that sent it and to the attempt that asked
 * for it: the issuer of the authorization response (RFC 9207) and the ID token of the token response
 * (OpenID Connect Core 3.1.3.7).
 *
 * The expected issuer is the `ssoIssuer` runtime setting (`SSO_ISSUER`) and, when that is empty, the
 * public address of the provider. It is compared exactly, as OpenID Connect requires, so it must be
 * written the way the provider's own `issuer` is.
 *
 * An ID token is accepted only when every one of these holds: its signature verifies against a key
 * of the provider's JWKS, `iss` is the configured issuer, `aud` holds this portal's client id (and
 * `azp` is that id when there are several audiences), `exp` has not passed and `iat` is not in the
 * future (both with a small leeway for clock drift), `sub` is present and `nonce` is the one sealed
 * into the attempt. Anything else, including any failure to fetch the keys, refuses the token: the
 * checks fail closed and never explain themselves to the caller.
 *
 * The JWKS is found through the provider's `openid-configuration`, read over the internal sign-on
 * address like every other server-to-server call. The configuration's `issuer` must equal the
 * configured one, and its `jwks_uri` is fetched by path on the internal address, because the public
 * address in a discovery document is the one browsers use and a server may not be able to reach it.
 * The keys are cached for a few minutes; a token signed by a key that is not cached triggers one
 * refresh (at most once in a while), so a key rotation does not refuse sign-ins.
 *
 * @module server/utils/signon-oidc
 */

import { createLocalJWKSet, errors, jwtVerify } from 'jose'
import type { JSONWebKeySet, JWTPayload } from 'jose'
import { signOnFetch } from '~/server/utils/signon-http'
import { sameText } from '~/server/utils/signon-transaction'

/** How far a clock may be off, in seconds, before `exp` or `iat` refuses a token. */
export const ID_TOKEN_LEEWAY_SECONDS = 60

/** How long a fetched key set is used before it is fetched again, in milliseconds. */
const KEY_CACHE_MS = 10 * 60 * 1000

/** The shortest time between two refreshes forced by an unknown key id, in milliseconds. */
const KEY_REFRESH_COOLDOWN_MS = 30 * 1000

/** The claims every ID token of ours must carry. */
const REQUIRED_CLAIMS = ['iss', 'aud', 'exp', 'iat', 'sub', 'nonce']

/** The signature algorithms accepted: asymmetric ones only, so a token can never be signed with a public key as a secret. */
const ACCEPTED_ALGORITHMS = ['RS256', 'RS384', 'RS512', 'PS256', 'PS384', 'PS512', 'ES256', 'ES384', 'ES512', 'EdDSA']

/** The path of the provider's discovery document. */
const DISCOVERY_PATH = '/.well-known/openid-configuration'

/** The part of the discovery document that is read. */
interface DiscoveryDocument {
  /** The provider's issuer identifier. */
  issuer?: unknown
  /** Where the provider publishes its signing keys. */
  jwks_uri?: unknown
}

/** A fetched key set, ready to verify with. */
interface CachedKeys {
  /** The issuer the keys were fetched for. A change of the configured issuer drops the cache. */
  issuer: string
  /** Resolves the key a token names from the fetched set. */
  resolve: ReturnType<typeof createLocalJWKSet>
  /** When the set was fetched, in milliseconds since the epoch. */
  loadedAt: number
}

/** The key set in use, if one has been fetched. */
let cachedKeys: CachedKeys | null = null

/** The fetch that is under way, shared by every verification that needs keys meanwhile. */
let loadingKeys: Promise<CachedKeys> | null = null

/** When a refresh was last forced by an unknown key id, in milliseconds since the epoch. */
let lastForcedRefresh = 0

/**
 * Forgets the cached keys. Tests call it between cases; nothing else needs it, since the cache
 * renews itself.
 */
export const resetSigningKeyCache = (): void => {
  cachedKeys = null
  loadingKeys = null
  lastForcedRefresh = 0
}

/**
 * The issuer the provider is expected to identify itself as.
 *
 * @returns `ssoIssuer` when set, otherwise the provider's public address; empty when neither is configured.
 */
export const configuredIssuer = (): string => {
  const config = useRuntimeConfig()
  const explicit = typeof config.ssoIssuer === 'string' ? config.ssoIssuer.trim() : ''
  const publicUrl = typeof config.public?.ssoPublicUrl === 'string' ? config.public.ssoPublicUrl.trim() : ''
  return explicit || publicUrl
}

/**
 * Whether an `iss` returned on the authorization response is the configured issuer.
 *
 * @param returned - The `iss` value of the callback.
 * @returns True only when an issuer is configured and the value equals it exactly.
 */
export const issuerMatches = (returned: string): boolean => {
  const expected = configuredIssuer()
  return expected !== '' && sameText(returned, expected)
}

/**
 * Fetches the provider's signing keys through its discovery document.
 *
 * @param issuer - The issuer the discovery document must declare.
 * @returns The key set.
 * @throws Error when the document or the keys cannot be fetched, or the document names another issuer.
 */
const fetchKeys = async (issuer: string): Promise<CachedKeys> => {
  const document = await signOnFetch<DiscoveryDocument>(DISCOVERY_PATH)
  if (typeof document?.issuer !== 'string' || !sameText(document.issuer, issuer) || typeof document.jwks_uri !== 'string') {
    throw new Error('discovery document refused')
  }
  const location = new URL(document.jwks_uri)
  const keys = await signOnFetch<JSONWebKeySet>(`${location.pathname}${location.search}`)
  return { issuer, resolve: createLocalJWKSet(keys), loadedAt: Date.now() }
}

/**
 * Returns the key set, fetching it when none is cached, the cache is stale or `force` is set.
 * Verifications that need keys at the same moment share one fetch.
 *
 * @param issuer - The configured issuer.
 * @param force - Fetch again even though the cached set is fresh.
 * @returns The key set.
 */
const loadKeys = (issuer: string, force: boolean): Promise<CachedKeys> => {
  if (!force && cachedKeys !== null && cachedKeys.issuer === issuer && Date.now() - cachedKeys.loadedAt < KEY_CACHE_MS) {
    return Promise.resolve(cachedKeys)
  }
  loadingKeys ??= fetchKeys(issuer)
    .then((fetched) => {
      cachedKeys = fetched
      return fetched
    })
    .finally(() => {
      loadingKeys = null
    })
  return loadingKeys
}

/**
 * Verifies the signature and the registered claims of an ID token.
 *
 * @param idToken - The compact token.
 * @param issuer - The configured issuer.
 * @param clientId - This portal's client id, which `aud` must hold.
 * @returns The verified claims.
 * @throws Error (a `jose` error) when the token is not valid.
 */
const verifyClaims = async (idToken: string, issuer: string, clientId: string): Promise<JWTPayload> => {
  const options = {
    issuer,
    audience: clientId,
    algorithms: ACCEPTED_ALGORITHMS,
    clockTolerance: ID_TOKEN_LEEWAY_SECONDS,
    requiredClaims: REQUIRED_CLAIMS,
  }
  const keys = await loadKeys(issuer, false)
  try {
    return (await jwtVerify(idToken, keys.resolve, options)).payload
  } catch (failure: unknown) {
    // A key the cache does not know may be a rotation: look once more, but not on every forged token.
    if (!(failure instanceof errors.JWKSNoMatchingKey) || Date.now() - lastForcedRefresh < KEY_REFRESH_COOLDOWN_MS) {
      throw failure
    }
    lastForcedRefresh = Date.now()
    const fresh = await loadKeys(issuer, true)
    return (await jwtVerify(idToken, fresh.resolve, options)).payload
  }
}

/**
 * Checks what `jose` does not: that `iat` is not in the future, that `nonce` is the attempt's own and
 * that a token issued for several audiences names this client as its authorized party.
 *
 * @param claims - The verified claims.
 * @param expectedNonce - The nonce sealed into the attempt.
 * @param clientId - This portal's client id.
 * @returns True when the claims are acceptable.
 */
const claimsAreAcceptable = (claims: JWTPayload, expectedNonce: string, clientId: string): boolean => {
  const nowSeconds = Math.floor(Date.now() / 1000)
  if (typeof claims.iat !== 'number' || claims.iat > nowSeconds + ID_TOKEN_LEEWAY_SECONDS) {
    return false
  }
  if (typeof claims['nonce'] !== 'string' || !sameText(claims['nonce'], expectedNonce)) {
    return false
  }
  return !Array.isArray(claims.aud) || claims.aud.length < 2 || claims['azp'] === clientId
}

/**
 * Decides whether an ID token may be trusted for the attempt that asked for it.
 *
 * @param idToken - The `id_token` of the token response.
 * @param expectedNonce - The nonce sealed into the attempt.
 * @returns True when every check passes; false for any failure, whatever its cause.
 */
export const verifyIdToken = async (idToken: unknown, expectedNonce: string): Promise<boolean> => {
  const issuer = configuredIssuer()
  const clientId = useRuntimeConfig().ssoClientId
  if (typeof idToken !== 'string' || !idToken || !expectedNonce || !issuer || typeof clientId !== 'string' || !clientId) {
    return false
  }
  try {
    const claims = await verifyClaims(idToken, issuer, clientId)
    return claimsAreAcceptable(claims, expectedNonce, clientId)
  } catch {
    // Not even the kind of failure is kept: the caller ends the attempt with one fixed reason.
    return false
  }
}
