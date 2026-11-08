/**
 * Tests for `GET` and `HEAD /robots.txt`: the private areas stay closed, the sitemap line carries the
 * operator's own origin, a `HEAD` is answered as the `GET` is, and no other method is.
 *
 * @module tests/server/routes/robots.txt.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, setRuntimeConfig, startBackend } from '../../helpers/bffHarness'
import type { RouteModule } from '../../helpers/bffHarness'
import type { StubServer } from '../../helpers/stubServer'
import { HttpMethod } from '~/types/httpmethod'

/** The stub the harness needs for its runtime config. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

beforeEach(async () => {
  backend = await startBackend()
  route = await import('~/server/routes/robots.txt')
})

afterEach(async () => {
  await backend.close()
})

describe('GET and HEAD /robots.txt', () => {
  it('answers a HEAD with the status and headers of the GET', async () => {
    setRuntimeConfig({ public: { baseUrl: 'https://shop.example' } })
    const get = makeEvent()
    const head = makeEvent({ method: HttpMethod.Head })

    await route.default(get)
    await route.default(head)

    expect(head.responseHeaders).toEqual(get.responseHeaders)
    expect(head.responseHeaders['Content-Type']).toBe('text/plain; charset=utf-8')
  })

  it.each([HttpMethod.Post, HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete, HttpMethod.Options])(
    'refuses %s with 405 and names the methods it answers',
    async (method) => {
      const event = makeEvent({ method })

      const error = await refusal(() => route.default(event))

      expect(error).toMatchObject({ statusCode: 405, data: { code: 'METHOD_NOT_ALLOWED' } })
      expect(event.responseHeaders['Allow']).toBe('GET, HEAD')
    }
  )

  it('names the sitemap on the configured public address, without a trailing slash', async () => {
    setRuntimeConfig({ public: { baseUrl: 'https://shop.example/' } })
    const event = makeEvent()

    const body = await route.default(event) as string

    expect(body).toContain('Sitemap: https://shop.example/sitemap.xml\n')
    expect(event.responseHeaders['Content-Type']).toBe('text/plain; charset=utf-8')
  })

  it('falls back to the origin the request arrived on', async () => {
    setRuntimeConfig({ public: {} })

    const body = await route.default(makeEvent({ headers: { host: 'portal.test' } })) as string

    expect(body).toContain('Sitemap: http://portal.test/sitemap.xml')
  })

  it('disallows the private areas under every language prefix and allows the rest', async () => {
    setRuntimeConfig({ public: { baseUrl: 'https://shop.example' } })

    const body = await route.default(makeEvent()) as string

    for (const path of ['/client/', '/cart/', '/checkout/', '/order/', '/ru/client/', '/hy/order/']) {
      expect(body).toContain(`Disallow: ${path}\n`)
    }
    expect(body).toContain('Allow: /\n')
    expect(body).toContain('Allow: /*.css$')
    expect(body.startsWith('User-agent: *\n')).toBe(true)
  })
})
