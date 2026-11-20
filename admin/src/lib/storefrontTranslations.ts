/**
 * Helpers that turn the per-locale form state of a storefront content form into the
 * `translations` object the API takes, and back.
 */

import { ContentLanguage } from '@/locales/contentLanguage'
import type { StorefrontTranslations } from '@/types/storefront'

/** Every locale the forms carry a tab for, in tab order: every content language. */
export const STOREFRONT_LOCALES: ContentLanguage[] = Object.values(ContentLanguage)

/**
 * Makes one value per locale.
 *
 * @param make - Makes the value of one locale; called once per locale so the values never share an object.
 * @returns A value for each locale.
 */
const perLocale = <T>(make: (locale: ContentLanguage) => T): Record<ContentLanguage, T> => {
  // Every locale is assigned in the loop below, which the empty literal cannot show to the compiler.
  const values = {} as Record<ContentLanguage, T>
  for (const locale of STOREFRONT_LOCALES) {
    values[locale] = make(locale)
  }
  return values
}

/**
 * Builds one blank form per locale.
 *
 * @param blank - Makes one blank form; called once per locale so the forms never share an object.
 * @returns A blank form for each locale.
 */
export const blankTranslations = <T>(blank: () => T): Record<ContentLanguage, T> => perLocale(() => blank())

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
): Record<ContentLanguage, T> => {
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
 * Tells, per locale, whether its form carries text; the language tabs mark the ones that do not.
 *
 * @param forms - The per-locale forms.
 * @param isFilled - Whether a form carries text.
 * @returns Whether each locale is filled.
 */
export const filledLocales = <T>(
  forms: Record<ContentLanguage, T>,
  isFilled: (form: T) => boolean,
): Record<ContentLanguage, boolean> => perLocale((locale) => isFilled(forms[locale]))

/**
 * Collects the translations worth sending: the trimmed forms of the locales whose main field is filled.
 *
 * @param forms - The per-locale forms.
 * @param isFilled - Whether a form carries text (the locale is skipped otherwise).
 * @param trim - Returns the form with its text trimmed.
 * @returns The translations object of the payload.
 */
export const collectTranslations = <T>(
  forms: Record<ContentLanguage, T>,
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

/**
 * Spreads the API's one `translations` refusal over the fields of the active locale tab.
 *
 * The server reports a missing or invalid translation under `translations` as a whole, while the form shows a
 * sentence under each field that makes it up, so the same sentence is copied to the keys of those fields.
 *
 * @param errors - The store's field sentences by key.
 * @param keys - Keys of the translated fields of the form.
 * @returns The sentences by key, with the `translations` one repeated under every key in `keys`.
 */
export const translationErrors = (errors: Readonly<Record<string, string>>, keys: readonly string[]): Record<string, string> => {
  const sentence = errors.translations
  if (sentence === undefined) {
    return { ...errors }
  }
  return { ...errors, ...Object.fromEntries(keys.map((key) => [key, sentence])) }
}
