/**
 * Translation for code that runs outside a component (stores, utilities).
 *
 * A component asks vue-i18n for words through `useI18n()`. A store has no component
 * instance, so it reaches the same message catalogue through the app's i18n instance.
 * The key is resolved at call time, never at module load, so a language switch is honoured.
 */

import { i18n } from '@/i18n'

/**
 * Resolves a message key in the operator's current language.
 *
 * @param key - Dotted key present in every locale catalogue.
 * @param named - Values for `{placeholders}` in the message.
 * @returns The translated sentence.
 */
export const translate = (key: string, named: Record<string, unknown> = {}): string =>
  i18n.global.t(key, named)

/**
 * Resolves a message key when the catalogue has it, otherwise returns the raw value.
 *
 * Used for values the API sends as identifiers (a ticket status, say) that may or may not
 * have a translation yet: an unknown value is shown as received instead of as a bare key.
 *
 * @param key - Dotted key to look up.
 * @param raw - What to show when the key is not in the catalogue.
 * @returns The translated sentence, or `raw`.
 */
export const translateOrRaw = (key: string, raw: string): string =>
  i18n.global.te(key) ? i18n.global.t(key) : raw

/**
 * Resolves a counted message key in a given language rather than the current one, choosing the plural form of
 * the count ("1 day ago", "2 days ago").
 *
 * @param key - Dotted key whose message holds the plural forms, with `{n}` for the count.
 * @param count - The counted number; selects the form and fills `{n}`.
 * @param locale - Language to write the sentence in.
 * @returns The sentence in that language, English when the catalogue lacks it.
 */
export const translateCountIn = (key: string, count: number, locale: string): string =>
  i18n.global.t(key, count, { locale })

/**
 * Resolves a message key in a given language rather than the current one.
 *
 * @param key - Dotted key present in every locale catalogue.
 * @param locale - Language to write the sentence in.
 * @returns The sentence in that language, English when the catalogue lacks it.
 */
export const translateIn = (key: string, locale: string): string => i18n.global.t(key, {}, { locale })
