/**
 * Admin panel i18n setup.
 *
 * Every shell and screen string is translated from `locales/<lang>/<area>.json` (assembled in
 * `locales/messages.ts`). Settings `key`/`description` values come from the backend as plain
 * strings, configuration identifiers and operator-facing copy stored in the database, so they
 * are not part of this catalogue.
 */
import { createI18n } from 'vue-i18n'
import { MessageLanguage, messages } from '@/locales/messages'

/** Locale codes the admin panel ships. */
export const SUPPORTED_LOCALES = [MessageLanguage.En, MessageLanguage.Hy, MessageLanguage.Ru, MessageLanguage.Es] as const

/** One of the shipped locale codes. */
export type SupportedLocale = (typeof SUPPORTED_LOCALES)[number]

/** Locale an unseen visitor gets, and the one a missing message falls back to. */
const DEFAULT_LOCALE = MessageLanguage.En

/** localStorage key holding the operator's chosen locale. */
const STORAGE_KEY = 'admin.locale'

/**
 * Reads the operator's saved locale preference, falling back to English for
 * an unseen visitor or a value the panel no longer ships.
 *
 * @returns A supported locale code.
 */
const initialLocale = (): SupportedLocale => {
  const stored = localStorage.getItem(STORAGE_KEY)
  if (stored && (SUPPORTED_LOCALES as readonly string[]).includes(stored)) {
    return stored as SupportedLocale
  }
  return DEFAULT_LOCALE
}

/**
 * Russian plural rule: picks "one | few | many" (1 год, 2 года, 5 лет) from a three-form message.
 *
 * @param choice - The counted number.
 * @param choicesLength - How many forms the message carries.
 * @returns Index of the form to use.
 */
const russianPlural = (choice: number, choicesLength: number): number => {
  if (choicesLength < 3) {
    return choice === 1 ? 0 : 1
  }
  const abs = Math.abs(choice)
  const mod10 = abs % 10
  const mod100 = abs % 100
  if (mod10 === 1 && mod100 !== 11) {
    return 0
  }
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) {
    return 1
  }
  return 2
}

/** The application's vue-i18n instance (composition mode, English fallback). */
export const i18n = createI18n({
  legacy: false,
  locale: initialLocale(),
  fallbackLocale: DEFAULT_LOCALE,
  messages,
  pluralRules: { [MessageLanguage.Ru]: russianPlural },
})

/**
 * Switches the active locale and persists the choice.
 *
 * @param locale - The locale to switch to.
 */
export const setLocale = (locale: SupportedLocale): void => {
  i18n.global.locale.value = locale
  localStorage.setItem(STORAGE_KEY, locale)
}
