/**
 * Tests for the navigation guard: the session state is asked of the server once, signed in or
 * signed out, an anonymous visitor is sent to the sign-in page, a signed-in one is kept off it, and the first-run screen is
 * reachable only while setup is outstanding on a deployment that owns its accounts.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { RouteLocationNormalized } from 'vue-router'
import { authMiddleware } from '@/middleware/auth'
import { pinia } from '@/pinia'
import { useAuthStore } from '@/stores/auth'
import { json, problem } from '../helpers/fetch'
import { stubRoutes } from '../helpers/routes'

/**
 * Builds the minimal route object the guard reads.
 *
 * @param meta - The route's `meta`.
 * @param fullPath - The route's full path.
 * @param query - The route's parsed query.
 * @returns A route cast to the router type.
 */
const route = (
  meta: Record<string, unknown>,
  fullPath = '/',
  query: Record<string, unknown> = {},
): RouteLocationNormalized => ({ meta, fullPath, query }) as unknown as RouteLocationNormalized

/**
 * Stubs the three session endpoints the guard triggers.
 *
 * @param options - Deployment mode, whether setup is outstanding, and whether a session exists.
 * @returns The fetch mock.
 */
const stubSession = (options: { mode: 'local' | 'sso'; setup: boolean; signedIn: boolean; roles?: string[] }) =>
  stubRoutes({
    'GET /api/auth/mode': () => json({ mode: options.mode }),
    'GET /api/auth/setup-required': () => json({ required: options.setup }),
    'GET /api/auth/me': () =>
      options.signedIn
        ? json({ email: 'a@b.c', firstName: 'Ada', lastName: 'Lovelace', roles: options.roles ?? ['Admin'], emailVerified: true })
        : problem('Unauthorized', 401),
  })

beforeEach(() => {
  // The guard uses the shared Pinia; start every test from a signed-out, unloaded store.
  const auth = useAuthStore(pinia)
  auth.mode = null
  auth.user = null
  auth.sessionChecked = false
  auth.setupRequired = false
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('authMiddleware', () => {
  it('sends an anonymous visitor of a protected route to /login', async () => {
    stubSession({ mode: 'local', setup: false, signedIn: false })

    expect(await authMiddleware(route({ requiresAuth: true }, '/dashboard'))).toBe('/login?redirect=%2Fdashboard')
  })

  it('lets a signed-in user into a protected route', async () => {
    stubSession({ mode: 'local', setup: false, signedIn: true })

    expect(await authMiddleware(route({ requiresAuth: true }))).toBe(true)
  })

  it('keeps a signed-in customer without the Admin role out of a protected route', async () => {
    stubSession({ mode: 'sso', setup: false, signedIn: true, roles: ['Client'] })

    expect(await authMiddleware(route({ requiresAuth: true }, '/dashboard'))).toBe('/forbidden')
  })

  it('leaves a signed-in customer without the Admin role on the sign-in page', async () => {
    stubSession({ mode: 'sso', setup: false, signedIn: true, roles: [] })

    expect(await authMiddleware(route({ public: true }))).toBe(true)
  })

  it('passes the requested full path to /login as the redirect query', async () => {
    stubSession({ mode: 'local', setup: false, signedIn: false })

    expect(await authMiddleware(route({ requiresAuth: true }, '/clients/1/summary?tab=x')))
      .toBe('/login?redirect=%2Fclients%2F1%2Fsummary%3Ftab%3Dx')
  })

  it('sends a signed-in user on a public page to the safe redirect target', async () => {
    stubSession({ mode: 'local', setup: false, signedIn: true })

    expect(await authMiddleware(route({ public: true }, '/login', { redirect: '/clients/1/summary' })))
      .toBe('/clients/1/summary')
  })

  it.each(['//evil.example', 'https://evil.example', '/login', ['/a', '/b']])(
    'ignores the unsafe redirect %j and lands on the dashboard',
    async (redirect) => {
      stubSession({ mode: 'local', setup: false, signedIn: true })

      expect(await authMiddleware(route({ public: true }, '/login', { redirect }))).toBe('/dashboard')
    },
  )

  it('keeps a signed-in user off a public page', async () => {
    stubSession({ mode: 'local', setup: false, signedIn: true })

    expect(await authMiddleware(route({ public: true }))).toBe('/dashboard')
  })

  it('lets an anonymous visitor open a public page', async () => {
    stubSession({ mode: 'local', setup: false, signedIn: false })

    expect(await authMiddleware(route({ public: true }))).toBe(true)
  })

  it('opens the first-run screen only while setup is outstanding on a local deployment', async () => {
    stubSession({ mode: 'local', setup: true, signedIn: false })
    expect(await authMiddleware(route({ setup: true }))).toBe(true)

    stubSession({ mode: 'local', setup: false, signedIn: false })
    const auth = useAuthStore(pinia)
    auth.mode = null
    auth.setupRequired = false
    expect(await authMiddleware(route({ setup: true }))).toBe('/login')
  })

  it('refuses the first-run screen under SSO', async () => {
    stubSession({ mode: 'sso', setup: true, signedIn: false })

    expect(await authMiddleware(route({ setup: true }))).toBe('/login')
  })

  it('asks the server for the mode only once per page load', async () => {
    const mock = stubSession({ mode: 'local', setup: false, signedIn: true })

    await authMiddleware(route({ requiresAuth: true }))
    await authMiddleware(route({ requiresAuth: true }))

    const modeCalls = mock.mock.calls.filter(([url]) => String(url).endsWith('/api/auth/mode'))
    expect(modeCalls).toHaveLength(1)
  })

  it('asks for the mode and the session together, not one after the other', async () => {
    const calls: string[] = []
    const release: Array<() => void> = []
    /**
     * The answer each endpoint gives once it is released.
     *
     * @param url - The requested URL.
     * @returns The response.
     */
    const answerFor = (url: string): Response => {
      if (url.endsWith('/api/auth/mode')) {
        return json({ mode: 'local' })
      }
      if (url.endsWith('/api/auth/setup-required')) {
        return json({ required: false })
      }
      return json({ email: 'a@b.c', firstName: 'Ada', lastName: 'Lovelace', roles: ['Admin'], emailVerified: true })
    }
    // Nothing is answered until all calls are on the wire, so a guard that waited for one answer
    // before asking the next would never get past two calls.
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL) => {
      const url = String(input)
      calls.push(url)
      return new Promise<Response>((resolve) => {
        release.push(() => resolve(answerFor(url)))
      })
    }))

    const navigation = authMiddleware(route({ requiresAuth: true }))
    await vi.waitFor(() => expect(calls).toHaveLength(3))
    release.forEach((answer) => answer())

    expect(await navigation).toBe(true)
  })

  it('asks a signed-out visitor for the session only once per page load', async () => {
    const mock = stubSession({ mode: 'local', setup: false, signedIn: false })

    await authMiddleware(route({ public: true }, '/login'))
    await authMiddleware(route({ requiresAuth: true }, '/dashboard'))
    await authMiddleware(route({ public: true }, '/login'))

    const meCalls = mock.mock.calls.filter(([url]) => String(url).endsWith('/api/auth/me'))
    expect(meCalls).toHaveLength(1)
  })

  it('makes no request at all once the session state is known', async () => {
    const mock = stubSession({ mode: 'local', setup: false, signedIn: true })
    await authMiddleware(route({ requiresAuth: true }))
    mock.mockClear()

    await authMiddleware(route({ requiresAuth: true }))

    expect(mock).not.toHaveBeenCalled()
  })
})
