/**
 * Helpers shared by the hostpanel client's e2e specs.
 *
 * Nothing here selects by anything but `data-testid` (Playwright's `getByTestId`); a helper that
 * needed a class name or a visible string to find an element would be a missing test id in the
 * component instead.
 *
 * @module e2e/support/helpers
 */
import type { APIRequestContext, BrowserContext, Page } from '@playwright/test'
import { CLIENT_CREDENTIALS, STUB_URL } from './fixtures.mjs'
import type { HttpMethod } from '../../types/httpmethod'

/** One request the stub backend has served, as `GET /__e2e/requests` reports it. */
export interface StubRequest {
  /** HTTP method. */
  method: string
  /** Path without the query string. */
  path: string
  /** The raw query string including the leading `?`, or empty when there was none. */
  query: string
  /** Parsed JSON body, or `null` when there was none. */
  body: Record<string, unknown> | null
  /** The `X-Language` header the request carried; empty when it carried none. */
  language: string
}

/** An answer to script for one stub route (see `POST /__e2e/script` in `stub-backend.mjs`). */
export interface ScriptedAnswer {
  /** HTTP method of the route. */
  method: HttpMethod
  /** Path of the route on the stub, e.g. `/api/clients/me`. */
  path: string
  /** Status the stub answers. */
  status: number
  /** Error code carried in the problem body's `type`. */
  code?: string
  /** The sentence carried in the problem body's `detail`. */
  detail?: string
  /** Field messages of a 400 `VALIDATION_ERROR`. */
  errors?: Record<string, string[]>
}

/**
 * Narrows a fixture entry that the type system only knows as "maybe there" (an array element under
 * `noUncheckedIndexedAccess`) and fails the spec with a readable message when it is missing.
 *
 * @param value The entry, possibly undefined.
 * @param what What the entry is, for the failure message.
 * @returns The entry itself.
 */
export const required = <T>(value: T | undefined, what: string): T => {
  if (value === undefined) {
    throw new Error(`The fixture has no ${what}.`)
  }
  return value
}

/**
 * Waits until the page is interactive: Vue has mounted on the server-rendered markup, Nuxt has
 * finished hydrating it, and the initial requests have settled.
 *
 * Server-rendered pages show their form before hydration finishes, and a value typed in that
 * window is wiped when the component takes over, which fails a spec as a "field is empty"
 * assertion far from the cause. The signal is Nuxt's own: the root element holds the Vue app
 * (`__vue_app__`) and `useNuxtApp().isHydrating` has turned false. The `data-v-app` attribute is
 * not used: Vue sets it on a client-side mount only, so a hydrated page never carries it.
 *
 * @param page The page that has just navigated.
 * @returns Resolves when the page is ready to be driven.
 */
export const waitForHydration = async (page: Page): Promise<void> => {
  await page.waitForFunction(() => {
    /** The element Vue mounts on; it holds the app once mounted. */
    const root = document.getElementById('__nuxt') as (HTMLElement & { __vue_app__?: unknown }) | null
    /** Nuxt's own accessor, which it exposes on `window` in the browser. */
    const nuxt = (window as unknown as { useNuxtApp?: () => { isHydrating: boolean } }).useNuxtApp
    return Boolean(root?.__vue_app__) && nuxt?.().isHydrating === false
  })
  await page.waitForLoadState('networkidle')
}

/**
 * Gives the browser context a signed-in session.
 *
 * The session lives on the server: the BFF keeps the tokens and gives the browser only an opaque
 * `portal_session` cookie and the readable `authed` flag. So the context signs in through the BFF's
 * own sign-in route with the stub customer's credentials (the same call the form makes), and the
 * cookies it answers land in the context, which keeps every spec but the sign-in one from
 * repeating the form.
 *
 * @param context The browser context to sign in.
 * @param baseURL The app's base URL.
 * @returns Resolves once the session cookies are in the context.
 */
export const signIn = async (context: BrowserContext, baseURL: string): Promise<void> => {
  const response = await context.request.post(`${baseURL}/api/portal/auth/login`, {
    // The BFF refuses a state-changing call without it (server/middleware/csrf.ts).
    headers: { 'X-Requested-With': 'XMLHttpRequest' },
    data: CLIENT_CREDENTIALS,
  })
  if (!response.ok()) {
    throw new Error(`Signing in for the spec failed with ${response.status()}.`)
  }
}

/**
 * Clears the stub backend's request log, so a spec asserts only on its own traffic.
 *
 * @param request Playwright's API request context.
 * @returns Resolves once the log is empty.
 */
export const resetStub = async (request: APIRequestContext): Promise<void> => {
  await request.post(`${STUB_URL}/__e2e/reset`)
}

/**
 * Reads every request the stub backend has served since the last reset.
 *
 * @param request Playwright's API request context.
 * @returns The requests, oldest first.
 */
export const stubRequests = async (request: APIRequestContext): Promise<StubRequest[]> => {
  const response = await request.get(`${STUB_URL}/__e2e/requests`)
  return (await response.json()) as StubRequest[]
}

/**
 * Sets whether the stub reports the signed-in address as confirmed, until the next {@link resetStub}.
 *
 * @param request Playwright's API request context.
 * @param verified The answer `GET /api/auth/email-verified` should give.
 * @returns Resolves once the stub has accepted the state.
 */
export const setEmailVerified = async (request: APIRequestContext, verified: boolean): Promise<void> => {
  await request.post(`${STUB_URL}/__e2e/email-verified`, { data: { verified } })
}

/**
 * Scripts one stub route to answer a refusal until the next {@link resetStub}.
 *
 * @param request Playwright's API request context.
 * @param answer The route and the refusal it should answer.
 * @returns Resolves once the stub has accepted the script.
 */
export const scriptStub = async (request: APIRequestContext, answer: ScriptedAnswer): Promise<void> => {
  await request.post(`${STUB_URL}/__e2e/script`, { data: answer })
}

/**
 * Overrides one public portal setting on the stub until the next {@link resetStub}.
 *
 * @param request Playwright's API request context.
 * @param key The setting key, e.g. `portal.template`.
 * @param value The value the storefront should read.
 * @returns Resolves once the stub has accepted the override.
 */
export const setPortalSetting = async (request: APIRequestContext, key: string, value: string): Promise<void> => {
  await request.post(`${STUB_URL}/__e2e/settings`, { data: { key, value } })
}

/**
 * Overrides fields of the signed-in person's account until the next {@link resetStub}.
 *
 * @param request Playwright's API request context.
 * @param fields The `GET /api/auth/me` fields to change, e.g. `{ clientPermissions: 0 }`.
 * @returns Resolves once the stub has accepted the override.
 */
export const setAccount = async (request: APIRequestContext, fields: Record<string, unknown>): Promise<void> => {
  await request.post(`${STUB_URL}/__e2e/account`, { data: fields })
}

/**
 * Switches the stub's demo mode on until the next {@link resetStub}; it is off by default.
 *
 * @param request Playwright's API request context.
 * @returns Resolves once the stub reports a demo.
 */
export const enableDemoMode = async (request: APIRequestContext): Promise<void> => {
  await request.post(`${STUB_URL}/__e2e/demo`, { data: { enabled: true } })
}

/**
 * Switches the stub's home-page slides on until the next {@link resetStub}; the feed is empty by default.
 *
 * @param request Playwright's API request context.
 * @returns Resolves once the stub serves the slides.
 */
export const enableSlides = async (request: APIRequestContext): Promise<void> => {
  await request.post(`${STUB_URL}/__e2e/slides`, { data: { enabled: true } })
}
