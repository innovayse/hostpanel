/**
 * The languages of the documentation site and the rules that tell which language a page path is in.
 * Pure helpers with no I/O, shared by the site config, the SEO hooks and the theme, so the browser
 * bundle and the build agree on what `/ru/staff/orders` means. The data lives in `locales.json`,
 * which the build scripts read too.
 */
import settings from './locales.json'

/** The languages of the site. The value is the folder name of a language and its key in the language files. */
export enum LocaleKey {
  /** English: the source language, served at the root of the site. */
  En = 'en',
  /** Russian: served below `/ru/`. */
  Ru = 'ru',
  /** Armenian: served below `/hy/`. */
  Hy = 'hy',
}

/** What the site knows about one language. */
export interface LocaleInfo {
  /** BCP 47 tag written to the `lang` attribute of the page. */
  lang: string
  /** Locale written to the `og:locale` tag. */
  ogLocale: string
  /** Code written to the `hreflang` attribute of the alternate links. */
  hreflang: string
  /** Name of the language in that language, shown in the language menu. */
  label: string
  /** Folder of the language in the address, below the site root; empty for the default language, whose pages sit at the root. */
  prefix: string
  /** Folder of the language's Markdown sources below the docs root. Every language has one, English too (`docs/en`). */
  folder: string
}

/** The default language: its pages sit at the root and it is the fallback of every other language. */
export const DEFAULT_LOCALE: LocaleKey = settings.default as LocaleKey

/** Every language, the default first. */
export const LOCALE_KEYS: readonly LocaleKey[] = Object.keys(settings.locales) as LocaleKey[]

/** Every language except the default one. */
export const TRANSLATED_LOCALES: readonly LocaleKey[] = LOCALE_KEYS.filter((key) => key !== DEFAULT_LOCALE)

/** Information about each language. */
export const LOCALES: Record<LocaleKey, LocaleInfo> = settings.locales as Record<LocaleKey, LocaleInfo>

/** Folder (below the docs root) the build writes the placeholder pages to; it is never committed. */
export const FALLBACK_DIR: string = settings.fallbackDir

/** Paths (below a language's source folder) that are not translated: folders end with a slash. */
export const UNTRANSLATED: readonly string[] = settings.untranslated

/**
 * Tells which language a page path is in.
 * @param path A source path (`ru/staff/orders.md`) or an address path (`ru/staff/orders`), no leading slash needed.
 * @returns The language whose folder the path starts with, English when none does.
 */
export const localeOfPath = (path: string): LocaleKey => {
  const first = path.replace(/^\/+/, '').split('/')[0].replace(/\.md$/, '')
  return TRANSLATED_LOCALES.find((key) => LOCALES[key].prefix === first) ?? DEFAULT_LOCALE
}

/**
 * Removes the language folder from a path.
 * @param path A source or address path.
 * @returns The same path in the English tree, without a leading slash: `ru/staff/orders` gives `staff/orders`, and `ru/` gives an empty string.
 */
export const stripLocale = (path: string): string => {
  const clean = path.replace(/^\/+/, '')
  const locale = localeOfPath(clean)
  if (locale === DEFAULT_LOCALE) {
    return clean
  }
  return clean.slice(LOCALES[locale].prefix.length).replace(/^\/+/, '')
}

/**
 * Puts a page path into a language.
 * @param locale The language.
 * @param path A path in the English tree, with or without a leading slash.
 * @returns `ru/staff/orders` for Russian, the path itself for English; no leading slash.
 */
export const withLocale = (locale: LocaleKey, path: string): string => {
  const clean = path.replace(/^\/+/, '')
  const prefix = LOCALES[locale].prefix
  if (prefix === '') {
    return clean
  }
  return clean === '' ? `${prefix}/` : `${prefix}/${clean}`
}

/**
 * Puts a link of the English tree into a language, for the config.
 * @param locale The language.
 * @param link A link that starts with a slash, such as `/staff/orders` or `/`.
 * @returns The link with the language folder in front: `/ru/staff/orders`, `/ru/`.
 */
export const localizeLink = (locale: LocaleKey, link: string): string => {
  return `/${withLocale(locale, link)}`
}

/**
 * Whether a page of the English tree is translated at all.
 * @param path A source or address path in the English tree.
 * @returns False for the folders and pages listed in `UNTRANSLATED`.
 */
export const isTranslatable = (path: string): boolean => {
  const clean = path.replace(/^\/+/, '')
  return !UNTRANSLATED.some((entry) => (entry.endsWith('/') ? clean.startsWith(entry) || `${clean}/` === entry : clean === entry || clean === entry.replace(/\.md$/, '')))
}
