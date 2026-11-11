/**
 * Fetch wrapper for the Hostpanel API: the only layer that knows about transport.
 *
 * Under `AUTH_MODE=sso` it holds no credential at all: the API performs the OIDC
 * exchange itself and hands the browser an opaque, httpOnly session cookie, which the
 * browser attaches on its own and the server renews behind. A token in browser storage
 * would be readable by any script on the page.
 *
 * Under `AUTH_MODE=local` it holds no credential either. `POST /api/auth/login`
 * mints a short-lived (15 minute) JWT, but the API writes the browser's copy into an
 * httpOnly `hostpanel_session` cookie and reads it back from there when no
 * `Authorization` header is present. `sessionStorage` would expose it to injected
 * scripts, and memory-only storage would sign the operator out on every refresh.
 *
 * So there is no token slot here, in either mode. Whether a session exists is a
 * question only the server can answer, which is what `/auth/me` is for.
 */

import { i18n } from '@/i18n'
import { API_BASE } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'
import { MimeType } from '@/types/http'
import type { ServerSentEvent } from '@/types/http'
import type { DownloadedFile } from '@/types/download'

/**
 * Builds the `X-Language` header from the active i18n locale, the only header the API reads
 * to pick the language of its `detail` sentences.
 *
 * @returns The header object, read at call time so a locale switch applies to the next request.
 */
const languageHeader = (): Record<string, string> => ({ 'X-Language': i18n.global.locale.value })

/** A value a query parameter may carry; arrays become repeated keys. */
export type QueryValue = string | number | boolean | null | undefined | ReadonlyArray<string | number | boolean>

/**
 * Builds a query string from a filter object: `undefined`, `null` and empty strings are dropped,
 * arrays become repeated keys (`?id=1&id=2`).
 *
 * @param params - Parameter name to value.
 * @returns The query string with its leading `?`, or an empty string when nothing is left.
 */
export const buildQuery = (params: Record<string, QueryValue>): string => {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === '') {
      continue
    }
    if (Array.isArray(value)) {
      for (const item of value) {
        search.append(key, String(item))
      }
    } else {
      search.append(key, String(value))
    }
  }
  const text = search.toString()
  return text === '' ? '' : `?${text}`
}

/**
 * Header a cross-site page cannot set without a CORS preflight the API does not
 * grant. The API refuses any state-changing request that arrives without it.
 */
const CSRF_HEADER = { 'X-Requested-With': 'XMLHttpRequest' } as const

/**
 * An HTTP failure that kept its response body, so callers can read the reason the
 * server actually gave instead of inventing one.
 */
export class ApiError extends Error {
  /**
   * Builds the error from a failed response.
   *
   * @param status - HTTP status code the API answered with.
   * @param body - Parsed JSON body, or null when the response carried none.
   */
  constructor(
    /** HTTP status code the API answered with. */
    public readonly status: number,
    /** Parsed JSON body of the failed response, null when there was none. */
    public readonly body: unknown,
  ) {
    super(`API error: ${status}`)
    this.name = 'ApiError'
  }
}

/**
 * Turns a failed response into an {@link ApiError} that keeps its body.
 *
 * The body is read rather than discarded: every failure the API reports explains itself
 * in a problem+json `{ type, detail }`, and a page that cannot see it has no choice
 * but to make a sentence up.
 *
 * @param response - A response whose status is not 2xx.
 * @returns The error to throw, with a null body when the response was not JSON.
 */
const failureOf = async (response: Response): Promise<ApiError> => {
  const failureText = await response.text().catch(() => '')
  let failureBody: unknown = null
  try {
    failureBody = failureText ? JSON.parse(failureText) : null
  } catch {
    // Not JSON: a proxy error page, most likely. The status still means something.
    failureBody = null
  }
  return new ApiError(response.status, failureBody)
}

/**
 * Reads the file name a response offers in its `Content-Disposition` header.
 *
 * @param response - A successful response.
 * @returns The file name, or null when the header is missing or names no file.
 */
const fileNameOf = (response: Response): string | null => {
  const header = response.headers.get('Content-Disposition')
  if (header === null) {
    return null
  }
  const encoded = /filename\*\s*=\s*UTF-8''([^;]+)/i.exec(header)
  if (encoded?.[1]) {
    try {
      return decodeURIComponent(encoded[1].trim())
    } catch {
      // Malformed escape: fall back to the plain `filename`.
    }
  }
  const plain = /filename\s*=\s*"?([^";]+)"?/i.exec(header)
  const name = plain?.[1]?.trim()
  return name ? name : null
}

/**
 * Composable for making authenticated API calls via the same-origin proxy.
 *
 * @returns `request` for JSON calls, `upload` for one multipart file, `probe` for 401-tolerant reads
 * and `download` for a file read as a blob.
 */
export const useApi = () => {
  /**
   * Makes an API request to the C# backend.
   *
   * @param endpoint - API path (e.g. '/admin/dashboard/stats').
   * @param options - Native fetch options to merge.
   * @returns Parsed JSON response data.
   * @throws {ApiError} When the API answers with a non-2xx status. The response body
   * travels on the error so the caller can read the server's own wording.
   */
  const request = async <T>(endpoint: string, options: RequestInit = {}): Promise<T> => {
    const response = await fetch(`${API_BASE}${endpoint}`, {
      ...options,
      headers: {
        'Content-Type': MimeType.Json,
        ...CSRF_HEADER,
        ...languageHeader(),
        ...(options.headers as Record<string, string> | undefined),
      },
    })

    // A 401 is thrown like any other error and nothing is redirected from here.
    //
    // fetchMe always asks the server, because the browser cannot answer "am I signed in",
    // so a redirect here would fire *inside* the router guard that called it. vue-router
    // aborts the navigation in progress and nothing renders at all: a blank page, no
    // error. The guard already sends unauthenticated visitors to /login; this layer
    // only has to report what happened.
    if (!response.ok) {
      throw await failureOf(response)
    }

    if (response.status === 204) {
      return undefined as T
    }

    const text = await response.text()
    if (!text) {
      return undefined as T
    }

    return JSON.parse(text) as T
  }

  /**
   * Uploads one file as multipart form data.
   *
   * Separate from {@link request} because a fixed `Content-Type: application/json`
   * header would stop the browser writing its own multipart boundary.
   *
   * @param endpoint - API path (e.g. '/admin/storefront/upload-image').
   * @param file - The file to send, under the form field `file`.
   * @returns Parsed JSON response data.
   * @throws {ApiError} When the API answers with a non-2xx status.
   */
  const upload = async <T>(endpoint: string, file: File): Promise<T> => {
    const formData = new FormData()
    formData.append('file', file)

    const response = await fetch(`${API_BASE}${endpoint}`, {
      method: HttpMethod.Post,
      body: formData,
      headers: { ...CSRF_HEADER, ...languageHeader() },
    })

    if (!response.ok) {
      throw await failureOf(response)
    }

    return await response.json() as T
  }

  /**
   * Asks a question whose honest answer may be "no session": a 401 is the answer, not a failure.
   *
   * Used by the session probe, which runs on every cold load including the signed-out one.
   * A 401 resolves to null, so nothing is thrown and nothing is logged by the app; every
   * other failure still throws like {@link request}. (The browser's own network panel still
   * lists the 401: that line is not reachable from script.)
   *
   * @param endpoint - API path (e.g. '/auth/me').
   * @returns Parsed JSON response data, or null when the API answered 401.
   * @throws {ApiError} When the API answers with any other non-2xx status.
   */
  const probe = async <T>(endpoint: string): Promise<T | null> => {
    try {
      return await request<T>(endpoint)
    } catch (cause) {
      if (cause instanceof ApiError && cause.status === 401) {
        return null
      }
      throw cause
    }
  }

  /**
   * Fetches a file as a blob, so the page can save it and show a refusal instead of
   * navigating to it.
   *
   * @param endpoint - API path (e.g. '/billing/5/pdf').
   * @returns The file and the name the API gave it.
   * @throws {ApiError} When the API answers with a non-2xx status; the problem+json body travels on the error.
   */
  const download = async (endpoint: string): Promise<DownloadedFile> => {
    const response = await fetch(`${API_BASE}${endpoint}`, {
      headers: { ...CSRF_HEADER, ...languageHeader() },
    })

    if (!response.ok) {
      throw await failureOf(response)
    }

    return { blob: await response.blob(), fileName: fileNameOf(response) }
  }

  /**
   * Posts a JSON body to a streaming endpoint and hands each server-sent event over as it arrives.
   *
   * Read with `fetch` and the response's reader, never `EventSource`, which cannot send the CSRF and
   * language headers. A 2xx answer that is not `text/event-stream` is refused like a broken upstream.
   *
   * @param endpoint - API path (e.g. '/admin/plugins/sites/ext/chat').
   * @param body - The JSON body.
   * @param onEvent - Called with each event, in order.
   * @param signal - Aborting it closes the stream; the promise then resolves.
   * @returns Resolves when the stream ended or was aborted.
   * @throws {ApiError} When the API refuses the call, answers something other than a stream, or one
   *   event grows past {@link MAX_STREAM_EVENT_CHARS}; whatever reading throws when the connection breaks.
   */
  const stream = async (
    endpoint: string,
    body: unknown,
    onEvent: (event: ServerSentEvent) => void,
    signal: AbortSignal,
  ): Promise<void> => {
    let response: Response
    try {
      response = await fetch(`${API_BASE}${endpoint}`, {
        method: HttpMethod.Post,
        body: JSON.stringify(body),
        signal,
        headers: { 'Content-Type': MimeType.Json, Accept: MimeType.EventStream, ...CSRF_HEADER, ...languageHeader() },
      })
    } catch (cause) {
      if (signal.aborted) {
        return
      }
      throw cause
    }
    if (!response.ok) {
      throw await failureOf(response)
    }
    if (!(response.headers.get('Content-Type') ?? '').startsWith(MimeType.EventStream) || response.body === null) {
      throw new ApiError(BROKEN_STREAM_STATUS, null)
    }
    await readEventStream(response.body, onEvent, signal)
  }

  return { request, upload, probe, download, stream }
}

/** The status a stream that cannot be read is refused with, the one a broken upstream answer gets everywhere else. */
const BROKEN_STREAM_STATUS = 502

/** The event name of a server-sent event that names none. */
const DEFAULT_EVENT = 'message'

/** The most characters one unfinished event may hold before the stream is given up: 1 Mi characters. */
export const MAX_STREAM_EVENT_CHARS = 1024 * 1024

/**
 * Parses one server-sent event block (the lines between two blank lines).
 *
 * @param block - The block's text, line ends already `\n`.
 * @returns The event, or null for a block holding only comments (a heartbeat).
 */
const parseEventBlock = (block: string): ServerSentEvent | null => {
  let type = DEFAULT_EVENT
  const data: string[] = []
  for (const line of block.split('\n')) {
    if (line === '' || line.startsWith(':')) {
      continue
    }
    const colon = line.indexOf(':')
    const name = colon === -1 ? line : line.slice(0, colon)
    const value = colon === -1 ? '' : line.slice(colon + 1).replace(/^ /, '')
    if (name === 'event') {
      type = value
    } else if (name === 'data') {
      data.push(value)
    }
  }
  if (data.length === 0) {
    return null
  }
  const text = data.join('\n')
  try {
    return { type, data: JSON.parse(text) as unknown }
  } catch {
    return { type, data: text }
  }
}

/**
 * Reads a server-sent event stream to its end, handing each event over as it arrives. Line ends are
 * normalised after joining chunks, so a `\r\n` split across two chunks stays one line end.
 *
 * @param body - The response body.
 * @param onEvent - Called with each event, in order.
 * @param signal - Aborting it stops reading and releases the stream; the promise then resolves.
 * @returns Resolves when the stream ended or was aborted.
 * @throws {ApiError} With status 502 when one event passes {@link MAX_STREAM_EVENT_CHARS}; whatever reading throws otherwise.
 */
export const readEventStream = async (
  body: ReadableStream<Uint8Array>,
  onEvent: (event: ServerSentEvent) => void,
  signal: AbortSignal,
): Promise<void> => {
  const reader = body.getReader()
  const decoder = new TextDecoder()
  /**
   * Cancels the read in flight, so an abort does not wait for the next chunk.
   *
   * @returns Nothing.
   */
  const stop = (): void => {
    reader.cancel().catch(() => undefined)
  }
  signal.addEventListener('abort', stop, { once: true })
  let buffer = ''
  let heldReturn = ''
  try {
    while (!signal.aborted) {
      const { done, value } = await reader.read()
      if (done) {
        break
      }
      let text = heldReturn + decoder.decode(value, { stream: true })
      heldReturn = text.endsWith('\r') ? '\r' : ''
      text = heldReturn ? text.slice(0, -1) : text
      const from = Math.max(0, buffer.length - 1)
      buffer += text.replace(/\r\n?/g, '\n')
      let start = 0
      let end = buffer.indexOf('\n\n', from)
      while (end !== -1) {
        const event = parseEventBlock(buffer.slice(start, end))
        if (event && !signal.aborted) {
          onEvent(event)
        }
        start = end + 2
        end = buffer.indexOf('\n\n', start)
      }
      buffer = buffer.slice(start)
      // One event may not grow without end: a stream that never closes an event would hold the tab's memory.
      if (buffer.length > MAX_STREAM_EVENT_CHARS) {
        stop()
        throw new ApiError(BROKEN_STREAM_STATUS, null)
      }
    }
  } catch (cause) {
    if (!signal.aborted) {
      throw cause
    }
  } finally {
    signal.removeEventListener('abort', stop)
    reader.releaseLock()
  }
}
