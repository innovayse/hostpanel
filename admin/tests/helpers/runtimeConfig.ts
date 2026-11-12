/**
 * Helper for tests that read the deployment configuration: sets `window.__APP_CONFIG__`, the
 * object `public/config.js` fills in production, so the real `lib/runtimeConfig` reads it.
 */
import { vi } from 'vitest'

/**
 * Sets the documentation address the deployment would inject through `config.js`. The global is
 * restored by `vi.unstubAllGlobals()`, which the suites already call after each test.
 *
 * @param docsUrl - Docs site address; an empty string hides every documentation link.
 */
export const stubDocsUrl = (docsUrl: string): void => {
  vi.stubGlobal('__APP_CONFIG__', { docsUrl })
}
