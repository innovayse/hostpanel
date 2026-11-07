/**
 * Tests for the plugin page `frame-src` middleware: the API's origins on a document of a plugin page, and `'none'` for
 * every doubt; other addresses are left alone and a failure never throws.
 *
 * @module tests/server/middleware/plugin-frame-csp.test
 */

import { afterEach, describe, expect, it } from 'vitest'
import { makeEvent, signedIn, startBackend } from '../../helpers/bffHarness'
import type { StubReply, StubRequest, StubServer } from '../../helpers/stubServer'

/** The stub API. */
let backend: StubServer | undefined

/** The headers a browser sends when it opens a page. */
const navigation = { 'user-agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36', 'sec-fetch-dest': 'document', accept: 'text/html' }

/**
 * Runs the middleware against a stub API.
 *
 * @param path - The requested address.
 * @param reply - The API's answer.
 * @param headers - The request headers.
 * @returns The event and the requests the API received.
 */
const run = async (path: string, reply: StubReply, headers: Record<string, string> = navigation) => {
  const requests: StubRequest[] = []
  backend = await startBackend((request) => {
    requests.push(request)
    return reply
  })
  const { default: middleware } = await import('~/server/middleware/plugin-frame-csp')
  const event = makeEvent({ path, headers, cookies: await signedIn('token') })
  await (middleware as unknown as (e: typeof event) => Promise<void>)(event)
  return { event, requests }
}

afterEach(async () => {
  await backend?.close()
  backend = undefined
})

describe('plugin-frame-csp middleware', () => {
  it('writes the origins the API answered, reading the page by its decoded address', async () => {
    const { event, requests } = await run('/client/apps/acme-sites/services/7?x=1', { body: { frameOrigins: ['https://a.example', 'https://www.a.example'] } })

    expect(event.responseHeaders['Content-Security-Policy']).toBe('frame-src https://a.example https://www.a.example')
    expect(requests[0]?.url).toBe('/api/client/plugins/ui/acme-sites/pages/by-path?path=%2Fservices%2F7')
  })


  it.each(['ru', 'hy'])('narrows a page under the %s language prefix too, reading the same page', async (language) => {
    const { event, requests } = await run(`/${language}/client/apps/acme-sites/services/7`, { body: { frameOrigins: ['https://a.example'] } })

    expect(event.responseHeaders['Content-Security-Policy']).toBe('frame-src https://a.example')
    expect(requests[0]?.url).toBe('/api/client/plugins/ui/acme-sites/pages/by-path?path=%2Fservices%2F7')
  })

  it('marks a document that received origins, so the preview block can tell it from a closed one', async () => {
    const { event } = await run('/client/apps/acme-sites/services/7', { body: { frameOrigins: ['https://a.example'] } })

    expect(event.responseHeaders['Server-Timing']).toBe('plugin-frames')
  })

  it('leaves the mark off a closed document, such as one served to a signed-out visitor', async () => {
    const { event } = await run('/client/apps/acme-sites/services/7', { status: 401, body: {} })

    expect(event.responseHeaders['Content-Security-Policy']).toBe("frame-src 'none'")
    expect(event.responseHeaders['Server-Timing']).toBeUndefined()
  })

  it('reads the root page for the plugin address itself', async () => {
    const { requests } = await run('/client/apps/acme-sites', { body: { frameOrigins: [] } })

    expect(requests[0]?.url).toBe('/api/client/plugins/ui/acme-sites/pages/by-path?path=%2F')
  })

  it.each([
    ['an empty list', { body: { frameOrigins: [] } }],
    ['a refusal', { status: 404, body: { type: 'PLUGIN_PAGE_NOT_FOUND', title: 'x', status: 404, detail: 'x' } }],
    ['a failure', { status: 500, body: {} }],
    ['an origin that could break the header', { body: { frameOrigins: ['https://a.example; script-src *'] } }],
    ['an upper-case origin', { body: { frameOrigins: ['https://A.example'] } }],
    ['a list missing from the answer', { body: {} }],
  ])('frames nothing on %s', async (_name, reply) => {
    const { event } = await run('/client/apps/acme-sites/sites', reply)

    expect(event.responseHeaders['Content-Security-Policy']).toBe("frame-src 'none'")
  })

  it.each(['/client/apps/UI/x', '/client/apps/ui/x', '/client/apps/acme/%2e%2e/x', '/client/apps/acme/%E0%A4%A'])('frames nothing and asks nothing for the malformed address %s', async (path) => {
    const { event, requests } = await run(path, { body: { frameOrigins: ['https://a.example'] } })

    expect(event.responseHeaders['Content-Security-Policy']).toBe("frame-src 'none'")
    expect(requests).toHaveLength(0)
  })

  it('closes a request that is no navigation without asking the API', async () => {
    const { event, requests } = await run('/client/apps/acme-sites/sites', { body: { frameOrigins: ['https://a.example'] } }, { 'sec-fetch-dest': 'empty', 'user-agent': navigation['user-agent'] })

    expect(event.responseHeaders['Content-Security-Policy']).toBe("frame-src 'none'")
    expect(requests).toHaveLength(0)
  })

  it('leaves every other address alone', async () => {
    const { event, requests } = await run('/client/services/7', { body: {} })

    expect(event.responseHeaders['Content-Security-Policy']).toBeUndefined()
    expect(requests).toHaveLength(0)
  })
})
