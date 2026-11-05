/**
 * Source-scan contract for the Nitro BFF (`server/`).
 *
 * The BFF is a pass-through: it forwards the reader's language, reaches the C# API only through
 * `internalApiCall` (or `internalApiBinaryCall` for a file and `internalApiStream` for an event
 * stream, which share its headers, token refresh and error forwarding), and authors no sentence. These checks read the route sources, so a new
 * route that breaks the contract fails here instead of surfacing as an English message in a
 * Russian customer's browser.
 *
 * @module tests/server/contract.test
 */

import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The `server/` directory. */
const SERVER = resolve(import.meta.dirname, '../../server')

/** The BFF routes under `server/api/portal`. */
const PORTAL = join(SERVER, 'api', 'portal')

/**
 * Routes that talk to the SSO / the browser's OAuth redirect flow, not to the C# API, and are
 * therefore the only ones allowed to skip `internalApiCall`.
 */
const NOT_BACKEND_ROUTES = /^auth[\\/]sso[\\/]/

/**
 * Lists every `.ts` file under a directory.
 *
 * @param directory - Absolute directory.
 * @returns Absolute file paths.
 */
const walk = (directory: string): string[] => {
  return readdirSync(directory).flatMap((name) => {
    const path = join(directory, name)
    return statSync(path).isDirectory() ? walk(path) : path.endsWith('.ts') ? [path] : []
  })
}

/**
 * Reads a source file with its comments removed, so prose that mentions a forbidden pattern
 * does not count as using it.
 *
 * @param path - Absolute file path.
 * @returns The code only.
 */
const code = (path: string): string => {
  return readFileSync(path, 'utf8').replace(/\/\*[\s\S]*?\*\//g, '').replace(/^\s*\/\/.*$/gm, '')
}

describe('BFF contract', () => {
  const routes = walk(PORTAL)

  it('finds the routes it is meant to guard', () => {
    expect(routes.length).toBeGreaterThan(60)
  })

  it('reaches the C# API only through internalApiCall, internalApiBinaryCall or internalApiStream', () => {
    const offenders = routes
      .filter((path) => !NOT_BACKEND_ROUTES.test(relative(PORTAL, path)))
      .filter((path) => !/internalApi(BinaryCall|Call|Stream)\s*[<(]/.test(code(path)))
      .map((path) => relative(SERVER, path))

    expect(offenders).toEqual([])
  })

  it('never calls the backend with a bare $fetch outside the SSO routes', () => {
    const offenders = routes
      .filter((path) => !NOT_BACKEND_ROUTES.test(relative(PORTAL, path)))
      .filter((path) => /\$fetch\s*[<(]/.test(code(path)))
      .map((path) => relative(SERVER, path))

    expect(offenders).toEqual([])
  })

  it('throws no literal statusMessage sentence anywhere in server/', () => {
    const offenders = walk(SERVER)
      .filter((path) => /statusMessage\s*:\s*['"`]/.test(code(path)))
      .map((path) => relative(SERVER, path))

    expect(offenders).toEqual([])
  })

  it('has retired the hand-translated sentence table and the x-locale header', () => {
    const offenders = walk(SERVER)
      .filter((path) => /serverT|x-locale/i.test(code(path)))
      .map((path) => relative(SERVER, path))

    expect(offenders).toEqual([])
  })

  it('guards route parameters with requireRouteParam instead of hand-written sentences', () => {
    const offenders = routes
      .filter((path) => /getRouterParam\(/.test(code(path)))
      .map((path) => relative(SERVER, path))

    expect(offenders).toEqual([])
  })

  it('reaches the sign-on service only through signOnFetch, bar the code exchange of the callback', () => {
    // config.get.ts only names `ssoUrl` as the key of the JSON the header widget reads; it never calls the service.
    const allowed = new Set(['utils/signon-http.ts', 'api/portal/auth/sso/callback.get.ts', 'routes/api/portal/public/config.get.ts'])
    const offenders = walk(SERVER)
      .filter((path) => /\bssoUrl\b/.test(code(path)))
      .map((path) => relative(SERVER, path).replace(/\\/g, '/'))
      .filter((path) => !allowed.has(path))

    expect(offenders).toEqual([])
  })
})
