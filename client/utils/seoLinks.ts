/**
 * The canonical and alternate-language URLs of a public page, built once.
 *
 * `useSeo` and the pages that set their own head (a hosting plan, a product) each carried a copy of
 * this arithmetic. English is the default language and has no URL prefix; every other language is
 * prefixed with its code.
 *
 * @module utils/seoLinks
 */

import { AppLocale } from '~/types/applocale'

/** The `hreflang` value that points search engines at the page to show when no language matches. */
export const HREFLANG_DEFAULT = 'x-default'

/**
 * One `<link rel="alternate">` or `<link rel="canonical">` entry of the document head.
 *
 * A type alias rather than an interface on purpose: head's `link` input has `data-*` index
 * signatures, which an alias satisfies implicitly and an interface never does. It is a union
 * discriminated by `rel` because head types an alternate link by what it points at (a language,
 * a feed, a media query) and only accepts the language form when `hreflang` is certainly there.
 */
export type SeoLink =
  | {
    /** Link relation of the canonical address. */
    rel: 'canonical'
    /** Absolute URL. */
    href: string
  }
  | {
    /** Link relation of a language alternate. */
    rel: 'alternate'
    /** Language the alternate is for. */
    hreflang: string
    /** Absolute URL. */
    href: string
  }

/**
 * Removes a leading language prefix from a path.
 *
 * @param path - A route path, possibly starting with `/ru` or `/hy`.
 * @returns The path as it is in the default language.
 */
export const stripLocalePrefix = (path: string): string => path.replace(/^\/(en|ru|hy|de)/, '')

/**
 * Builds the absolute URL of a page in one language.
 *
 * @param baseUrl - The site origin, without a trailing slash.
 * @param path - The page path in the default language (no prefix); empty means the home page.
 * @param locale - The language of the URL.
 * @returns The absolute URL.
 */
export const localizedUrl = (baseUrl: string, path: string, locale: string): string =>
  locale === AppLocale.En ? `${baseUrl}${path || '/'}` : `${baseUrl}/${locale}${path}`

/**
 * Builds the `<link>` entries a page needs: its canonical URL and one alternate per language.
 *
 * @param baseUrl - The site origin, without a trailing slash.
 * @param path - The page path in the default language.
 * @param locale - The language the page is being served in, which decides the canonical URL.
 * @returns The canonical link, an alternate per language and the `x-default` alternate.
 */
export const buildSeoLinks = (baseUrl: string, path: string, locale: string): SeoLink[] => [
  { rel: 'canonical', href: localizedUrl(baseUrl, path, locale) },
  ...Object.values(AppLocale).map((code) => ({ rel: 'alternate' as const, hreflang: code, href: localizedUrl(baseUrl, path, code) })),
  { rel: 'alternate', hreflang: HREFLANG_DEFAULT, href: localizedUrl(baseUrl, path, AppLocale.En) },
]
