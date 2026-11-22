/**
 * The drafts of one setting that is stored once per language (`portal.chat.website_token`,
 * `.ru`, `.hy`), with what the `UiLocalizedField` tabs need to mark them.
 */

import { computed } from 'vue'
import type { ComputedRef, Ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSettingDraft } from '@/composables/useSettingDraft'
import type { SettingDraft } from '@/composables/useSettingDraft'
import { MessageLanguage } from '@/locales/messages'
import type { Setting } from '@/types/setting'

/**
 * The languages a setting kept per language is stored in: the ones with a `.ru` or `.hy` row beside the
 * plain one. A subset of the admin languages, because a language the interface is translated into does
 * not get its own row of every such setting.
 */
export const SettingLanguage = {
  /** English, the plain setting. */
  En: MessageLanguage.En,
  /** Russian, the `.ru` row. */
  Ru: MessageLanguage.Ru,
  /** Armenian, the `.hy` row. */
  Hy: MessageLanguage.Hy,
} as const

/** A language a per-language setting is stored in. */
export type SettingLanguage = (typeof SettingLanguage)[keyof typeof SettingLanguage]

/** The languages a setting kept per language carries a tab for, English first. */
export const SETTING_LOCALES: readonly [SettingLanguage, ...SettingLanguage[]] = [
  SettingLanguage.En,
  ...Object.values(SettingLanguage).filter((locale) => locale !== SettingLanguage.En),
]

/** What {@link useLocalizedSettingDrafts} hands the card. */
export interface LocalizedSettingDrafts {
  /** The draft of each language's row. */
  drafts: Record<SettingLanguage, SettingDraft>
  /** Whether each language's row holds text; an unseeded row counts as empty. */
  filled: ComputedRef<Record<SettingLanguage, boolean>>
  /** The sentence of each language whose last save was refused; a language that saved or did not save is absent. */
  errors: ComputedRef<Partial<Record<SettingLanguage, string>>>
}

/**
 * Creates the drafts of a setting stored once per language.
 *
 * Written out per language rather than looped from the enum, so adding a language to
 * `SettingLanguage` fails to compile until the keys name its row.
 *
 * @param settings - All system settings, as loaded from the backend.
 * @param keys - The settings key of each language's row.
 * @returns The drafts and the per-language marks for the tabs.
 */
export const useLocalizedSettingDrafts = (
  settings: Ref<Setting[]>,
  keys: Record<SettingLanguage, string>,
): LocalizedSettingDrafts => {
  const { t } = useI18n()

  /** The draft of each language's row. */
  const drafts: Record<SettingLanguage, SettingDraft> = {
    [SettingLanguage.En]: useSettingDraft(settings, keys[SettingLanguage.En]),
    [SettingLanguage.Ru]: useSettingDraft(settings, keys[SettingLanguage.Ru]),
    [SettingLanguage.Hy]: useSettingDraft(settings, keys[SettingLanguage.Hy]),
  }

  /** Whether each language's row holds text. */
  const filled = computed<Record<SettingLanguage, boolean>>(() => ({
    [SettingLanguage.En]: drafts[SettingLanguage.En].value.value.trim() !== '',
    [SettingLanguage.Ru]: drafts[SettingLanguage.Ru].value.value.trim() !== '',
    [SettingLanguage.Hy]: drafts[SettingLanguage.Hy].value.value.trim() !== '',
  }))

  /** The refusal sentence of each language whose last save failed. */
  const errors = computed<Partial<Record<SettingLanguage, string>>>(() => {
    const refused: Partial<Record<SettingLanguage, string>> = {}
    for (const locale of Object.values(SettingLanguage)) {
      const outcome = drafts[locale].outcome.value
      if (outcome !== null && !outcome.saved) {
        refused[locale] = outcome.error ?? t('errors.noAnswer')
      }
    }
    return refused
  })

  return { drafts, filled, errors }
}
