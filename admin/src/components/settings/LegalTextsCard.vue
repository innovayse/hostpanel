<script setup lang="ts">
/**
 * Legal texts card: the operator's own terms of service, privacy policy, refund policy, acceptable use policy and cookie
 * policy, one text per page and language.
 *
 * Every page is one `UiLocalizedField` (a tab per language) over a multi-line field; the drafts are the page's save bar's
 * to save. A page with no text in a language shows the English text there, and a page with no English text either keeps the
 * built-in wording the storefront ships with, so leaving everything empty changes nothing. The text is plain: a `## ` line
 * starts a section, a blank line a paragraph and a `- ` line a list entry; the storefront never renders it as markup.
 */
import { toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useLocalizedSettingDrafts } from '@/composables/useLocalizedSettingDrafts'
import type { LocalizedSettingDrafts } from '@/composables/useLocalizedSettingDrafts'
import SettingNotSeeded from '@/components/settings/SettingNotSeeded.vue'
import SettingOutcome from '@/components/settings/SettingOutcome.vue'
import SettingsSection from '@/components/settings/SettingsSection.vue'
import UiLocalizedField from '@/components/ui/UiLocalizedField.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { LEGAL_TEXT_MAX_LENGTH } from '@/lib/limits'
import { ContentLanguage } from '@/locales/contentLanguage'
import { SettingKey } from '@/types/setting'
import type { Setting } from '@/types/setting'
import { UiGap } from '@/utils/ui/layout'

const { t } = useI18n()

/** Props for LegalTextsCard. */
const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
}>()

/** The settings list as a ref, which is what the draft composable watches. */
const settings = toRef(props, 'settings')

/** Visible lines of each text field; the field grows by dragging. */
const TEXT_ROWS = 10

/** One legal page's text, written once per language. */
interface LegalDocumentField {
  /** Suffix of the test ids: `legal-<testid>` is the English field, `legal-<testid>-<language code>` the others. */
  testid: string
  /** Locale key of the page's caption. */
  labelKey: string
  /** The drafts of the rows and what the tabs mark. */
  localized: LocalizedSettingDrafts
}

/**
 * Builds the field of one page.
 *
 * @param testid - Suffix of the test ids.
 * @param labelKey - Locale key of the page's caption.
 * @param key - The settings key of the page's default-language row; the other languages' rows are named from it.
 * @returns The field with its drafts.
 */
const documentField = (testid: string, labelKey: string, key: SettingKey): LegalDocumentField => ({
  testid,
  labelKey,
  localized: useLocalizedSettingDrafts(settings, key),
})

/** The five legal pages, in the order the card draws them. */
const DOCUMENTS: LegalDocumentField[] = [
  documentField('terms', 'settings.legalTexts.terms', SettingKey.LegalTerms),
  documentField('privacy', 'settings.legalTexts.privacy', SettingKey.LegalPrivacy),
  documentField('refund', 'settings.legalTexts.refund', SettingKey.LegalRefund),
  documentField('acceptable-use', 'settings.legalTexts.acceptableUse', SettingKey.LegalAcceptableUse),
  documentField('cookies', 'settings.legalTexts.cookies', SettingKey.LegalCookies),
]

/**
 * Test id of the field of one language of a page; English keeps the bare suffix.
 *
 * @param field - The page's field.
 * @param locale - The language of the field.
 * @returns The `data-testid` of the text field.
 */
const inputTestid = (field: LegalDocumentField, locale: ContentLanguage): string =>
  locale === ContentLanguage.En ? `legal-${field.testid}` : `legal-${field.testid}-${locale}`
</script>

<template>
  <SettingsSection
    :columns="1"
    :title="t('settings.legalTexts.title')"
    :description="t('settings.legalTexts.description')"
  >
    <UiLocalizedField
      v-for="field in DOCUMENTS"
      :key="field.testid"
      :label="t(field.labelKey)"
      :hint="t('settings.legalTexts.hint')"
      :filled="field.localized.filled.value"
      :errors="field.localized.errors.value"
      :testid="`legal-${field.testid}-tab`"
    >
      <template #default="{ locale }">
        <SettingNotSeeded
          v-if="!field.localized.drafts[locale].setting.value"
          :setting-key="field.localized.keys[locale]"
          :env-name="field.localized.envs[locale]"
        />
        <UiStack v-else :gap="UiGap.Xs">
          <UiTextarea
            v-model="field.localized.drafts[locale].value.value"
            :placeholder="t('settings.legalTexts.placeholder')"
            :rows="TEXT_ROWS"
            :maxlength="LEGAL_TEXT_MAX_LENGTH"
            :data-testid="inputTestid(field, locale)"
          />
          <SettingOutcome :outcome="field.localized.drafts[locale].outcome.value?.saved ? field.localized.drafts[locale].outcome.value : null" />
        </UiStack>
      </template>
    </UiLocalizedField>
  </SettingsSection>
</template>
