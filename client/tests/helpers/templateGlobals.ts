/**
 * Test support shared by every portal template: stands in for the Nuxt composables the template
 * layout, page and section components call.
 *
 * Under Nuxt, `useLocalePath`, `useSwitchLocalePath`, `useAppColorMode`, `usePortalSettings`, `useAuthMode`, `useBrandLogo`,
 * `useSiteIdentity` and `useSecurityFeatures` (no claim switched on) are auto-imported. Vitest has no such step, so each is defined here as a
 * global, except `useLocalePath`, which {@link stubLocalePath} from `runtimeStubs` defines (this module only calls it).
 * The settings map is what an operator would have entered under Admin > Settings.
 * Template-specific stand-ins (the aurora auth store, say) build on {@link stubTemplateGlobals}.
 *
 * @module tests/helpers/templateGlobals
 */

import { computed, ref } from 'vue'
import { vi } from 'vitest'
import { stubLocalePath } from './runtimeStubs'

/** Site name the identity composable reports unless a test says otherwise. */
export const DEFAULT_SITE_NAME = 'Acme Host'

/** Options for {@link stubTemplateGlobals}. */
export interface TemplateGlobalsOptions {
  /** Operator settings by their dotted key; an absent key reads as an empty string. */
  settings?: Record<string, string>
  /** Light-mode logo URL the brand composable reports; empty means no upload. */
  logo?: string
  /** Dark-mode logo URL; when set the brand composable reports a dark variant. */
  logoDark?: string
  /** Alternative text the brand composable reports for the logo. */
  logoAlt?: string
  /** Site name the identity composable reports. */
  siteName?: string
}

/**
 * Defines the template components' auto-imported composables as globals.
 *
 * @param options Settings, logo and site name to report.
 */
export const stubTemplateGlobals = (options: TemplateGlobalsOptions = {}): void => {
  const settings = options.settings ?? {}
  stubLocalePath()
  vi.stubGlobal('useSwitchLocalePath', () => (code: string) => `/${code}`)
  vi.stubGlobal('usePortalSettings', () => ({ get: (key: string) => settings[key] ?? '' }))
  vi.stubGlobal('useAppColorMode', () => ({ isDark: ref(true), applied: ref('dark'), userToggleEnabled: ref(true), toggle: vi.fn() }))
  vi.stubGlobal('useAuthMode', () => ({ signInHref: ref('/client/login') }))
  vi.stubGlobal('useBrandLogo', () => ({
    // Each side falls back to the other, as `resolveBrandLogos` does for the real composable.
    light: ref(options.logo || options.logoDark || ''),
    dark: ref(options.logoDark || options.logo || ''),
    hasDarkVariant: ref(Boolean(options.logoDark)),
    alt: ref(options.logoAlt ?? ''),
  }))
  vi.stubGlobal('useSecurityFeatures', () => computed(() => []))
  vi.stubGlobal('useSiteIdentity', () => ({ name: ref(options.siteName ?? DEFAULT_SITE_NAME), tagline: ref('') }))
}
