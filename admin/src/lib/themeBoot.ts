/**
 * Applies the operator's theme to the document: before first paint and on every later change.
 *
 * {@link bootTheme} is the one implementation: `themeBootPlugin` serialises it into `theme-boot.js`, a
 * same-origin script at the end of `<head>` (the Content-Security-Policy refuses an inline one; the page
 * still never flashes dark before switching to light),
 * and the theme store calls the same function after a choice or a system-scheme change. Because
 * it is turned into source text with `toString()`, it must stay self-contained: no imports, no
 * closure over module values: everything it needs arrives in its {@link ThemeBootConfig}.
 */
// vite.config.ts imports this module under the Node tsconfig, which has no DOM types of its own.
/// <reference lib="dom" />
// Relative and with its extension, not through the `@/` alias: Node loads this file for vite.config.ts
// and knows neither the alias nor an extensionless relative import.
import { AdminTheme } from '../types/admintheme.ts'

/** Everything {@link bootTheme} needs, passed in so the function can run as a standalone script. */
export interface ThemeBootConfig {
  /** localStorage key holding the operator's {@link AdminTheme} choice. */
  storageKey: string
  /** Stored value meaning "always light". */
  light: string
  /** Stored value meaning "always dark". */
  dark: string
  /** Media query that is true when the system prefers the light scheme. */
  lightQuery: string
  /** Class on `<html>` that switches `main.css` to the light palette. */
  lightClass: string
  /** Browser chrome colour per scheme: the page background (`--surface-base` in `main.css`). */
  colors: {
    /** Chrome colour of the light scheme. */
    light: string
    /** Chrome colour of the dark scheme. */
    dark: string
  }
}

/** localStorage key holding the operator's theme choice. */
export const THEME_STORAGE_KEY = 'admin.theme'

/** The panel's theme configuration, shared by the pre-paint script and the store. */
export const THEME_BOOT_CONFIG: ThemeBootConfig = {
  storageKey: THEME_STORAGE_KEY,
  light: AdminTheme.Light,
  dark: AdminTheme.Dark,
  lightQuery: '(prefers-color-scheme: light)',
  lightClass: 'light',
  colors: { light: '#f4f6fb', dark: '#0a0a0f' },
}

/**
 * Reads the stored choice, resolves "system" through `prefers-color-scheme`, toggles the light
 * class on `<html>` and points every `theme-color` meta at the resolved scheme's colour.
 *
 * Storage that throws (private mode, blocked site data) counts as no choice, which means
 * "follow the system".
 *
 * @param config - Storage key, stored values, media query, class and colours.
 * @returns True when the light scheme was applied.
 */
export const bootTheme = (config: ThemeBootConfig): boolean => {
  let stored: string | null = null
  try {
    stored = window.localStorage.getItem(config.storageKey)
  } catch {
    stored = null
  }
  let light = stored === config.light
  if (stored !== config.light && stored !== config.dark) {
    light = typeof window.matchMedia === 'function' && window.matchMedia(config.lightQuery).matches
  }
  document.documentElement.classList.toggle(config.lightClass, light)
  const color = light ? config.colors.light : config.colors.dark
  document.querySelectorAll('meta[name="theme-color"]').forEach((meta) => {
    // The static metas carry a media query per scheme; an explicit choice must win over it.
    meta.removeAttribute('media')
    meta.setAttribute('content', color)
  })
  return light
}

/**
 * Source text of the pre-paint script: {@link bootTheme} called with the panel's config.
 *
 * @param config - Configuration to embed; the panel's own by default.
 * @returns A self-invoking script body for a `<script>` tag.
 */
export const themeBootScript = (config: ThemeBootConfig = THEME_BOOT_CONFIG): string =>
  `(${bootTheme.toString()})(${JSON.stringify(config)})`
