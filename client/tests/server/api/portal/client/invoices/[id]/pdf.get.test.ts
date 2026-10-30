/**
 * Tests for `GET /api/portal/client/invoices/:id/pdf`: it calls the backend `GET /me/invoices/7/pdf`
 * with the session bearer and hands the PDF bytes and download headers back unchanged.
 *
 * @module tests/server/api/portal/client/invoices/[id]/pdf.get.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, signedIn, startBackend, storedSession } from '../../../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../../../helpers/bffHarness'
import type { StubServer } from '../../../../../../helpers/stubServer'

/** The stub playing the C# API. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

/** What the stub answers with: a PDF as the backend sends it. */
const pdfReply = () => ({
  body: '%PDF-1.4 test',
  headers: {
    'content-type': 'application/pdf',
    'content-disposition': 'attachment; filename=invoice-7.pdf',
    // Not the route's own fallback, so the assertion can only pass if the header was forwarded.
    'cache-control': 'private, no-store, max-age=0',
  },
})

beforeEach(async () => {
  backend = await startBackend()
  route = await import('~/server/api/portal/client/invoices/[id]/pdf.get')
})

afterEach(async () => {
  await backend.close()
})

describe('GET /api/portal/client/invoices/:id/pdf', () => {
  it('forwards to the backend with the bearer token', async () => {
    backend.respondWith(pdfReply)

    await route.default(makeEvent({ params: { id: '7' }, cookies: await signedIn('jwt') }))

    expect(backend.requests[0]?.method).toBe('GET')
    expect(backend.requests[0]?.url).toBe('/api/me/invoices/7/pdf')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
  })

  it('refuses an id that is not a number with 400, and never calls the backend', async () => {
    backend.respondWith(pdfReply)

    const error = await refusal(async () => route.default(makeEvent({ params: { id: '7/../../clients' }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 400, data: { code: 'BAD_ROUTE_PARAM' } })
    expect(backend.requests).toHaveLength(0)
  })

  it('returns the PDF bytes and download headers unchanged', async () => {
    backend.respondWith(pdfReply)
    const event = makeEvent({ params: { id: '7' }, cookies: await signedIn('jwt') })

    const result = await route.default(event)

    expect(Buffer.isBuffer(result)).toBe(true)
    expect((result as Buffer).toString('latin1')).toBe('%PDF-1.4 test')
    expect(event.responseHeaders).toMatchObject({
      'content-type': 'application/pdf',
      'content-disposition': 'attachment; filename=invoice-7.pdf',
      'cache-control': 'private, no-store, max-age=0',
    })
  })

  it('still forbids caching when the backend sends no cache-control', async () => {
    backend.respondWith(() => ({ body: '%PDF-1.4 test', headers: { 'content-type': 'application/pdf' } }))
    const event = makeEvent({ params: { id: '7' }, cookies: await signedIn('jwt') })

    await route.default(event)

    expect(event.responseHeaders['cache-control']).toBe('private, no-store')
  })

  it('passes a 404 refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 404, body: { type: 'INVOICE_NOT_FOUND', detail: 'Invoice not found.' } }))

    const error = await refusal(async () => route.default(makeEvent({ params: { id: '7' }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 404, statusMessage: 'Invoice not found.', data: { code: 'INVOICE_NOT_FOUND' } })
  })

  it('passes a busy 503 through with its Retry-After, so the browser knows when to ask again', async () => {
    backend.respondWith(() => ({
      status: 503,
      headers: { 'Retry-After': '30' },
      body: { type: 'INVOICE_PDF_RENDER_BUSY', detail: 'Too many invoice PDFs are being prepared right now.' },
    }))
    const event = makeEvent({ params: { id: '7' }, cookies: await signedIn('jwt') })

    const error = await refusal(async () => route.default(event))

    expect(error).toMatchObject({
      statusCode: 503,
      statusMessage: 'Too many invoice PDFs are being prepared right now.',
      data: { code: 'INVOICE_PDF_RENDER_BUSY' },
    })
    expect(event.responseHeaders['Retry-After']).toBe('30')
  })

  it('refreshes the session once on a 401 and retries', async () => {
    backend.respondWith((request) => {
      if (request.url === '/connect/token') {
        return { body: { access_token: 'fresh', expires_in: 900 } }
      }
      return request.headers['authorization'] === 'Bearer fresh' ? pdfReply() : { status: 401, body: {} }
    })
    const cookies = await signedIn('old', { refreshToken: 'r' })
    const event = makeEvent({ params: { id: '7' }, cookies })

    const result = await route.default(event)

    expect((result as Buffer).toString('latin1')).toBe('%PDF-1.4 test')
    const backendAuth = backend.requests.filter((r) => r.url.startsWith('/api/')).map((r) => r.headers['authorization'])
    expect(backendAuth).toEqual(['Bearer old', 'Bearer fresh'])
    expect(await storedSession(cookies)).toMatchObject({ accessToken: 'fresh', refreshToken: 'r' })
  })
})
