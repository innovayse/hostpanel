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

/** The languages a setting kept per language carries a tab for: the admin languages, the ones with a `.ru`/`.hy` key. */
export const SETTING_LOCALES: MessageLanguage[] = Object.values(MessageLanguage)

/** What {@link useLocalizedSettingDrafts} hands the card. */
export interface LocalizedSettingDrafts {
  /** The draft of each language's row. */
  drafts: Record<MessageLanguage, SettingDraft>
  /** Whether each language's row holds text; an unseeded row counts as empty. */
  filled: ComputedRef<Record<MessageLanguage, boolean>>
  /** The sentence of each language whose last save was refused; a language that saved or did not save is absent. */
  errors: ComputedRef<Partial<Record<MessageLanguage, string>>>
}

/**
 * Creates the drafts of a setting stored once per language.
 *
 * Written out per language rather than looped from the enum, so adding a language to
 * `MessageLanguage` fails to compile until the keys name its row.
 *
 * @param settings - All system settings, as loaded from the backend.
 * @param keys - The settings key of each language's row.
 * @returns The drafts and the per-language marks for the tabs.
 */
export const useLocalizedSettingDrafts = (
  settings: Ref<Setting[]>,
  keys: Record<MessageLanguage, string>,
): LocalizedSettingDrafts => {
  const { t } = useI18n()

  /** The draft of each language's row. */
  const drafts: Record<MessageLanguage, SettingDraft> = {
    [MessageLanguage.En]: useSettingDraft(settings, keys[MessageLanguage.En]),
    [MessageLanguage.Ru]: useSettingDraft(settings, keys[MessageLanguage.Ru]),
    [MessageLanguage.Hy]: useSettingDraft(settings, keys[MessageLanguage.Hy]),
  }

  /** Whether each language's row holds text. */
  const filled = computed<Record<MessageLanguage, boolean>>(() => ({
    [MessageLanguage.En]: drafts[MessageLanguage.En].value.value.trim() !== '',
    [MessageLanguage.Ru]: drafts[MessageLanguage.Ru].value.value.trim() !== '',
    [MessageLanguage.Hy]: drafts[MessageLanguage.Hy].value.value.trim() !== '',
  }))

  /** The refusal sentence of each language whose last save failed. */
  const errors = computed<Partial<Record<MessageLanguage, string>>>(() => {
    const refused: Partial<Record<MessageLanguage, string>> = {}
    for (const locale of Object.values(MessageLanguage)) {
      const outcome = drafts[locale].outcome.value
      if (outcome !== null && !outcome.saved) {
        refused[locale] = outcome.error ?? t('errors.noAnswer')
      }
    }
    return refused
  })

  return { drafts, filled, errors }
}
