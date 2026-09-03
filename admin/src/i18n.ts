/**
 * Admin panel i18n setup.
 *
 * Only the static shell (nav, dashboard, the settings page's own chrome) is
 * translated here. Settings `key`/`description` values come from the backend
 * as plain strings — they are configuration identifiers and operator-facing
 * copy stored in the database, not frontend messages, so they are out of
 * scope for this catalogue and stay in English regardless of locale.
 */
import { createI18n } from 'vue-i18n'
import en from './locales/en.json'
import hy from './locales/hy.json'
import ru from './locales/ru.json'

/** Locale codes the admin panel ships. */
export const SUPPORTED_LOCALES = ['en', 'hy', 'ru'] as const

export type SupportedLocale = (typeof SUPPORTED_LOCALES)[number]

const STORAGE_KEY = 'admin.locale'

/**
 * Reads the operator's saved locale preference, falling back to English for
 * an unseen visitor or a value the panel no longer ships.
 *
 * @returns A supported locale code.
 */
function initialLocale(): SupportedLocale {
  const stored = localStorage.getItem(STORAGE_KEY)
  if (stored && (SUPPORTED_LOCALES as readonly string[]).includes(stored)) {
    return stored as SupportedLocale
  }
  return 'en'
}

export const i18n = createI18n({
  legacy: false,
  locale: initialLocale(),
  fallbackLocale: 'en',
  messages: { en, hy, ru },
})

/**
 * Switches the active locale and persists the choice.
 *
 * @param locale - The locale to switch to.
 */
export function setLocale(locale: SupportedLocale): void {
  i18n.global.locale.value = locale
  localStorage.setItem(STORAGE_KEY, locale)
}
