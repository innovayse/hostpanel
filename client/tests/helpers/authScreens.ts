/**
 * Helper for the client auth, layout and dashboard screens: the Nuxt auto-imports those screens use, and the real
 * composables the brand logo they draw reads.
 *
 * `mountClient` and `mountPage` (see `mountClient.ts`) register the kit, the real `Logo` and every
 * `components/client` component; the logo reads the operator's brand settings through the real composables this
 * module registers, so it renders the bundled wordmark (or an upload the test loads through the settings stub).
 *
 * @module tests/helpers/authScreens
 */

import { vi } from 'vitest'
import { useAppColorMode } from '~/composables/useAppColorMode'
import { useAuthMode } from '~/composables/useAuthMode'
import { useBrandLogo } from '~/composables/useBrandLogo'
import { usePortalSettings } from '~/composables/usePortalSettings'
import { useSiteIdentity } from '~/composables/useSiteIdentity'
import { AuthMode } from '~/types/authmode'
import { stubLocalePath, stubRoute, stubSwitchLocalePath } from '~/tests/helpers/runtimeStubs'

/** What a test can change about the stood-in runtime. */
export interface AuthRuntimeOptions {
  /** The route query (`route.query`). */
  query?: Record<string, string>
  /** The full path (`route.fullPath`). */
  fullPath?: string
  /** Whether the deployment signs in through the platform SSO. */
  isSso?: boolean
  /** The public runtime config; its `authMode` wins over {@link AuthRuntimeOptions.isSso}. */
  publicConfig?: Record<string, unknown>
}

/** The spies a test reads after mounting a screen. */
export interface AuthRuntimeSpies {
  /** `router.push`. */
  push: ReturnType<typeof vi.fn>
}

/**
 * Stands in for the auto-imports the auth screens use that `vitest.setup.ts` does not cover, and registers the real
 * `useAuthMode`, which reads the authentication mode from the stood-in runtime config, and the settings, identity,
 * colour-mode and brand-logo composables the real `Logo` calls.
 *
 * @param options - Route, SSO flag and public config for this test.
 * @returns The router spy.
 */
export const stubAuthRuntime = (options: AuthRuntimeOptions = {}): AuthRuntimeSpies => {
  const push = vi.fn()
  stubRoute(options)
  vi.stubGlobal('useRouter', () => ({ push, replace: vi.fn(), back: vi.fn() }))
  stubLocalePath()
  stubSwitchLocalePath()
  vi.stubGlobal('useRuntimeConfig', () => ({ public: { authMode: options.isSso ? AuthMode.Sso : AuthMode.Local, ...options.publicConfig } }))
  vi.stubGlobal('useAuthMode', useAuthMode)
  vi.stubGlobal('usePortalSettings', usePortalSettings)
  vi.stubGlobal('useSiteIdentity', useSiteIdentity)
  vi.stubGlobal('useAppColorMode', useAppColorMode)
  vi.stubGlobal('useBrandLogo', useBrandLogo)
  vi.mocked(navigateTo).mockClear()
  return { push }
}

