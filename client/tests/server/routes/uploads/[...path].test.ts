/**
 * Tests for `GET` and `HEAD /uploads/**`: it proxies the request for an uploaded file to the same path under
 * `/uploads` on the C# API, refuses every path that could leave that folder, forwards only an allow
 * list of request headers and returns only an allow list of response headers. A `HEAD` is asked of
 * the file server as a `HEAD` and answered with the same status and headers and no body; no other
 * method is answered.
 *
 * @module tests/server/routes/uploads/[...path].test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, startBackend } from '../../../helpers/bffHarness'
import type { RouteModule } from '../../../helpers/bffHarness'
import type { StubServer } from '../../../helpers/stubServer'
import { HttpMethod } from '~/types/httpmethod'

/** The stub playing the C# API. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

/**
 * Runs the route and reads the streamed body it returns.
 *
 * @param event - The fake request.
 * @returns The body text.
 */
const bodyOf = async (event: ReturnType<typeof makeEvent>): Promise<string> => {
  return new Response(await route.default(event) as ReadableStream).text()
}

beforeEach(async () => {
  backend = await startBackend()
  route = await import('~/server/routes/uploads/[...path]')
})

afterEach(async () => {
  await backend.close()
})

describe('GET and HEAD /uploads/**', () => {
  it('asks the file server for a HEAD, answers its status and headers and sends no body', async () => {
    backend.respondWith(() => ({ body: 'file-bytes', headers: { 'content-type': 'application/pdf', etag: '"v1"' } }))
    const event = makeEvent({ params: { path: 'docs/a.pdf' }, method: HttpMethod.Head })

    const body = await route.default(event)

    expect(backend.requests[0]?.method).toBe('HEAD')
    expect(backend.requests[0]?.url).toBe('/uploads/docs/a.pdf')
    expect(body).toBeUndefined()
    expect(event.noContentStatus).toBe(200)
    expect(event.status).toBe(200)
    expect(event.responseHeaders['content-type']).toBe('application/pdf')
    expect(event.responseHeaders['etag']).toBe('"v1"')
    expect(event.responseHeaders['X-Content-Type-Options']).toBe('nosniff')
  })

  it('answers a HEAD of a missing file with the 404 of the GET', async () => {
    backend.respondWith(() => ({ status: 404, body: 'missing' }))
    const event = makeEvent({ params: { path: 'docs/gone.pdf' }, method: HttpMethod.Head })

    await route.default(event)

    expect(event.status).toBe(404)
    expect(event.noContentStatus).toBe(404)
  })

  it.each([HttpMethod.Post, HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete, HttpMethod.Options])(
    'refuses %s with 405, without calling the backend',
    async (method) => {
      const error = await refusal(() => route.default(makeEvent({ params: { path: 'docs/a.pdf' }, method })))

      expect(error).toMatchObject({ statusCode: 405, data: { code: 'METHOD_NOT_ALLOWED' } })
      expect(backend.requests).toHaveLength(0)
    }
  )

  it('requests the same path under /uploads on the backend and returns its answer', async () => {
    backend.respondWith(() => ({ body: 'file-bytes', headers: { 'content-type': 'application/pdf' } }))
    const event = makeEvent({ params: { path: 'docs/a.pdf' } })

    const text = await bodyOf(event)

    expect(text).toBe('file-bytes')
    expect(backend.requests[0]?.method).toBe('GET')
    expect(backend.requests[0]?.url).toBe('/uploads/docs/a.pdf')
    expect(event.status).toBe(200)
    expect(event.responseHeaders['content-type']).toBe('application/pdf')
    expect(event.responseHeaders['X-Content-Type-Options']).toBe('nosniff')
  })

  it('decodes the path once and encodes each segment into the upstream address', async () => {
    await bodyOf(makeEvent({ params: { path: 'docs/my%20file%2Bv2.png' } }))

    expect(backend.requests[0]?.url).toBe('/uploads/docs/my%20file%2Bv2.png')
  })

  it.each([
    ['an encoded dot-dot segment', '%2e%2e/secret.png'],
    ['an encoded dot-dot followed by an encoded slash', '..%2fsecret.png'],
    ['an upper-case encoded dot-dot', '%2E%2E/secret.png'],
    ['an encoded slash after a segment', 'docs%2f..%2f..%2fsecret.png'],
    ['an encoded backslash', 'docs%5c..%5csecret.png'],
    ['an upper-case encoded backslash', 'docs%5Csecret.png'],
    ['a literal backslash', 'docs\\secret.png'],
    ['a double-encoded dot-dot', '%252e%252e/secret.png'],
    ['a double-encoded slash', '..%252fsecret.png'],
    ['a plain dot-dot segment', '../secret.png'],
    ['a dot segment', 'docs/./a.png'],
    ['an empty segment', 'docs//a.png'],
    ['a leading slash', '/docs/a.png'],
    ['a trailing slash', 'docs/'],
    ['an empty path', ''],
    ['an encoded NUL', 'docs/a.png%00.txt'],
    ['an encoded newline', 'docs/a%0d%0aSet-Cookie.png'],
    ['a malformed escape', 'docs/%E0%A4%A.png']
  ])('refuses %s with a coded 400, without calling the backend', async (_label, path) => {
    const error = await refusal(() => route.default(makeEvent({ params: { path } })))

    expect(error).toMatchObject({ statusCode: 400, data: { code: 'BAD_ROUTE_PARAM' } })
    expect(backend.requests).toHaveLength(0)
  })

  it('forwards no cookie, authorization or other client header to the file server', async () => {
    const event = makeEvent({
      params: { path: 'docs/a.pdf' },
      headers: {
        cookie: 'portal_session=secret',
        authorization: 'Bearer secret',
        'x-forwarded-for': '203.0.113.9',
        'x-language': 'ru',
        accept: 'image/*',
        range: 'bytes=0-9',
        'if-none-match': '"abc"'
      }
    })

    await bodyOf(event)

    const received = backend.requests[0]?.headers
    expect(received?.['cookie']).toBeUndefined()
    expect(received?.['authorization']).toBeUndefined()
    expect(received?.['x-forwarded-for']).toBeUndefined()
    expect(received?.['x-language']).toBeUndefined()
    expect(received?.['range']).toBe('bytes=0-9')
    expect(received?.['if-none-match']).toBe('"abc"')
    expect(received?.['accept']).toBe('image/*')
  })

  it('does not pass a Set-Cookie or any other unlisted response header back', async () => {
    backend.respondWith(() => ({
      body: 'x',
      headers: { 'set-cookie': 'session=stolen; Path=/', 'x-powered-by': 'backend', 'cache-control': 'public, max-age=60', etag: '"v1"' }
    }))
    const event = makeEvent({ params: { path: 'docs/a.pdf' } })

    await bodyOf(event)

    expect(Object.keys(event.responseHeaders).map(name => name.toLowerCase())).not.toContain('set-cookie')
    expect(Object.keys(event.responseHeaders).map(name => name.toLowerCase())).not.toContain('x-powered-by')
    expect(event.responseHeaders['cache-control']).toBe('public, max-age=60')
    expect(event.responseHeaders['etag']).toBe('"v1"')
  })

  it('passes the file server status through for a missing file', async () => {
    backend.respondWith(() => ({ status: 404, body: 'missing' }))
    const event = makeEvent({ params: { path: 'docs/gone.pdf' } })

    await bodyOf(event)

    expect(event.status).toBe(404)
  })
})
