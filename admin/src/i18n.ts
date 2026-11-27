/**
 * Admin panel i18n setup.
 *
 * Every shell and screen string is translated from `locales/<lang>/<area>.json` (assembled in
 * `locales/messages.ts`). Settings `key`/`description` values come from the backend as plain
 * strings, configuration identifiers and operator-facing copy stored in the database, so they
 * are not part of this catalogue.
 */
import { watch } from 'vue'
import { createI18n } from 'vue-i18n'
import { MessageLanguage, messages } from '@/locales/messages'

/** Locale codes the admin panel ships: every `MessageLanguage`, the interface languages, in the switcher's order. */
export const SUPPORTED_LOCALES: readonly MessageLanguage[] = Object.values(MessageLanguage)

/** One of the shipped locale codes. */
export type SupportedLocale = MessageLanguage

/** Locale an unseen visitor gets, and the one a missing message falls back to. */
const DEFAULT_LOCALE = MessageLanguage.En

/** localStorage key holding the operator's chosen locale. */
const STORAGE_KEY = 'admin.locale'

/**
 * The shipped locale a code names, or English for a code the panel does not ship (or none).
 *
 * @param code - A locale code, such as a saved preference or the language a formatter was asked for.
 * @returns A supported locale code.
 */
export const toSupportedLocale = (code: string | null | undefined): SupportedLocale =>
  SUPPORTED_LOCALES.find((locale) => locale === code) ?? DEFAULT_LOCALE

/**
 * Reads the operator's saved locale preference, falling back to English for
 * an unseen visitor or a value the panel no longer ships.
 *
 * @returns A supported locale code.
 */
const initialLocale = (): SupportedLocale => toSupportedLocale(localStorage.getItem(STORAGE_KEY))

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

// The document language follows the active locale, from startup and on every change, so assistive technology and the
// browser's own hyphenation, spell check and font choice use the language the panel is shown in.
watch(i18n.global.locale, (locale) => {
  document.documentElement.lang = locale
}, { immediate: true })
