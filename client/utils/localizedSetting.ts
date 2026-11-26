/**
 * The names of the rows a setting kept once per language is stored in.
 *
 * The operator writes such a setting (the About story, the office address, the working hours, the chat
 * token, a legal text) in every language the portal is published in. The default language keeps the plain
 * key (`portal.contact.address`); every other language keeps `<key>.<language>` (`portal.contact.address.de`),
 * and a language without text of its own reads the default row. This is the one place that names a row, so
 * a language added to {@link AppLocale} has its rows without a line changed anywhere else; it mirrors
 * `PortalSettingKeys.ForLanguage` in the backend.
 *
 * @module utils/localizedSetting
 */

// Relative, not `~/`: `nuxt.config.ts` imports this module and has no alias to resolve.
import { APP_LOCALES, DEFAULT_APP_LOCALE } from '../constants/appLocales'

/**
 * Names the row of one language.
 *
 * @param key The default-language key, e.g. `portal.contact.address`.
 * @param locale The language whose row is wanted.
 * @returns The key itself for the default language, else the key with a `.<locale>` suffix.
 */
export const localizedSettingKey = (key: string, locale: string): string =>
  locale === DEFAULT_APP_LOCALE ? key : `${key}.${locale}`

/**
 * Names the runtime-config entry that backs the row of one language when the setting is not stored.
 *
 * @param envKey The runtime-config key of the default language, e.g. `portalContactAddress`.
 * @param locale The language whose entry is wanted.
 * @returns The key itself for the default language, else the key with the capitalised language
 *   appended (`portalContactAddressRu`).
 */
export const localizedSettingEnvKey = (envKey: string, locale: string): string =>
  locale === DEFAULT_APP_LOCALE ? envKey : `${envKey}${locale.charAt(0).toUpperCase()}${locale.slice(1)}`

/**
 * Names the environment variable that backs the row of one language.
 *
 * @param envVar The variable of the default language, e.g. `NUXT_PUBLIC_PORTAL_CONTACT_ADDRESS`.
 * @param locale The language whose variable is wanted.
 * @returns The variable with the upper-case language appended (`NUXT_PUBLIC_PORTAL_CONTACT_ADDRESS_DE`).
 */
export const localizedSettingEnvVar = (envVar: string, locale: string): string => `${envVar}_${locale.toUpperCase()}`

/**
 * Builds the runtime-config entries of one setting kept per language, read from the environment.
 *
 * @param envKey The runtime-config key of the default language, e.g. `portalContactAddress`.
 * @param envVar The variable of the default language, e.g. `NUXT_PUBLIC_PORTAL_CONTACT_ADDRESS`.
 * @param env The process environment.
 * @returns One entry per language but the default, each the variable's value or empty.
 */
export const localizedRuntimeConfig = (envKey: string, envVar: string, env: Record<string, string | undefined>): Record<string, string> =>
  Object.fromEntries(
    APP_LOCALES.filter(locale => locale !== DEFAULT_APP_LOCALE)
      .map(locale => [localizedSettingEnvKey(envKey, locale), env[localizedSettingEnvVar(envVar, locale)] || ''])
  )

/**
 * Lists the keys of every language's row of one setting.
 *
 * @param key The default-language key.
 * @returns One key per portal language, the default language first.
 */
export const localizedSettingKeys = (key: string): string[] =>
  APP_LOCALES.map(locale => localizedSettingKey(key, locale))
