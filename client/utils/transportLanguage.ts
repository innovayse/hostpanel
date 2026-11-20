/**
 * The language a request asks the backend to answer in.
 *
 * The C# API resolves the language of every sentence it writes (`detail`, `title`, field
 * messages) from the `X-Language` header alone, so the portal stamps that header on every call:
 * the browser-side transport (`composables/useApi.ts`) sends it to the Nitro routes, and the
 * server-side transport (`server/utils/api.ts`) forwards it to the API. Both validate the value
 * here, so an arbitrary header never reaches the backend and the two hops cannot disagree about
 * which languages exist.
 *
 * @module utils/transportLanguage
 */

import { APP_LOCALES, DEFAULT_APP_LOCALE } from '../constants/appLocales'

/** Header that carries the reader's language, on both hops (browser to Nitro, Nitro to API). */
export const LANGUAGE_HEADER = 'X-Language'

/** Languages the backend ships sentences for (`SupportedLanguages` on the C# side): every portal language. */
export const SUPPORTED_LANGUAGES: readonly string[] = APP_LOCALES

/** The i18n default locale (`defaultLocale` in `nuxt.config.ts`), used when the value is missing or unknown. */
export const DEFAULT_LANGUAGE: string = DEFAULT_APP_LOCALE

/**
 * Narrows a raw locale or header value to a language the backend supports.
 *
 * @param raw - A locale code or a header value, possibly absent.
 * @returns The value when it is one of the portal languages (trimmed, lower-cased), otherwise {@link DEFAULT_LANGUAGE}.
 */
export const resolveLanguage = (raw: string | null | undefined): string => {
  const normalised = raw?.trim().toLowerCase() ?? ''
  return SUPPORTED_LANGUAGES.includes(normalised) ? normalised : DEFAULT_LANGUAGE
}
