/**
 * Test support shared by every portal template: the real composables the template layout, page and section
 * components call, fed by the operator's settings from the stub server.
 *
 * Under Nuxt, `useSwitchLocalePath`, `useAppColorMode`, `usePortalSettings`, `useAuthMode`, `useBrandLogo`,
 * `useContactDetails`, `useContactHours`, `useSiteIdentity` and `useSecurityFeatures` are auto-imported. Vitest has no such step, so the real ones of ours
 * are registered under their auto-import names by {@link usePortalSettingsServer}, and only the i18n router's
 * `useSwitchLocalePath` is stood in for. The settings map is what an operator would have entered under
 * Admin > Settings; it reaches the composables through the real `portal-settings` plugin, as in the app.
 * Template-specific setups (the aurora session, say) build on {@link useTemplateGlobals}.
 *
 * @module tests/helpers/templateGlobals
 */

import { vi } from 'vitest'
import { useAppColorMode } from '~/composables/useAppColorMode'
import { useAuthMode } from '~/composables/useAuthMode'
import { useBrandLogo } from '~/composables/useBrandLogo'
import { useContactDetails } from '~/composables/useContactDetails'
import { useContactHours } from '~/composables/useContactHours'
import { usePortalSettings } from '~/composables/usePortalSettings'
import { useSecurityFeatures } from '~/composables/useSecurityFeatures'
import { useSiteIdentity } from '~/composables/useSiteIdentity'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { AuthMode } from '~/types/authmode'
import { loadPortalSettings, usePortalSettingsServer } from './portalSettings'
import type { PortalSettingsServer } from './portalSettings'
import { stubSwitchLocalePath } from './runtimeStubs'

/** Site name the identity composable reports unless a test says otherwise. */
const DEFAULT_SITE_NAME = 'Acme Host'

/** The real composables every template component reads, by their auto-import name. */
const TEMPLATE_COMPOSABLES = { usePortalSettings, useAppColorMode, useAuthMode, useBrandLogo, useContactDetails, useContactHours, useSecurityFeatures, useSiteIdentity }

/** Options for {@link TemplateGlobals.loadGlobals}. */
export interface TemplateGlobalsOptions {
  /** Operator settings by their dotted key; an absent key reads as unset. */
  settings?: Record<string, string>
  /** Light-mode logo URL the operator uploaded; empty means no upload. */
  logo?: string
  /** Dark-mode logo URL the operator uploaded; when set the brand composable reports a dark variant. */
  logoDark?: string
  /** Site name the operator set. */
  siteName?: string
  /** How the deployment signs visitors in; the standalone local mode unless a test says otherwise. */
  authMode?: AuthMode
}

/** What {@link useTemplateGlobals} hands the test file. */
export interface TemplateGlobals extends PortalSettingsServer {
  /**
   * Loads the operator's settings through the real plugin and stands in for the i18n router's switch path.
   *
   * @param options - Settings, logo, site name and authentication mode to report.
   * @returns Resolves once the real composables can read the settings.
   */
  loadGlobals: (options?: TemplateGlobalsOptions) => Promise<void>
}

/**
 * Registers the hooks every test of a template component needs: the stub server under `$fetch` and the real
 * composables registered as the globals Nuxt would inject. Call it once at the top of the test file, then call
 * {@link TemplateGlobals.loadGlobals} in each test.
 *
 * @returns The stub accessor and reply table of the settings server, and the loader of the operator's settings.
 */
export const useTemplateGlobals = (): TemplateGlobals => {
  const server = usePortalSettingsServer(TEMPLATE_COMPOSABLES)

  /**
   * Stands in for the globals that do not exist in a unit test and loads the settings.
   *
   * @param options - Settings, logo, site name and authentication mode to report.
   * @returns Resolves once the settings are in the shared state.
   */
  const loadGlobals = async (options: TemplateGlobalsOptions = {}): Promise<void> => {
    stubSwitchLocalePath()
    vi.stubGlobal('useRuntimeConfig', () => ({ public: { authMode: options.authMode ?? AuthMode.Local } }))
    await loadPortalSettings(server.replies, {
      [PortalSettingKey.SiteName]: options.siteName ?? DEFAULT_SITE_NAME,
      ...(options.logo ? { [PortalSettingKey.Logo]: options.logo } : {}),
      ...(options.logoDark ? { [PortalSettingKey.LogoDark]: options.logoDark } : {}),
      ...options.settings,
    })
  }

  return { ...server, loadGlobals }
}
