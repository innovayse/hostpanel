/**
 * Playwright configuration for the hostpanel client's end-to-end suite.
 *
 * The suite drives a real browser against the real Nuxt server (Nitro, with its BFF routes under
 * `server/api/portal/**`). Only the outermost hop is replaced: `e2e/support/stub-backend.mjs`
 * stands in for the C# API. A stub *server* rather than `page.route` because the storefront is
 * server-rendered: plans, settings and the TLD table are fetched by Nitro, which a browser-side
 * route handler never sees. See `e2e/README.md` for how to run it.
 *
 * Playwright starts both processes itself, so a run is one command (`yarn e2e`):
 * 1. the stub backend, on `E2E_STUB_PORT` (default 5999);
 * 2. the client, on `E2E_PORT` (default 3100), in local auth mode and pointed at the stub.
 *
 * The client is served from the production build by default (`nuxt build` first: what the
 * deployment ships, and where SSR crashes show up). `E2E_DEV=1` serves `nuxt dev` instead, which
 * needs no build but is slower on first render.
 *
 * @see https://playwright.dev/docs/test-configuration
 * @see e2e/README.md
 */
import { defineConfig, devices } from '@playwright/test'
import { CookieConsent } from './types/cookieconsent'

/** Port the client listens on. */
const PORT = Number(process.env.E2E_PORT || 3100)

/** Port the stub backend listens on; must match `e2e/support/fixtures.mjs`. */
const STUB_PORT = Number(process.env.E2E_STUB_PORT || 5999)

/** Where the app answers, as the browser sees it. */
const BASE_URL = `http://127.0.0.1:${PORT}`

/**
 * Name of the cookie the consent banner writes (`COOKIE_CONSENT_NAME` in
 * `composables/useCookieConsent.ts`, which cannot be imported here: it reaches for Nuxt aliases).
 */
const CONSENT_COOKIE = 'cookie-consent'

/** Whether to serve `nuxt dev` instead of the production build. */
const useDevServer = process.env.E2E_DEV === '1'

export default defineConfig({
  testDir: './e2e',
  // 60s per test: the first render of a page on a cold server (and, in dev mode, its on-demand
  // compile) can take well over Playwright's 30s default.
  timeout: 60_000,
  expect: { timeout: 10_000 },
  // The stub keeps one request log and one server-side product cache is shared by every spec, so
  // specs run one after another; the suite is small enough that this costs seconds, not minutes.
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['junit', { outputFile: 'e2e-results.xml' }]] : 'list',
  use: {
    baseURL: BASE_URL,
    // 1440 wide so the desktop header navigation (visible from the `xl` breakpoint up) is shown.
    viewport: { width: 1440, height: 900 },
    // Every visitor has already answered the cookie banner: it is fixed to the bottom edge and
    // would sit over any control down there (a form's submit button), so no flow could click it.
    // The banner has its own unit test; no flow here is about it.
    storageState: {
      cookies: [{
        name: CONSENT_COOKIE,
        value: CookieConsent.Essential,
        domain: '127.0.0.1',
        path: '/',
        expires: -1,
        httpOnly: false,
        secure: false,
        sameSite: 'Lax',
      }],
      origins: [],
    },
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 900 } },
      // A flow written for a touch screen runs in the phone project only.
      grepInvert: /@phone/,
    },
    {
      // A phone: touch input and a narrow screen, so panes stack. Only the flows tagged `@phone` run here; the others
      // are about the desktop layout and would only repeat the chromium project.
      name: 'phone',
      use: { ...devices['Pixel 7'] },
      grep: /@phone/,
    },
  ],
  webServer: [
    {
      // The stand-in for the C# API: first, because the client fetches from it while rendering.
      command: 'node e2e/support/stub-backend.mjs',
      port: STUB_PORT,
      reuseExistingServer: !process.env.CI,
      timeout: 30_000,
      env: { E2E_STUB_PORT: String(STUB_PORT) },
    },
    {
      command: useDevServer
        ? `npx nuxt dev --host 127.0.0.1 --port ${PORT}`
        : 'node .output/server/index.mjs',
      url: BASE_URL,
      reuseExistingServer: !process.env.CI,
      timeout: 180_000,
      env: {
        PORT: String(PORT),
        HOST: '127.0.0.1',
        // The backend is the stub. `API_URL` feeds nuxt.config at dev start, `NUXT_API_URL` feeds
        // the runtime config of an already-built server.
        API_URL: `http://127.0.0.1:${STUB_PORT}`,
        NUXT_API_URL: `http://127.0.0.1:${STUB_PORT}`,
        // Local auth: the sign-in form instead of the redirect to the identity provider. The
        // server and public halves are separate settings, so both are pinned.
        AUTH_MODE: 'local',
        NUXT_AUTH_MODE: 'local',
        NUXT_PUBLIC_AUTH_MODE: 'local',
        NUXT_PUBLIC_BASE_URL: BASE_URL,
        // Pinned to aurora, not the shipped default (classic), so a changed default cannot silently change which
        // components these specs drive.
        NUXT_PUBLIC_PORTAL_TEMPLATE: 'aurora',
      },
    },
  ],
})
