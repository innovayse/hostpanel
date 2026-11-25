/**
 * Reads an operator setting that can differ per language.
 *
 * Some settings (the About story, the office address, the working hours, the chat token) are written by
 * the operator in each language they publish. They are stored as a default row plus one row per other
 * portal language (`utils/localizedSetting.ts` names them); a visitor whose language has no text of its
 * own gets the default row. One reader holds that rule so no page restates it.
 *
 * @module composables/useLocalizedSetting
 */

import type { PortalSettingKey } from '~/constants/portalSettingKey'
import { localizedSettingEnvKey, localizedSettingKey } from '~/utils/localizedSetting'

/** The default row of a setting kept per language, with the runtime-config key it falls back to. */
export interface LocalizedSettingSpec {
  /** The default-language row; the other languages' rows are named from it. */
  key: PortalSettingKey
  /** The runtime-config key of the default row; the other languages' keys are named from it. */
  envKey: string
}

/** Reads one setting: the stored row, else the runtime-config entry, else empty (`usePortalSettings().get`). */
export type SettingReader = (key: string, envKey: string) => string

/**
 * Reads the text in one language, falling back to the default one.
 *
 * The one place that says which row a language reads: its own when it has text, else the default row.
 *
 * @param get Reads one setting by its key and runtime-config key.
 * @param locale The visitor's language.
 * @param spec The default row of the setting.
 * @returns The text, trimmed; empty when nothing is set.
 */
export const readLocalized = (get: SettingReader, locale: string, spec: LocalizedSettingSpec): string =>
  get(localizedSettingKey(spec.key, locale), localizedSettingEnvKey(spec.envKey, locale)) || get(spec.key, spec.envKey)

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
   * @param spec The default row of the setting.
   * @returns The text, trimmed; empty when nothing is set. Reactive when read inside a computed.
   */
  const getLocalized = (spec: LocalizedSettingSpec): string => readLocalized(get, locale.value, spec)

  return { getLocalized }
}
