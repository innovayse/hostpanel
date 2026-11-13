/**
 * The operator's logo and favicon, as the admin panel renders them.
 *
 * The branding settings are one brand, not a storefront-only one, so the panel and the
 * storefront read the same keys and a white-label deployment shows the operator's mark in
 * its back office too.
 *
 * Loaded once per page load and never refetched: an operator who changes the logo is
 * changing it for the next visit, which is what the settings screen already promises
 * ("Changes take effect on the next page load").
 *
 * Deliberately failure-tolerant. A backend that is down, or too old to serve
 * `/settings/public`, leaves both values empty and every consumer falls back to the
 * built-in asset: the panel must still render its login screen when the API cannot be
 * reached, because that is exactly when an operator needs to get in and look.
 */

import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { usePublicSettingsApi } from '@/composables/apis/usePublicSettingsApi'
import { runAction } from '@/lib/runAction'
import { SettingKey } from '@/types/setting'

/** Setting key holding the logo drawn on light backgrounds. */
const LOGO_KEY = SettingKey.Logo

/**
 * Setting key holding the logo drawn on dark backgrounds.
 *
 * The panel is dark-only, so this is the one it wants: the light variant is made for white
 * paper and would be unreadable on the dark sidebar.
 */
const LOGO_DARK_KEY = SettingKey.LogoDark

/** Setting key holding the browser tab icon. */
const FAVICON_KEY = SettingKey.Favicon

/** Pinia store holding the operator's logo and favicon URLs for the panel. */
export const useBrandingStore = defineStore('branding', () => {
  const api = usePublicSettingsApi()

  /** The light-background logo, empty when the operator has not set one. */
  const logoUrl = ref('')

  /** The dark-background logo, empty when the operator has not set one. */
  const logoDarkUrl = ref('')

  /**
   * The logo to draw on the panel's dark surfaces: the dark variant when set, else
   * the light one: the same fallback the storefront and the settings screen promise
   * ("Empty falls back to the light logo"). Empty means "use the built-in mark".
   */
  const logoOnDarkUrl = computed(() => logoDarkUrl.value || logoUrl.value)

  /**
   * The logo to draw on the panel's light surfaces: the light variant when set, else the
   * dark one, so an operator who uploaded a single logo sees it in both themes. Empty means
   * "use the built-in wordmark".
   */
  const logoOnLightUrl = computed(() => logoUrl.value || logoDarkUrl.value)

  /** Whether the operator has uploaded a logo of either kind, so the panel shows it instead of the built-in tile. */
  const hasLogo = computed(() => logoOnDarkUrl.value !== '')

  /** Operator's favicon URL; empty string means "leave index.html's links alone". */
  const faviconUrl = ref('')

  /** True once a load has been attempted, successfully or not. */
  const loaded = ref(false)

  /**
   * Reads the public settings and keeps the two branding values.
   *
   * Safe to call more than once; only the first call reaches the network.
   *
   * @returns Resolves when the attempt is over, whether or not it found anything.
   */
  const load = async (): Promise<void> => {
    if (loaded.value) {
      return
    }
    loaded.value = true

    // The sentence is discarded on purpose: see the note at the top of this file. Branding
    // is not worth a broken login screen, and the fallbacks are real files.
    const ignored = ref<string | null>(null)
    const result = await runAction(ignored, () => api.fetchPublicSettings())
    if (!result.ok) {
      return
    }
    const rows = result.value
    logoUrl.value = rows.find(r => r.key === LOGO_KEY)?.value?.trim() ?? ''
    logoDarkUrl.value = rows.find(r => r.key === LOGO_DARK_KEY)?.value?.trim() ?? ''
    faviconUrl.value = rows.find(r => r.key === FAVICON_KEY)?.value?.trim() ?? ''
  }

  return { logoUrl, logoDarkUrl, logoOnDarkUrl, logoOnLightUrl, hasLogo, faviconUrl, loaded, load }
})
