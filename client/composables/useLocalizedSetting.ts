/**
 * Reads an operator setting that can differ per language.
 *
 * Some settings (the About story, the office address, the working hours) are written by the
 * operator in each language they publish. They are stored as a default row plus a Russian and an
 * Armenian row; English visitors, and any visitor whose language has no row of its own, get the
 * default. One reader holds that rule so no page restates it.
 *
 * @module composables/useLocalizedSetting
 */

import type { PortalSettingKey } from '~/constants/portalSettingKey'
import { AppLocale } from '~/types/applocale'

/** The rows that hold one text in each language, with the runtime-config key each falls back to. */
export interface LocalizedSettingSpec {
  /** The default-language row and its runtime-config key. */
  default: { key: PortalSettingKey, envKey: string }
  /** The Russian row and its runtime-config key. */
  ru: { key: PortalSettingKey, envKey: string }
  /** The Armenian row and its runtime-config key. */
  hy: { key: PortalSettingKey, envKey: string }
}

/**
 * Picks the language's own row, when the visitor's language has one.
 *
 * The one place that says which languages have a row of their own: English, and any language without a
 * row, read the default one, so the caller falls back to it when this answers `undefined`.
 *
 * @param locale The visitor's language.
 * @param rows The Russian and Armenian rows of a setting stored once per language.
 * @returns The row of the visitor's language; `undefined` for a language that reads the default row.
 */
export const ownRowFor = <Row>(locale: string, rows: { ru: Row, hy: Row }): Row | undefined =>
  locale === AppLocale.Ru ? rows.ru : locale === AppLocale.Hy ? rows.hy : undefined

/**
 * Creates the per-language reader.
 *
 * @returns A function that resolves a {@link LocalizedSettingSpec} for the active language.
 */
export const useLocalizedSetting = () => {
  const { get } = usePortalSettings()
  const { locale } = useI18n()

  /**
   * Reads the text in the active language, falling back to the default one.
   *
   * @param spec The default, Russian and Armenian rows of the setting.
   * @returns The text, trimmed; empty when nothing is set. Reactive when read inside a computed.
   */
  const getLocalized = (spec: LocalizedSettingSpec): string => {
    const own = ownRowFor(locale.value, spec)
    const perLocale = own ? get(own.key, own.envKey) : ''
    return perLocale || get(spec.default.key, spec.default.envKey)
  }

  return { getLocalized }
}
