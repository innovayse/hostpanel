/**
 * The drafts of one setting that is stored once per language (`portal.contact.address`, `.ru`, `.hy`,
 * `.de`, `.es`), with what the `UiLocalizedField` tabs need to mark them.
 *
 * The rows are named from the setting's default key and the shared language list (`STOREFRONT_LOCALES`), so a
 * language added to `ContentLanguage` gets its tab and its row here without a line changed.
 */

import { computed } from 'vue'
import type { ComputedRef, Ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSettingDraft } from '@/composables/useSettingDraft'
import type { SettingDraft } from '@/composables/useSettingDraft'
import { perLocale, STOREFRONT_LOCALES } from '@/lib/storefrontTranslations'
import { ContentLanguage } from '@/locales/contentLanguage'
import { SettingEnv } from '@/types/setting'
import type { Setting, SettingKey } from '@/types/setting'

/** What {@link useLocalizedSettingDrafts} hands the card. */
export interface LocalizedSettingDrafts {
  /** The draft of each language's row. */
  drafts: Record<ContentLanguage, SettingDraft>
  /** The settings key of each language's row; the default language keeps the plain key. */
  keys: Record<ContentLanguage, string>
  /** The environment variable each language's row falls back to, shown while the row is missing. */
  envs: Record<ContentLanguage, string>
  /** Whether each language's row holds text; an unseeded row counts as empty. */
  filled: ComputedRef<Record<ContentLanguage, boolean>>
  /** The sentence of each language whose last save was refused; a language that saved or did not save is absent. */
  errors: ComputedRef<Partial<Record<ContentLanguage, string>>>
}

/**
 * Names the settings row of one language.
 *
 * @param key - The default-language key, e.g. `portal.contact.address`.
 * @param locale - The language whose row is wanted.
 * @returns The key itself for English, else the key with a `.<language>` suffix (`portal.contact.address.de`).
 */
export const localizedSettingKey = (key: string, locale: ContentLanguage): string =>
  locale === ContentLanguage.En ? key : `${key}.${locale}`

/**
 * Names the environment variable of one language's row.
 *
 * @param env - The variable of the default-language row, e.g. `NUXT_PUBLIC_PORTAL_CONTACT_ADDRESS`.
 * @param locale - The language whose variable is wanted.
 * @returns The variable itself for English, else the variable with the upper-case language appended
 *   (`NUXT_PUBLIC_PORTAL_CONTACT_ADDRESS_DE`).
 */
export const localizedSettingEnv = (env: string, locale: ContentLanguage): string =>
  locale === ContentLanguage.En ? env : `${env}_${locale.toUpperCase()}`

/**
 * Creates the drafts of a setting stored once per language.
 *
 * One draft per language of the shared list, so the tabs show every language the platform supports.
 *
 * @param settings - All system settings, as loaded from the backend.
 * @param key - The settings key of the default-language row; the other languages' rows are named from it.
 * @returns The drafts, the row keys and the per-language marks for the tabs.
 */
export const useLocalizedSettingDrafts = (settings: Ref<Setting[]>, key: SettingKey): LocalizedSettingDrafts => {
  const { t } = useI18n()

  /** The settings key of each language's row. */
  const keys = perLocale((locale) => localizedSettingKey(key, locale))

  /** The environment variable of each language's row. */
  const envs = perLocale((locale) => localizedSettingEnv(SettingEnv[key], locale))

  /** The draft of each language's row. */
  const drafts = perLocale((locale) => useSettingDraft(settings, keys[locale]))

  /** Whether each language's row holds text. */
  const filled = computed<Record<ContentLanguage, boolean>>(() =>
    perLocale((locale) => drafts[locale].value.value.trim() !== ''))

  /** The refusal sentence of each language whose last save failed. */
  const errors = computed<Partial<Record<ContentLanguage, string>>>(() => {
    const refused: Partial<Record<ContentLanguage, string>> = {}
    for (const locale of STOREFRONT_LOCALES) {
      const outcome = drafts[locale].outcome.value
      if (outcome !== null && !outcome.saved) {
        refused[locale] = outcome.error ?? t('errors.noAnswer')
      }
    }
    return refused
  })

  return { drafts, keys, envs, filled, errors }
}
