/**
 * The portal's languages as one list, derived from the {@link AppLocale} enum so no file spells
 * `['en', 'ru', 'hy', 'de', 'fr']` out again.
 *
 * @module constants/appLocales
 */

import { AppLocale } from '../types/applocale'

/** Every language the portal is published in, in the order the language switcher lists them. */
export const APP_LOCALES: readonly AppLocale[] = Object.values(AppLocale)

/** The language served without a URL prefix and used when a value is missing or unknown. */
export const DEFAULT_APP_LOCALE: AppLocale = AppLocale.En
