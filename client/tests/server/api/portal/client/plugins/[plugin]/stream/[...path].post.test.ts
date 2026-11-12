/**
 * Tests for `POST /api/portal/client/plugins/:plugin/stream/**`: the plugin's events reach the
 * browser as the backend writes them (the route answers before the stream ends, nothing is
 * buffered), only the route's own headers travel (no upstream header, never the session's token),
 * the backend call ends when the browser goes away, a refusal is passed on as JSON before any stream
 * opens, and a malformed path or body never reaches the backend.
 *
 * The backend here is a real `node:http` server that keeps its response open, because the stub the
 * harness starts answers in one piece and so cannot show that an event arrives before the end.
 *
 * @module tests/server/api/portal/client/plugins/[plugin]/stream/[...path].post.test
 */

import { createServer } from 'node:http'
import type { IncomingMessage, Server, ServerResponse } from 'node:http'
import type { AddressInfo } from 'node:net'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, setRuntimeConfig, signedIn, startBackend } from '../../../../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../../../../helpers/bffHarness'
import type { StubServer } from '../../../../../../../helpers/stubServer'
import { HttpMethod } from '~/types/httpmethod'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The stub the harness starts; the session store needs it as the sign-on service. */
let backend: StubServer

/** A backend that streams, written to by each test. */
let streaming: Server

/** The request the streaming backend received, its open response and the body it read. */
let open: { request: IncomingMessage, response: ServerResponse, body: string } | undefined

/** How many requests the streaming backend received. */
let received = 0

/** Whether the streaming backend saw its connection close before it ended the stream. */
let upstreamClosed = false

/** The route under test. */
let route: RouteModule

/** The headers the route answers a stream with, and nothing else. */
const STREAM_HEADERS = {
  'Content-Type': 'text/event-stream; charset=utf-8',
  'Cache-Control': 'no-cache, no-transform',
  'X-Accel-Buffering': 'no',
  'X-Content-Type-Options': 'nosniff',
}

beforeEach(async () => {
  backend = await startBackend()
  upstreamClosed = false
  open = undefined
  received = 0
  streaming = createServer((request, response) => {
    received += 1
    let body = ''
    request.on('data', (chunk: Buffer) => {
      body += chunk.toString()
    })
    request.on('end', () => {
      if (request.url?.includes('/refuse')) {
        response.writeHead(429, { 'content-type': 'application/problem+json', 'retry-after': '30' })
        response.end(JSON.stringify({ type: 'RATE_LIMITED', detail: 'Too many requests.' }))
        return
      }
      if (request.url?.includes('/missing')) {
        response.writeHead(404, { 'content-type': 'application/problem+json' })
        response.end(JSON.stringify({ type: 'PLUGIN_ROUTE_NOT_FOUND', detail: 'No such route.' }))
        return
      }
      if (request.url?.includes('/json')) {
        response.writeHead(200, { 'content-type': 'application/json' })
        response.end(JSON.stringify({ answer: 'not a stream' }))
        return
      }
      if (request.url?.includes('/slow')) {
        // Answers nothing until the browser has gone; the close is what the test watches for.
        response.on('close', () => {
          upstreamClosed = true
        })
        return
      }
      // Headers the browser must never see: an internal one and a cookie of the backend's own.
      response.writeHead(200, { 'content-type': 'text/event-stream', 'x-plugin-secret': 'internal', 'set-cookie': 'backend=1' })
      response.write('event: delta\ndata: {"text":"Hel"}\n\n')
      response.on('close', () => {
        upstreamClosed = true
      })
      open = { request, response, body }
    })
  })
  await new Promise<void>(resolve => streaming.listen(0, '127.0.0.1', resolve))
  setRuntimeConfig({ apiUrl: `http://127.0.0.1:${(streaming.address() as AddressInfo).port}` })
  route = await import('~/server/api/portal/client/plugins/[plugin]/stream/[...path].post')
})

afterEach(async () => {
  open?.response.end()
  streaming.closeAllConnections()
  await new Promise<void>(resolve => streaming.close(() => resolve()))
  await backend.close()
})

/**
 * Reads the next chunk of a stream as text.
 *
 * @param reader - The reader of the stream the route handed to the browser.
 * @returns The chunk's text.
 */
const nextText = async (reader: ReadableStreamDefaultReader<Uint8Array>): Promise<string> => {
  const { value } = await reader.read()
  return new TextDecoder().decode(value)
}

describe('plugin stream route', () => {
  it('opens the backend stream with the session, the language and the body', async () => {
    const event = makeEvent({
      method: HttpMethod.Post,
      params: { plugin: 'acme-sites', path: 'sites/7/chat' },
      query: { draft: '1' },
      body: { message: 'hi' },
      cookies: await signedIn('jwt'),
      headers: { 'x-language': 'ru' },
    })

    await route.default(event)

    expect(open?.request.method).toBe('POST')
    expect(open?.request.url).toBe('/api/client/plugins/acme-sites/sites/7/chat?draft=1')
    expect(open?.request.headers['authorization']).toBe('Bearer jwt')
    expect(open?.request.headers['x-language']).toBe('ru')
    expect(open?.request.headers['accept']).toBe('text/event-stream')
    expect(JSON.parse(open?.body ?? '')).toEqual({ message: 'hi' })
  })

  it('hands each event to the browser as it is written, before the stream ends', async () => {
    const event = makeEvent({
      method: HttpMethod.Post,
      params: { plugin: 'acme-sites', path: 'sites/7/chat' },
      body: { message: 'hi' },
      cookies: await signedIn('jwt'),
    })

    // The route resolves while the backend still holds its response open: nothing waits for the end.
    await route.default(event)
    expect(open?.response.writableEnded).toBe(false)

    const reader = event.sentStream!.getReader()
    expect(await nextText(reader)).toBe('event: delta\ndata: {"text":"Hel"}\n\n')

    open!.response.write('event: delta\ndata: {"text":"lo"}\n\n')
    expect(await nextText(reader)).toBe('event: delta\ndata: {"text":"lo"}\n\n')

    open!.response.write('event: done\ndata: {}\n\n')
    expect(await nextText(reader)).toBe('event: done\ndata: {}\n\n')
    reader.releaseLock()
  })

  it('answers with its own stream headers only, never an upstream header or the token', async () => {
    const event = makeEvent({
      method: HttpMethod.Post,
      params: { plugin: 'acme-sites', path: 'sites/7/chat' },
      body: {},
      cookies: await signedIn('jwt'),
    })

    await route.default(event)

    expect(event.responseHeaders).toEqual(STREAM_HEADERS)
    expect(JSON.stringify(event.responseHeaders)).not.toContain('jwt')
    expect(event.written).toEqual({})
  })

  it('ends the backend call when the browser goes away', async () => {
    const event = makeEvent({
      method: HttpMethod.Post,
      params: { plugin: 'acme-sites', path: 'sites/7/chat' },
      body: {},
      cookies: await signedIn('jwt'),
    })
    await route.default(event)
    expect(upstreamClosed).toBe(false)

    event.node.res.emit('close')

    await waitUntil(() => expect(upstreamClosed).toBe(true))
  })

  it('passes a refusal on with its code, sentence and Retry-After, and opens no stream', async () => {
    const event = makeEvent({
      method: HttpMethod.Post,
      params: { plugin: 'acme-sites', path: 'refuse' },
      body: {},
      cookies: await signedIn('jwt'),
    })

    const error = await refusal(() => route.default(event))

    expect(error).toMatchObject({ statusCode: 429, statusMessage: 'Too many requests.', data: { code: 'RATE_LIMITED' } })
    expect(event.responseHeaders['Retry-After']).toBe('30')
    expect(event.responseHeaders['Content-Type']).toBeUndefined()
    expect(event.sentStream).toBeUndefined()
  })

  it('passes the backend\'s 404 for an undeclared route on unchanged', async () => {
    const event = makeEvent({
      method: HttpMethod.Post,
      params: { plugin: 'acme-sites', path: 'missing' },
      body: {},
      cookies: await signedIn('jwt'),
    })

    const error = await refusal(() => route.default(event))

    expect(error).toMatchObject({ statusCode: 404, statusMessage: 'No such route.', data: { code: 'PLUGIN_ROUTE_NOT_FOUND' } })
    expect(event.sentStream).toBeUndefined()
  })

  it.each([
    ['../../admin', 'a climb out of the plugin'],
    ['%2e%2e/chat', 'an encoded climb'],
    ['sites//chat', 'an empty segment'],
  ])('refuses the path %j (%s) before the backend', async (path: string, _reason: string) => {
    const error = await refusal(async () => route.default(makeEvent({
      method: HttpMethod.Post,
      params: { plugin: 'acme-sites', path },
      body: {},
      cookies: await signedIn('jwt'),
    })))

    expect(error).toMatchObject({ statusCode: 400, data: { code: 'BAD_ROUTE_PARAM' } })
    expect(received).toBe(0)
  })

  it('refuses a body that is not an object before the backend', async () => {
    const error = await refusal(async () => route.default(makeEvent({
      method: HttpMethod.Post,
      params: { plugin: 'acme-sites', path: 'sites/7/chat' },
      body: 'hi',
      cookies: await signedIn('jwt'),
    })))

    expect(error).toMatchObject({ statusCode: 400, data: { code: 'BAD_BODY' } })
    expect(received).toBe(0)
  })

  it('refuses a success that is not an event stream with a coded 502 and no sentence, and pipes nothing', async () => {
    const event = makeEvent({
      method: HttpMethod.Post,
      params: { plugin: 'acme-sites', path: 'json' },
      body: {},
      cookies: await signedIn('jwt'),
    })

    const error = await refusal(() => route.default(event))

    expect(error).toMatchObject({ statusCode: 502, data: { code: 'PLUGIN_STREAM_INVALID' } })
    expect(error['statusMessage']).toBeUndefined()
    expect(event.sentStream).toBeUndefined()
    expect(event.responseHeaders['Content-Type']).toBeUndefined()
  })

  it('ends quietly, with no refusal, when the browser leaves before the backend answered', async () => {
    const event = makeEvent({
      method: HttpMethod.Post,
      params: { plugin: 'acme-sites', path: 'slow' },
      body: {},
      cookies: await signedIn('jwt'),
    })

    const answering = route.default(event)
    await waitUntil(() => expect(received).toBe(1))
    event.node.res.emit('close')

    await expect(answering).resolves.toBeNull()
    expect(event.sentStream).toBeUndefined()
    await waitUntil(() => expect(upstreamClosed).toBe(true))
  })
})
