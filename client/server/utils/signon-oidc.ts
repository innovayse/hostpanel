/**
 * The checks that bind a sign-in response to the provider that sent it and to the attempt that asked
 * for it: the issuer of the authorization response (RFC 9207) and the ID token of the token response
 * (OpenID Connect Core 3.1.3.7).
 *
 * The expected issuer is never taken from configuration as text. It is the `issuer` the provider's
 * own discovery document declares, once that document has been validated (OpenID Connect Discovery
 * 1.0 section 4.3, RFC 8414 section 3.3): the document is fetched from the configured provider, and
 * its `issuer` must denote that same provider. The configured address is the public address of the
 * provider (`SSO_PUBLIC_URL`); it and the declared issuer are compared as URLs, with a missing
 * trailing slash on either side treated as equivalent and nothing looser (same scheme, host, port
 * and path, no query, no fragment). When `SSO_ISSUER` is set it replaces that comparison and must
 * equal the declared issuer exactly, or everything fails closed.
 *
 * Once validated, the declared issuer string is the expected issuer, and both the `iss` of the
 * authorization response and the `iss` of the ID token are compared to it exactly, character for
 * character. A provider whose issuer ends in a slash therefore signs in, and a variant without the
 * slash is refused.
 *
 * An ID token is accepted only when every one of these holds: its signature verifies against a key
 * of the provider's JWKS, `iss` is the declared issuer, `aud` holds this portal's client id (and
 * `azp` is that id when there are several audiences), `exp` has not passed and `iat` is not in the
 * future (both with a small leeway for clock drift), `sub` is present and `nonce` is the one sealed
 * into the attempt. Anything else, including any failure to fetch the discovery document or the
 * keys, refuses the token and the callback: the checks fail closed and never explain themselves to
 * the caller.
 *
 * The discovery document is read over the internal sign-on address like every other server-to-server
 * call, and its `jwks_uri` is fetched by path on the internal address, because the public address in
 * a discovery document is the one browsers use and a server may not be able to reach it. The result
 * is cached for a few minutes; a token signed by a key that is not cached triggers one refresh (at
 * most once in a while), so a key rotation does not refuse sign-ins.
 *
 * @module server/utils/signon-oidc
 */

import { createLocalJWKSet, errors, jwtVerify } from 'jose'
import type { JSONWebKeySet, JWTPayload } from 'jose'
import { signOnFetch } from '~/server/utils/signon-http'
import { sameText } from '~/server/utils/signon-transaction'

/** How far a clock may be off, in seconds, before `exp` or `iat` refuses a token. */
export const ID_TOKEN_LEEWAY_SECONDS = 60

/** How long a fetched provider description is used before it is fetched again, in milliseconds. */
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

/** What the deployment configures about the provider. */
interface ProviderSettings {
  /** The public address of the provider (`SSO_PUBLIC_URL`); empty when not set. */
  publicUrl: string
  /** The explicitly configured issuer (`SSO_ISSUER`); empty when not set. */
  explicitIssuer: string
}

/** A validated provider description: the declared issuer and its signing keys. */
interface CachedProvider {
  /** The settings it was fetched for, joined into one key. A change of the settings drops the cache. */
  settingsKey: string
  /** The issuer the provider's discovery document declares, validated; the expected `iss`. */
  issuer: string
  /** Resolves the key a token names from the fetched set. */
  resolve: ReturnType<typeof createLocalJWKSet>
  /** When the description was fetched, in milliseconds since the epoch. */
  loadedAt: number
}

/** The provider description in use, if one has been fetched. */
let cachedProvider: CachedProvider | null = null

/** The fetch that is under way, shared by every verification that needs it meanwhile. */
let loadingProvider: Promise<CachedProvider> | null = null

/** When a refresh was last forced by an unknown key id, in milliseconds since the epoch. */
let lastForcedRefresh = 0

/**
 * Forgets the cached provider description. Tests call it between cases; nothing else needs it,
 * since the cache renews itself.
 */
export const resetSigningKeyCache = (): void => {
  cachedProvider = null
  loadingProvider = null
  lastForcedRefresh = 0
}

/**
 * Reads what the deployment configures about the provider.
 *
 * @returns The public address and the explicit issuer, each trimmed and empty when not set.
 */
const providerSettings = (): ProviderSettings => {
  const config = useRuntimeConfig()
  return {
    publicUrl: typeof config.public?.ssoPublicUrl === 'string' ? config.public.ssoPublicUrl.trim() : '',
    explicitIssuer: typeof config.ssoIssuer === 'string' ? config.ssoIssuer.trim() : '',
  }
}

/**
 * Reduces a provider URL to the parts that identify it, with a single trailing slash on the path
 * ignored. A URL with a query, a fragment or credentials identifies no provider and is refused.
 *
 * @param value - The URL text.
 * @returns Scheme, host with port and path, or null when it is not a usable provider URL.
 */
const providerIdentity = (value: string): string | null => {
  let url: URL
  try {
    url = new URL(value)
  } catch {
    return null
  }
  if (url.search !== '' || url.hash !== '' || url.username !== '' || url.password !== '') {
    return null
  }
  const path = url.pathname.endsWith('/') ? url.pathname.slice(0, -1) : url.pathname
  return `${url.protocol}//${url.host}${path}`
}

/**
 * Whether the issuer a discovery document declares denotes the configured provider.
 *
 * With an explicit issuer it must equal the declared one exactly. Otherwise the declared issuer and
 * the configured public address must be the same URL: same scheme, host, port and path, a trailing
 * slash on either side not counting.
 *
 * @param declared - The `issuer` of the discovery document.
 * @param settings - The configured provider settings.
 * @returns True when the declared issuer may be trusted as the expected issuer.
 */
const declaredIssuerIsAccepted = (declared: string, settings: ProviderSettings): boolean => {
  if (settings.explicitIssuer !== '') {
    return sameText(declared, settings.explicitIssuer)
  }
  if (settings.publicUrl === '') {
    return false
  }
  const wanted = providerIdentity(settings.publicUrl)
  const found = providerIdentity(declared)
  return wanted !== null && found !== null && sameText(wanted, found)
}

/**
 * Joins the settings into the key a cached description is stored under.
 *
 * @param settings - The configured provider settings.
 * @returns One string that differs whenever either setting differs.
 */
const settingsKeyOf = (settings: ProviderSettings): string => `${settings.publicUrl}\n${settings.explicitIssuer}`

/**
 * Fetches the provider's discovery document, validates its issuer and fetches the signing keys.
 *
 * @param settings - The configured provider settings.
 * @returns The validated provider description.
 * @throws Error when the document or the keys cannot be fetched, or the document names another provider.
 */
const fetchProvider = async (settings: ProviderSettings): Promise<CachedProvider> => {
  const document = await signOnFetch<DiscoveryDocument>(DISCOVERY_PATH)
  if (
    typeof document?.issuer !== 'string'
    || typeof document.jwks_uri !== 'string'
    || !declaredIssuerIsAccepted(document.issuer, settings)
  ) {
    throw new Error('discovery document refused')
  }
  const location = new URL(document.jwks_uri)
  const keys = await signOnFetch<JSONWebKeySet>(`${location.pathname}${location.search}`)
  return {
    settingsKey: settingsKeyOf(settings),
    issuer: document.issuer,
    resolve: createLocalJWKSet(keys),
    loadedAt: Date.now(),
  }
}

/**
 * Returns the validated provider description, fetching it when none is cached, the cache is stale,
 * the settings changed or `force` is set. Callers that need it at the same moment share one fetch.
 *
 * @param force - Fetch again even though the cached description is fresh.
 * @returns The provider description.
 * @throws Error when it cannot be fetched or validated.
 */
const loadProvider = (force: boolean): Promise<CachedProvider> => {
  const settings = providerSettings()
  const settingsKey = settingsKeyOf(settings)
  if (!force && cachedProvider !== null && cachedProvider.settingsKey === settingsKey && Date.now() - cachedProvider.loadedAt < KEY_CACHE_MS) {
    return Promise.resolve(cachedProvider)
  }
  loadingProvider ??= fetchProvider(settings)
    .then((fetched) => {
      cachedProvider = fetched
      return fetched
    })
    .finally(() => {
      loadingProvider = null
    })
  return loadingProvider
}

/**
 * Whether an `iss` returned on the authorization response is the provider's declared issuer.
 *
 * @param returned - The `iss` value of the callback.
 * @returns True only when the provider's discovery document validates and the value equals its
 *          declared issuer exactly; false for any failure, an unreachable provider included.
 */
export const issuerMatches = async (returned: string): Promise<boolean> => {
  try {
    const provider = await loadProvider(false)
    return sameText(returned, provider.issuer)
  } catch {
    return false
  }
}

/**
 * Verifies the signature and the registered claims of an ID token against one provider description.
 *
 * @param idToken - The compact token.
 * @param provider - The validated provider description.
 * @param clientId - This portal's client id, which `aud` must hold.
 * @returns The verified claims.
 * @throws Error (a `jose` error) when the token is not valid.
 */
const verifyWith = async (idToken: string, provider: CachedProvider, clientId: string): Promise<JWTPayload> => {
  const options = {
    issuer: provider.issuer,
    audience: clientId,
    algorithms: ACCEPTED_ALGORITHMS,
    clockTolerance: ID_TOKEN_LEEWAY_SECONDS,
    requiredClaims: REQUIRED_CLAIMS,
  }
  return (await jwtVerify(idToken, provider.resolve, options)).payload
}

/**
 * Verifies the signature and the registered claims of an ID token, refreshing the keys once when the
 * token names a key that is not cached.
 *
 * @param idToken - The compact token.
 * @param clientId - This portal's client id, which `aud` must hold.
 * @returns The verified claims.
 * @throws Error when the provider cannot be validated or the token is not valid.
 */
const verifyClaims = async (idToken: string, clientId: string): Promise<JWTPayload> => {
  const provider = await loadProvider(false)
  try {
    return await verifyWith(idToken, provider, clientId)
  } catch (failure: unknown) {
    // A key the cache does not know may be a rotation: look once more, but not on every forged token.
    if (!(failure instanceof errors.JWKSNoMatchingKey) || Date.now() - lastForcedRefresh < KEY_REFRESH_COOLDOWN_MS) {
      throw failure
    }
    lastForcedRefresh = Date.now()
    return verifyWith(idToken, await loadProvider(true), clientId)
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
  const clientId = useRuntimeConfig().ssoClientId
  if (typeof idToken !== 'string' || !idToken || !expectedNonce || typeof clientId !== 'string' || !clientId) {
    return false
  }
  try {
    const claims = await verifyClaims(idToken, clientId)
    return claimsAreAcceptable(claims, expectedNonce, clientId)
  } catch {
    // Not even the kind of failure is kept: the caller ends the attempt with one fixed reason.
    return false
  }
}
