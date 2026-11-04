/**
 * C# backend API utility: server-side only
 *
 * All backend API calls go through `internalApiCall`, or `internalApiBinaryCall` when the answer is
 * a file. Both:
 * - Reads `apiUrl` from runtimeConfig (set via API_URL env var)
 * - Attaches the access token of the server-side session (`server/utils/portal-session.ts`); the
 *   browser holds only the opaque session cookie, never a token
 * - Forwards the browser's `X-Language` on every backend call (the retry after a refresh included)
 *   so the backend answers its sentences in the reader's language
 * - Forwards the visitor's address in `X-Forwarded-For`, so the backend rate limits and records
 *   the visitor rather than this server; the backend honours it only from a configured proxy. The
 *   address is read from the right of the incoming header by the configured number of trusted
 *   proxies (`server/utils/clientAddress.ts`), never from the leftmost entry a visitor can write
 * - Sliding session: when the backend answers with a renewed access token
 *   (`X-Renewed-Access-Token`), stores it in the session so it outlives the 15-minute token for as
 *   long as the person keeps working
 * - On 401: refreshes the session's token with its refresh token and retries the request once
 * - Throws appropriate H3 errors on failure; the sentence is always the backend's own `detail`,
 *   and the backend's `Retry-After` and `X-Request-Id` travel to the browser with it
 * - `internalApiStream` opens a server-sent event stream and hands back the open response, so a
 *   route can pass the events on as they arrive instead of buffering the whole reply
 *
 * @module server/utils/api
 */

import type { H3Event } from 'h3'
import { LANGUAGE_HEADER, resolveLanguage } from '~/utils/transportLanguage'
import { CSRF_HEADER, CSRF_HEADER_VALUE } from '~/constants/csrfHeader'
import { HttpMethod } from '~/types/httpmethod'
import { resolveClientAddress } from '~/server/utils/clientAddress'
import { refreshSession, resolveAccessToken, updateAccessToken } from '~/server/utils/portal-session'

/** What a failed backend call exposes that `backendError` reads. */
interface UpstreamError {
  /** The backend's JSON body, when it sent one. */
  data?: unknown
  /** The backend's answer, whose headers a refusal passes on; absent when nothing answered. */
  response?: { headers: Headers }
}

/** Options of one `internalApiCall`. */
interface InternalApiOptions {
  /** HTTP method; GET when absent. */
  method?: HttpMethod
  // `object` rather than `Record<string, unknown>`: the routes pass their typed body interfaces
  // (`ProfileUpdate`, `EmailChangeInput` and the rest), and an interface has no index signature, so it
  // would not fit a record type. `$fetch` serialises any plain object to JSON.
  /** Request body: a JSON object, or an already encoded body. */
  body?: object | BodyInit | null
  /** Extra request headers. */
  headers?: Record<string, string>
  /** Omit the bearer token, for the calls made before a session exists. */
  anonymous?: boolean
  /** Aborts the backend call, e.g. when the browser that asked has gone away. */
  signal?: AbortSignal
}

/** Request header that carries the visitor's address to the backend. */
const FORWARDED_FOR_HEADER = 'X-Forwarded-For'

/** Response header the backend uses to hand a bearer caller a renewed access token. */
const RENEWED_TOKEN_HEADER = 'x-renewed-access-token'

/**
 * The backend headers a refusal passes on to the browser, each with the shape it may have. A
 * throttled caller needs `Retry-After` to know when to come back, and support needs the backend's
 * `X-Request-Id` to find the failing request in its log; anything else the backend sends stays
 * behind, and a value of another shape is dropped rather than echoed.
 */
const FORWARDED_ERROR_HEADERS: ReadonlyArray<{ name: string, shape: RegExp }> = [
  { name: 'Retry-After', shape: /^\d{1,6}$/ },
  { name: 'X-Request-Id', shape: /^[A-Za-z0-9-]{1,64}$/ }
]

/**
 * Resolve the C# backend base URL from runtime config.
 *
 * @returns The backend API base URL (`API_URL`, defaulted in `nuxt.config.ts`).
 */
export const getApiUrl = (): string => {
  const config = useRuntimeConfig()
  return config.apiUrl as string
}

/**
 * Resolves the language the backend should answer in for this request.
 *
 * Reads the `X-Language` header the browser-side `useApi` stamps and validates it against
 * `en|ru|hy`, so an arbitrary header value never reaches the backend.
 *
 * @param event - H3 event of the incoming browser request.
 * @returns A supported language code, the i18n default when the header is missing or unsupported.
 */
export const backendLanguage = (event: H3Event): string => {
  return resolveLanguage(getHeader(event, LANGUAGE_HEADER))
}

/** The problem+json `ErrorResponse` every C# backend endpoint answers a refusal with. */
interface BackendErrorBody {
  /** The machine-readable SCREAMING_SNAKE code, e.g. `CLIENT_PROFILE_NOT_FOUND`. Never localized. */
  type?: string
  /** Short localized summary; the sentence of last resort when `detail` is absent. */
  title?: string
  /** The human-readable sentence, localized by the backend. Rendered as-is. */
  detail?: string
  /** Field name to messages, present on a 400 `VALIDATION_ERROR`. */
  errors?: Record<string, string[]>
}

/**
 * Reads the backend's refusal body. A file request reads every answer as bytes, refusals
 * included, so a binary body is decoded as the JSON it is.
 *
 * @param data - The `$fetch` rejection's `data`.
 * @returns The refusal body, or undefined when there is none or it is not JSON.
 */
const errorBody = (data: unknown): BackendErrorBody | undefined => {
  if (data instanceof ArrayBuffer || ArrayBuffer.isView(data)) {
    try {
      return JSON.parse(Buffer.from(data as ArrayBuffer).toString('utf8')) as BackendErrorBody
    } catch {
      return undefined
    }
  }
  return data as BackendErrorBody | undefined
}

/**
 * Turns a failed backend call into the H3 error this route rethrows to the browser.
 *
 * The sentence (`detail`, else `title`) travels in `statusMessage`; the backend's code (`type`),
 * its `title` and its field `errors` travel in `data`, because `data` is the only other part of an
 * H3Error that `server/error.ts` serialises into the JSON body the browser receives. The browser
 * therefore never has to string-match English prose to tell one refusal from another.
 *
 * When the backend sent no body at all (it was unreachable, or answered a bare status) there is
 * no sentence to forward and none is invented here: `statusMessage` stays empty and the client's
 * one "no answer" sentence is shown. `err.message` is deliberately not a fallback: on a response
 * with no JSON body, a bare 401 above all, `$fetch` composes its own message and embeds the
 * address it called, which would publish the internal host and port of a service the browser
 * cannot reach.
 *
 * The backend's `Retry-After` and `X-Request-Id` are set on the response of `event` before the error
 * is returned, so they reach the browser beside the body.
 *
 * @param event - H3 event of the incoming browser request, whose response carries the forwarded headers.
 * @param status - HTTP status the backend answered with.
 * @param err - The `$fetch` rejection, whose `data` holds the backend's JSON body.
 * @returns An H3 error ready to be thrown.
 */
const backendError = (event: H3Event, status: number, err: UpstreamError) => {
  const data = errorBody(err?.data)

  const forwarded: Record<string, string> = {}
  for (const { name, shape } of FORWARDED_ERROR_HEADERS) {
    const value = err?.response?.headers?.get(name)
    if (value && shape.test(value)) {
      forwarded[name] = value
    }
  }
  if (Object.keys(forwarded).length > 0) {
    setResponseHeaders(event, forwarded)
  }

  return createError({
    statusCode: status,
    statusMessage: data?.detail ?? data?.title,
    data: {
      code: data?.type ?? null,
      title: data?.title ?? null,
      errors: data?.errors ?? null,
    },
  })
}

/** What a `$fetch` rejection exposes about the upstream answer. */
interface UpstreamFailure {
  /** HTTP status (ofetch). */
  status?: number
  /** HTTP status (H3-style). */
  statusCode?: number
  /** The upstream response body. */
  data?: unknown
  /** The upstream response, whose headers a refusal passes on. */
  response?: { headers: Headers }
}

/**
 * Reads the HTTP status off a `$fetch` rejection.
 *
 * @param err - Whatever `$fetch` threw.
 * @returns The upstream status, or 502 when the request never got an answer.
 */
const upstreamStatus = (err: UpstreamFailure): number => {
  return err?.status ?? err?.statusCode ?? 502
}

/**
 * Builds the headers of one backend call; the language is read from the incoming request.
 *
 * @param event - H3 event of the incoming browser request.
 * @param options - The call's options; extra headers are merged in last.
 * @param bearerToken - Access token to send, absent for an anonymous call.
 * @returns The header map.
 */
const backendHeaders = (event: H3Event, options: InternalApiOptions, bearerToken: string | undefined): Record<string, string> => {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    [LANGUAGE_HEADER]: backendLanguage(event),
    // The backend's SSO build refuses an anonymous state-changing call without it (a bearer call is exempt).
    [CSRF_HEADER]: CSRF_HEADER_VALUE,
    ...options.headers,
  }
  // The backend sees this server as the caller; without the visitor's address every guest
  // would share one rate-limit bucket and every order would record this server's address.
  const clientIp = resolveClientAddress(event)
  if (clientIp) {
    headers[FORWARDED_FOR_HEADER] = clientIp
  }
  if (bearerToken) {
    headers['Authorization'] = `Bearer ${bearerToken}`
  }
  return headers
}

/** Watches one backend answer for a renewed access token; passed to `$fetch` as `onResponse`. */
type RenewalWatcher = (context: { response: Response }) => void

/**
 * Runs one backend call with the session's bearer token: a renewed access token the backend
 * offers is stored in the session, a 401 refreshes the token and retries once, and any failure
 * becomes the H3 error the route rethrows to the browser.
 *
 * @param event - H3 event of the incoming browser request.
 * @param endpoint - API path after `/api`; auth endpoints are never retried, to avoid loops.
 * @param options - The call's options; `anonymous` sends no token and never refreshes.
 * @param send - Sends the request once with the given token, passing `watchRenewal` as `onResponse`.
 * @returns Whatever `send` resolves with.
 * @throws H3Error forwarding any upstream HTTP error status
 */
const withSession = async <T>(
  event: H3Event,
  endpoint: string,
  options: InternalApiOptions,
  send: (bearerToken: string | undefined, watchRenewal: RenewalWatcher) => Promise<T>
): Promise<T> => {
  const token = options.anonymous ? undefined : await resolveAccessToken(event)

  /**
   * Sends one attempt and keeps the renewed access token the backend offered on it, if any.
   *
   * @param bearerToken - Access token to send, absent for an anonymous call.
   * @returns Whatever `send` resolves with.
   */
  const attempt = async (bearerToken: string | undefined): Promise<T> => {
    /** The renewed access token the backend offered on this attempt, if any. */
    const renewal: { token: string | null } = { token: null }
    try {
      return await send(bearerToken, ({ response }) => {
        renewal.token = response.headers.get(RENEWED_TOKEN_HEADER)
      })
    } finally {
      // Only a bearer call belongs to a session; an anonymous call has nothing to renew.
      if (renewal.token && bearerToken) {
        // Best effort: a store failure here must not replace the backend's own error, or turn a
        // successful call into a failure. The session keeps its old token, which a refresh renews.
        await updateAccessToken(event, renewal.token).catch(() => undefined)
      }
    }
  }

  try {
    return await attempt(token)
  } catch (err: unknown) {
    const failure = err as UpstreamFailure
    const status = upstreamStatus(failure)

    // On 401: attempt token refresh and retry (skip for auth endpoints to avoid loops)
    const isAuthEndpoint = endpoint.startsWith('/auth/')
    if (status === 401 && !isAuthEndpoint && !options.anonymous) {
      const newToken = await refreshSession(event)
      if (newToken) {
        try {
          return await attempt(newToken)
        } catch (retryErr: unknown) {
          const retryFailure = retryErr as UpstreamFailure
          throw backendError(event, upstreamStatus(retryFailure), retryFailure)
        }
      }
    }

    throw backendError(event, status, failure)
  }
}

/**
 * The backend address for an endpoint.
 *
 * @param endpoint - API path after `/api`, e.g. `/clients/me`.
 * @returns The absolute URL.
 */
const backendUrl = (endpoint: string): string => {
  // Bound to a plain `string` first. Passed as a template literal, Nitro tries to match the URL
  // against its own route table and answers with a `TypedInternalResponse` rather than `T`:
  // even though this address is the external C# API and matches no Nitro route at all.
  const url: string = `${getApiUrl()}/api${endpoint}`
  return url
}

/**
 * Make an authenticated request to the internal C# backend API.
 *
 * On 401 responses, automatically attempts a token refresh and retries
 * the request once. If refresh fails, throws 401 to the client.
 *
 * @param event - H3 event from the Nuxt server route handler
 * @param endpoint - API path after `/api`, e.g. `/clients/me`
 * @param options - Optional method, body, extra headers; `anonymous` omits the bearer token for
 *   the calls made before a session exists (sign-in, registration, password reset)
 * @returns Parsed JSON response typed as `T`
 * @throws H3Error forwarding any upstream HTTP error status
 */
export const internalApiCall = <T>(
  event: H3Event,
  endpoint: string,
  options: InternalApiOptions = {}
): Promise<T> => {
  const url = backendUrl(endpoint)

  // The request type is stated as a plain `string`: left to its default, `$fetch` types the reply
  // as a union over every Nitro route, although this address is the C# API and matches none.
  return withSession(event, endpoint, options, (bearerToken, watchRenewal) =>
    $fetch<T, string>(url, {
      method: options.method ?? HttpMethod.Get,
      headers: backendHeaders(event, options, bearerToken),
      body: options.body,
      onResponse: watchRenewal,
    })
  )
}

/** A file the backend answered with, ready for a route to hand to the browser. */
export interface BinaryResponse {
  /** The file's bytes. A Buffer, because H3 serialises a bare ArrayBuffer as `{}`. */
  body: Buffer
  /** The backend's `Content-Type`. */
  contentType: string
  /** The backend's `Content-Disposition` (file name and attachment), or null when it sent none. */
  contentDisposition: string | null
  /** The backend's `Cache-Control`, or null when it sent none. */
  cacheControl: string | null
}

/**
 * Makes an authenticated GET to the backend for a file rather than JSON, with the same session,
 * token refresh and error forwarding as `internalApiCall`.
 *
 * @param event - H3 event from the Nuxt server route handler
 * @param endpoint - API path after `/api`, e.g. `/me/invoices/7/pdf`
 * @returns The file's bytes and the headers a download needs
 * @throws H3Error forwarding any upstream HTTP error status, with the backend's code and sentence
 */
export const internalApiBinaryCall = (event: H3Event, endpoint: string): Promise<BinaryResponse> => {
  const url = backendUrl(endpoint)

  return withSession(event, endpoint, {}, async (bearerToken, watchRenewal) => {
    const response = await $fetch.raw<ArrayBuffer>(url, {
      method: HttpMethod.Get,
      headers: backendHeaders(event, {}, bearerToken),
      responseType: 'arrayBuffer',
      onResponse: watchRenewal,
    })
    return {
      body: Buffer.from(response._data ?? new ArrayBuffer(0)),
      contentType: response.headers.get('content-type') ?? 'application/octet-stream',
      contentDisposition: response.headers.get('content-disposition'),
      cacheControl: response.headers.get('cache-control'),
    }
  })
}

/**
 * Opens a server-sent event stream on the backend, with the same session, token refresh and error
 * forwarding as `internalApiCall`, and hands back the open response without reading it, so the
 * route can pass each event on to the browser as it arrives.
 *
 * Native `fetch` rather than `$fetch`: `$fetch` reads the whole body before it resolves, which
 * would hold back every event until the plugin finished. Nothing of the upstream answer but its
 * body is handed on; its headers stay here.
 *
 * @param event - H3 event from the Nuxt server route handler
 * @param endpoint - API path after `/api`, e.g. `/client/plugins/acme/sites/7/chat`
 * @param options - Method (POST when absent), JSON body, and the signal that aborts the stream
 * @returns The open response; its body is the event stream
 * @throws H3Error forwarding any upstream refusal, with the backend's code and sentence
 */
export const internalApiStream = (
  event: H3Event,
  endpoint: string,
  options: InternalApiOptions = {}
): Promise<Response> => {
  const url = backendUrl(endpoint)

  return withSession(event, endpoint, options, async (bearerToken, watchRenewal) => {
    const response = await fetch(url, {
      method: options.method ?? HttpMethod.Post,
      headers: { ...backendHeaders(event, options, bearerToken), Accept: 'text/event-stream' },
      body: options.body === undefined || options.body === null ? undefined : JSON.stringify(options.body),
      signal: options.signal,
    })
    watchRenewal({ response })
    if (!response.ok) {
      // Shaped like a `$fetch` rejection, so the refresh-and-retry and `backendError` read it alike.
      const text = await response.text()
      let data: unknown
      try {
        data = text ? JSON.parse(text) : undefined
      } catch {
        data = undefined
      }
      const failure: UpstreamFailure = { status: response.status, data, response }
      throw Object.assign(new Error(`Backend stream refused with ${response.status}`), failure)
    }
    return response
  })
}
