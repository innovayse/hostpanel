/**
 * The operator's logo and favicon, as the admin panel renders them.
 *
 * The panel used to ship its own `public/logo.svg` and `public/favicon.svg` and show
 * those whatever an operator had configured, so a white-label deployment carried this
 * product's mark through its whole back office while the storefront carried the
 * customer's. The branding settings are one brand, not a storefront-only one, so both
 * surfaces read the same two keys.
 *
 * Loaded once per page load and never refetched: an operator who changes the logo is
 * changing it for the next visit, which is what the settings screen already promises
 * ("Changes take effect on the next page load").
 *
 * Deliberately failure-tolerant. A backend that is down, or too old to serve
 * `/settings/public`, leaves both values empty and every consumer falls back to the
 * built-in asset — the panel must still render its login screen when the API cannot be
 * reached, because that is exactly when an operator needs to get in and look.
 */

import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { usePublicSettingsApi } from '../composables/apis/usePublicSettingsApi'

/** Setting key holding the logo drawn on light backgrounds. */
const LOGO_KEY = 'portal.logo'

/**
 * Setting key holding the logo drawn on dark backgrounds.
 *
 * The panel is dark-only, so this is the one it wants; it used to read the light
 * variant alone and paint a logo made for white paper onto its dark sidebar.
 */
const LOGO_DARK_KEY = 'portal.logo.dark'

/** Setting key holding the browser tab icon. */
const FAVICON_KEY = 'portal.favicon'

export const useBrandingStore = defineStore('branding', () => {
  /** Operator's logo URL; empty string means "use the built-in mark". */
  /** The light-background logo, empty when the operator has not set one. */
  const logoUrl = ref('')

  /** The dark-background logo, empty when the operator has not set one. */
  const logoDarkUrl = ref('')

  /**
   * The logo to draw on the panel's dark surfaces: the dark variant when set, else
   * the light one — the same fallback the storefront and the settings screen promise
   * ("Empty falls back to the light logo"). Empty means "use the built-in mark".
   */
  const logoOnDarkUrl = computed(() => logoDarkUrl.value || logoUrl.value)

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
    if (loaded.value) return
    loaded.value = true

    try {
      const rows = await usePublicSettingsApi().fetchPublicSettings()
      logoUrl.value = rows.find(r => r.key === LOGO_KEY)?.value?.trim() ?? ''
      logoDarkUrl.value = rows.find(r => r.key === LOGO_DARK_KEY)?.value?.trim() ?? ''
      faviconUrl.value = rows.find(r => r.key === FAVICON_KEY)?.value?.trim() ?? ''
    } catch {
      // Swallowed on purpose — see the note at the top of this file. Branding is not
      // worth a broken login screen, and the fallbacks are real files.
    }
  }

  return { logoUrl, logoDarkUrl, logoOnDarkUrl, faviconUrl, loaded, load }
})
