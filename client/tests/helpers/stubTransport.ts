/**
 * Points the browser transport at a real stub server, for api-composable tests.
 *
 * An api composable is a list of paths, verbs, queries and bodies, so its test sends the real
 * request through `apiFetch` and asserts on what arrived at the stub, never by mocking the
 * transport. The Nuxt instance `apiFetch` asks for its locale is faked just enough to answer.
 *
 * @module tests/helpers/stubTransport
 */

import { ofetch } from 'ofetch'
import { expect, vi } from 'vitest'
import { startStubServer } from './stubServer'
import type { StubHandler, StubServer } from './stubServer'
import type { HttpMethod } from '~/types/httpmethod'

/**
 * Starts a stub server and makes `$fetch` and `useNuxtApp` talk to it.
 *
 * @param reply Initial reply function; defaults to `200 {}`.
 * @returns The running stub; the caller closes it after the test.
 */
export const startTransport = async (reply?: StubHandler): Promise<StubServer> => {
  const stub = await startStubServer(reply)
  vi.stubGlobal('$fetch', ofetch.create({ baseURL: stub.baseUrl }))
  vi.stubGlobal('useNuxtApp', () => ({
    $i18n: { locale: { value: 'en' } },
    runWithContext: (callback: () => Promise<void>) => callback()
  }))
  return stub
}

/** What a table row of an api test expects to arrive at the stub. */
export interface ExpectedRequest {
  /** Test title: the endpoint function and what it sends. */
  name: string
  /** Calls the endpoint function under test. */
  call: () => Promise<unknown>
  /** HTTP verb the endpoint must use. */
  method: HttpMethod
  /** Path and query string the endpoint must request. */
  url: string
  /** JSON body the endpoint must send; omitted when it sends none. */
  body?: unknown
}

/**
 * Asserts that the last request the stub received is the expected one.
 *
 * @param stub The running stub.
 * @param expected The row under test.
 */
export const expectLastRequest = (stub: StubServer, expected: ExpectedRequest): void => {
  const sent = stub.requests[stub.requests.length - 1]
  expect(sent?.method).toBe(expected.method)
  expect(sent?.url).toBe(expected.url)
  expect(sent?.headers['x-language']).toBe('en')
  if (expected.body === undefined) {
    expect(sent?.body).toBe('')
  } else {
    expect(JSON.parse(sent?.body ?? '')).toEqual(expected.body)
  }
}
