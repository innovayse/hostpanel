/**
 * The texts of the documentation interface in every language: the navigation bar, the sidebar titles,
 * the search box, the theme controls, the custom components and the text of the placeholder pages.
 * The texts live in one JSON file per language (`en.json`, `ru.json`, `hy.json`) with the same keys in
 * all three; `tests/i18n.test.ts` fails when a key is missing. Pure data and helpers, no Vue and no I/O,
 * so the config (Node) and the theme (browser) read them the same way.
 */
import en from './en.json'
import hy from './hy.json'
import ru from './ru.json'
import { DEFAULT_LOCALE, LOCALE_KEYS, LOCALES, LocaleKey } from './locales'

/** The shape of a language file; English is the reference the other two must match. */
export type UiStrings = typeof en

/** The texts of every language. */
export const UI_STRINGS: Record<LocaleKey, UiStrings> = {
  [LocaleKey.En]: en,
  [LocaleKey.Ru]: ru as UiStrings,
  [LocaleKey.Hy]: hy as UiStrings,
}

/**
 * Finds the language of a `lang` attribute value.
 * @param lang A BCP 47 tag such as `ru-RU`, or nothing.
 * @returns The language whose tag or primary subtag matches, English otherwise.
 */
export const localeOfLang = (lang: string | undefined): LocaleKey => {
  const primary = (lang ?? '').toLowerCase().split('-')[0]
  return LOCALE_KEYS.find((key) => key === primary || LOCALES[key].lang.toLowerCase() === (lang ?? '').toLowerCase()) ?? DEFAULT_LOCALE
}

/**
 * The texts for a language.
 * @param locale The language.
 * @returns Its texts.
 */
export const stringsFor = (locale: LocaleKey): UiStrings => {
  return UI_STRINGS[locale]
}

/**
 * The texts for the `lang` attribute of the open page.
 * @param lang A BCP 47 tag, or nothing (a test or a page without a language).
 * @returns The texts of that language, English when the tag is unknown.
 */
export const stringsForLang = (lang: string | undefined): UiStrings => {
  return UI_STRINGS[localeOfLang(lang)]
}

/**
 * Fills the `{name}` placeholders of a text.
 * @param template Text such as `Documentation version: {version}`.
 * @param values Value for each placeholder name; a placeholder without a value stays as written.
 * @returns The finished text.
 */
export const format = (template: string, values: Record<string, string>): string => {
  return template.replace(/\{(\w+)\}/g, (match: string, name: string) => values[name] ?? match)
}
