/**
 * Shared helpers for store and api tests: build `fetch` responses (success, refusal, validation
 * refusal), stub the global `fetch` with a scripted sequence, and read back what it was called
 * with, so code is exercised through the real `useApi` transport.
 */

import { vi } from 'vitest'

/**
 * Builds a JSON response.
 *
 * @param body - Value serialised as the response body.
 * @param status - HTTP status, 200 by default.
 * @returns The response.
 */
export const json = (body: unknown, status = 200): Response => new Response(JSON.stringify(body), { status })

/**
 * Builds a `application/problem+json` refusal carrying a finished sentence.
 *
 * @param detail - The sentence the API wrote.
 * @param status - HTTP status, 400 by default.
 * @returns The response.
 */
export const problem = (detail: string, status = 400): Response =>
  json({ type: 'REFUSED', title: 'Refused', status, detail }, status)

/**
 * Stubs the global `fetch` to answer the given responses in order, then repeat the last one.
 *
 * @param responses - Responses to hand out, one per call.
 * @returns The mock, for asserting calls.
 */
export const stubFetch = (...responses: Response[]): ReturnType<typeof vi.fn> => {
  let call = 0
  // Hand out a clone each time: a Response body can be read once, and the last answer repeats.
  const mock = vi.fn(() => {
    const response = responses[Math.min(call, responses.length - 1)]
    call += 1
    return Promise.resolve(response ? response.clone() : new Response(null, { status: 500 }))
  })
  vi.stubGlobal('fetch', mock)
  return mock
}

/**
 * Stubs the global `fetch` so every call fails as if the network were down.
 *
 * @returns The mock, for asserting calls.
 */
export const stubOffline = (): ReturnType<typeof vi.fn> => {
  const mock = vi.fn().mockRejectedValue(new TypeError('offline'))
  vi.stubGlobal('fetch', mock)
  return mock
}

/**
 * Builds a 400 `application/problem+json` response carrying per-field messages.
 *
 * @param detail - The finished sentence the API wrote.
 * @param errors - Field name to its messages, as the API groups them under `errors`.
 * @returns The response.
 */
export const invalid = (detail: string, errors: Record<string, string[]>): Response =>
  json({ type: 'VALIDATION_ERROR', title: 'Validation failed', status: 400, detail, errors }, 400)

/** What one `fetch` call carried, decoded for assertions. */
export interface RecordedCall {
  /** The URL requested, including the `/api` prefix and query string. */
  url: string
  /** The HTTP verb, GET when none was set. */
  method: string
  /** The JSON body parsed, undefined when the call had no string body. */
  body: unknown
  /** The raw request init, for header or form-data assertions. */
  init: RequestInit
}

/**
 * Decodes the nth call of a stubbed `fetch`.
 *
 * @param mock - The mock returned by `stubFetch`.
 * @param index - Zero-based call number, 0 by default.
 * @returns The URL, verb, parsed body and raw init of that call.
 */
export const callOf = (mock: ReturnType<typeof vi.fn>, index = 0): RecordedCall => {
  const [url, init] = mock.mock.calls[index] as unknown as [string, RequestInit | undefined]
  const requestInit = init ?? {}
  return {
    url: String(url),
    method: requestInit.method ?? 'GET',
    body: typeof requestInit.body === 'string' ? JSON.parse(requestInit.body) : undefined,
    init: requestInit,
  }
}

/**
 * Decodes every call of a stubbed `fetch`, in order.
 *
 * @param mock - The mock returned by `stubFetch`.
 * @returns One decoded call per request made.
 */
export const callsOf = (mock: ReturnType<typeof vi.fn>): RecordedCall[] => mock.mock.calls.map((_, index) => callOf(mock, index))

/**
 * Finds the first call with a verb and a URL.
 *
 * @param mock - The mock returned by `stubFetch`.
 * @param method - The HTTP verb.
 * @param url - The request URL, as sent.
 * @returns The decoded call, or undefined when no such request was made.
 */
export const findCall = (mock: ReturnType<typeof vi.fn>, method: string, url: string): RecordedCall | undefined =>
  callsOf(mock).find((call) => call.method === method && call.url === url)

