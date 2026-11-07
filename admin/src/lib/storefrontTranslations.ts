/**
 * Helpers that turn the per-locale form state of a storefront content form into the
 * `translations` object the API takes, and back.
 */

import { MessageLanguage } from '@/locales/messages'
import type { StorefrontTranslations } from '@/types/storefront'

/** Every locale the forms carry a tab for, in tab order. */
export const STOREFRONT_LOCALES: MessageLanguage[] = [MessageLanguage.En, MessageLanguage.Ru, MessageLanguage.Hy, MessageLanguage.Es]

/**
 * Builds one blank form per locale.
 *
 * Written out per locale rather than looped from a string-indexed object: the `Record` type then
 * proves every locale is present, so adding a locale to the enum fails to compile until the helper
 * is updated.
 *
 * @param blank - Makes one blank form; called once per locale so the forms never share an object.
 * @returns A blank form for each locale.
 */
export const blankTranslations = <T>(blank: () => T): Record<MessageLanguage, T> => ({
  [MessageLanguage.En]: blank(),
  [MessageLanguage.Ru]: blank(),
  [MessageLanguage.Hy]: blank(),
  [MessageLanguage.Es]: blank(),
})

/**
 * Copies the stored translations into the per-locale forms, leaving a locale with no stored text blank.
 *
 * @param stored - Translations as the API returned them.
 * @param blank - Makes one blank form.
 * @returns A form for each locale.
 */
export const formsFromTranslations = <T extends object>(
  stored: StorefrontTranslations<T>,
  blank: () => T,
): Record<MessageLanguage, T> => {
  const forms = blankTranslations(blank)
  for (const locale of STOREFRONT_LOCALES) {
    const text = stored[locale]
    if (text) {
      forms[locale] = { ...text }
    }
  }
  return forms
}

/**
 * Collects the translations worth sending: the trimmed forms of the locales whose main field is filled.
 *
 * @param forms - The per-locale forms.
 * @param isFilled - Whether a form carries text (the locale is skipped otherwise).
 * @param trim - Returns the form with its text trimmed.
 * @returns The translations object of the payload.
 */
export const collectTranslations = <T>(
  forms: Record<MessageLanguage, T>,
  isFilled: (form: T) => boolean,
  trim: (form: T) => T,
): StorefrontTranslations<T> => {
  const collected: StorefrontTranslations<T> = {}
  for (const locale of STOREFRONT_LOCALES) {
    if (isFilled(forms[locale])) {
      collected[locale] = trim(forms[locale])
    }
  }
  return collected
}

/**
 * Picks the text to show in a list: English when written, otherwise the first locale that has any.
 *
 * @param stored - Translations as the API returned them.
 * @returns The chosen translation, or undefined when the entry has none.
 */
export const displayTranslation = <T>(stored: StorefrontTranslations<T>): T | undefined => {
  for (const locale of STOREFRONT_LOCALES) {
    const text = stored[locale]
    if (text) {
      return text
    }
  }
  return undefined
}
