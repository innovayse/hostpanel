/**
 * The operator's colour scheme: light or dark.
 *
 * Until the operator switches once, the panel follows the operating system's scheme. The choice then
 * lives in localStorage so the pre-paint script (`lib/themeBoot.ts`) can apply it before the app
 * loads. This store owns changing it afterwards, and re-applies the system scheme whenever the
 * operating system switches while nothing is stored.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { bootTheme, THEME_BOOT_CONFIG } from '@/lib/themeBoot'
import { AdminTheme } from '@/types/admintheme'

/**
 * Persists a choice. Storage that throws only costs the choice surviving a reload.
 *
 * @param theme - The choice to store.
 */
const writeStoredTheme = (theme: AdminTheme): void => {
  try {
    window.localStorage.setItem(THEME_BOOT_CONFIG.storageKey, theme)
  } catch {
    // Private mode or blocked site data: the choice still applies for this page.
  }
}

/** Pinia store holding the theme choice and applying it to the document. */
export const useThemeStore = defineStore('theme', () => {
  /** True when the light palette is applied right now. */
  const isLight = ref(false)

  /** The choice made on this page, applied even when storage could not keep it; undefined until the first switch. */
  let chosen: AdminTheme | undefined

  /** The system scheme query, kept so its listener is added only once. */
  let media: MediaQueryList | null = null

  /** Re-applies the stored choice, or the system scheme when none is stored, to the document. */
  const apply = (): void => {
    isLight.value = bootTheme(THEME_BOOT_CONFIG, chosen)
  }

  /** Applies the choice and starts following system scheme changes. Safe to call twice. */
  const init = (): void => {
    apply()
    if (media === null && typeof window.matchMedia === 'function') {
      media = window.matchMedia(THEME_BOOT_CONFIG.lightQuery)
      // Only an unstored choice depends on it, but bootTheme re-reads the storage, so one listener is enough.
      media.addEventListener('change', apply)
    }
  }

  /** Switches to the other of light and dark, stores the choice and applies it. */
  const toggle = (): void => {
    chosen = isLight.value ? AdminTheme.Dark : AdminTheme.Light
    writeStoredTheme(chosen)
    apply()
  }

  return { isLight, init, toggle }
})
