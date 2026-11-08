/**
 * Proves, on the real route table, that the storefront file routes answer a `HEAD`.
 *
 * Nitro turns a route file name into a route and a method (`scanServerRoutes`), registers each on an
 * h3 router (`router.use(route, handler, method)`) and lets the page renderer take whatever no route
 * matched. A file named `*.get.ts` is registered for `GET` alone, and h3 does not fall back to it for
 * `HEAD`, so such a `HEAD` reached the renderer and answered a status and headers that described no
 * file. The unit tests of the routes cannot see that: they call the handler directly, and a handler
 * called directly answers a `HEAD` however its file is named. This test scans the real
 * `server/routes` folder with Nitro's own scanner, registers the result the way Nitro does with the
 * renderer as the fallback, and sends real requests over a socket.
 *
 * @module tests/server/nitroRouter.test
 */

import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { createServer } from 'node:http'
import type { Server } from 'node:http'
import type { AddressInfo } from 'node:net'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import * as h3 from 'h3'
import type { EventHandler } from 'h3'
import { scanServerRoutes } from 'nitropack/core'
import type { Nitro } from 'nitropack'
import { ofetch } from 'ofetch'
import { afterAll, beforeAll, describe, expect, it, vi } from 'vitest'
import { startStubServer } from '../helpers/stubServer'
import type { StubServer } from '../helpers/stubServer'
import { HttpMethod } from '~/types/httpmethod'

/** The `server/` directory whose `routes/` folder Nitro scans. */
const SERVER = resolve(import.meta.dirname, '../../server')

/** The header the stand-in for the page renderer sets, so a test can tell who answered. */
const SERVED_BY = 'x-served-by'

/** The value of {@link SERVED_BY} when the renderer, not a route, answered. */
const RENDERER = 'renderer'

/** One route as `scanServerRoutes` reports it. */
type ScannedRoute = Awaited<ReturnType<typeof scanServerRoutes>>[number]

/** The runtime config the routes read; filled in once the stubs run. */
const runtimeConfig: Record<string, unknown> = { public: {} }

// Nitro injects these names into every route file; here they are the real h3 functions, so a route
// is exercised by the framework and not by a stand-in for it.
for (const [name, value] of Object.entries(h3)) {
  vi.stubGlobal(name, value)
}
vi.stubGlobal('$fetch', ofetch)
vi.stubGlobal('useRuntimeConfig', () => runtimeConfig)
const { getApiUrl } = await import('~/server/utils/api')
vi.stubGlobal('getApiUrl', getApiUrl)

/**
 * The part of Nitro's configuration the scanner reads: the folders to scan and the paths to ignore.
 *
 * @param scanDir - The folder that holds `routes/`.
 * @returns A Nitro handle the scanner accepts.
 */
const nitroFor = (scanDir: string): Nitro => {
  return { options: { scanDirs: [scanDir], ignore: [] } } as unknown as Nitro
}

/**
 * Builds an h3 server the way Nitro wires its routes, with the renderer as the fallback.
 *
 * @param scanned - The routes `scanServerRoutes` found.
 * @param handlerFor - Supplies the handler of a scanned route.
 * @returns The listening server.
 */
const serve = async (scanned: ScannedRoute[], handlerFor: (route: ScannedRoute) => EventHandler): Promise<Server> => {
  const router = h3.createRouter({ preemptive: true })
  for (const route of scanned) {
    router.use(route.route, handlerFor(route), route.method)
  }
  router.use('/**', h3.defineEventHandler((event) => {
    h3.setResponseHeader(event, SERVED_BY, RENDERER)
    h3.setResponseHeader(event, 'content-type', 'text/html')
    return '<html>page</html>'
  }))
  const app = h3.createApp()
  app.use(router.handler)

  const server = createServer(h3.toNodeListener(app))
  await new Promise<void>((done) => {
    server.listen(0, '127.0.0.1', done)
  })
  return server
}

/**
 * The address a listening server answers on.
 *
 * @param server - The listening server.
 * @returns Its origin.
 */
const originOf = (server: Server): string => `http://127.0.0.1:${(server.address() as AddressInfo).port}`

/**
 * Stops a server, ending any connection a test left open.
 *
 * @param server - The server to stop.
 * @returns Resolves once the server has closed.
 */
const stop = (server: Server): Promise<void> => {
  return new Promise((done, fail) => {
    server.close(error => (error ? fail(error) : done()))
    server.closeAllConnections()
  })
}

/** The file routes and the content type each one answers with, for a request the stub can satisfy. */
const FILE_ROUTES = [
  ['/robots.txt', 'text/plain'],
  ['/site.webmanifest', 'application/manifest+json'],
  ['/uploads/docs/a.pdf', 'application/pdf'],
  ['/uploads/branding/abc/v1/icon-192.png', 'image/png'],
  ['/widget/header.js', 'text/javascript']
] as const

describe('the real Nitro route table', () => {
  let upstream: StubServer
  let server: Server
  let origin: string

  beforeAll(async () => {
    upstream = await startStubServer((request) => {
      return request.url.startsWith('/widget/')
        ? { body: '(function(){})()' }
        : { body: 'file-bytes', headers: { 'content-type': 'application/pdf', 'content-length': '10' } }
    })
    runtimeConfig['apiUrl'] = upstream.baseUrl
    runtimeConfig['public'] = { authMode: 'sso', mainUrl: upstream.baseUrl, baseUrl: 'https://shop.example' }

    const scanned = await scanServerRoutes(nitroFor(SERVER), 'routes')
    // Lazy, as Nitro loads them: a route file is read when its path is first asked for.
    server = await serve(scanned, route => h3.lazyEventHandler(async () => (await import(/* @vite-ignore */ route.handler)).default))
    origin = originOf(server)
  })

  afterAll(async () => {
    await stop(server)
    await upstream.close()
  })

  it.each(FILE_ROUTES)('answers a HEAD of %s from the route itself, as the GET does', async (path, contentType) => {
    const get = await fetch(`${origin}${path}`)
    const head = await fetch(`${origin}${path}`, { method: HttpMethod.Head })

    expect(get.status).toBe(200)
    expect(head.status).toBe(200)
    expect(head.headers.get(SERVED_BY)).toBeNull()
    expect(head.headers.get('content-type')).toContain(contentType)
    expect(head.headers.get('content-type')).toBe(get.headers.get('content-type'))
    expect(await head.text()).toBe('')
  })

  it.each(['/uploads/docs/a.pdf', '/uploads/branding/abc/v1/icon-192.png'])(
    'sends the size the file server gave for %s on a HEAD, without fetching the file',
    async (path) => {
      upstream.requests.length = 0

      const head = await fetch(`${origin}${path}`, { method: HttpMethod.Head })

      expect(head.headers.get('content-length')).toBe('10')
      expect(upstream.requests.map(request => request.method)).toEqual([HttpMethod.Head])
    }
  )

  it.each([HttpMethod.Post, HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete, HttpMethod.Options])(
    'answers %s on a file route with 405 and the allowed methods, not with the page',
    async (method) => {
      for (const [path] of FILE_ROUTES) {
        const response = await fetch(`${origin}${path}`, { method })

        expect(response.status).toBe(405)
        expect(response.headers.get('allow')).toBe('GET, HEAD')
        expect(response.headers.get(SERVED_BY)).toBeNull()
      }
    }
  )
})

describe('the same table with a file named *.get.ts', () => {
  let folder: string
  let server: Server

  beforeAll(async () => {
    // The old shape of these routes, planted: the scanner gives it the method `get`, and the test
    // shows that the router then hands a HEAD to the renderer. It is what makes the test above able to fail.
    folder = mkdtempSync(join(tmpdir(), 'nitro-routes-'))
    mkdirSync(join(folder, 'routes'))
    writeFileSync(join(folder, 'routes', 'probe.get.ts'), '')
    const scanned = await scanServerRoutes(nitroFor(folder), 'routes')
    expect(scanned).toMatchObject([{ route: '/probe', method: 'get' }])

    server = await serve(scanned, () => h3.defineEventHandler(() => 'file'))
  })

  afterAll(async () => {
    await stop(server)
    rmSync(folder, { recursive: true, force: true })
  })

  it('answers a GET from the route and a HEAD from the renderer', async () => {
    const origin = originOf(server)

    const get = await fetch(`${origin}/probe`)
    const head = await fetch(`${origin}/probe`, { method: HttpMethod.Head })

    expect(get.headers.get(SERVED_BY)).toBeNull()
    expect(head.headers.get(SERVED_BY)).toBe(RENDERER)
  })
})
