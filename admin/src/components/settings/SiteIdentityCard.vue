<script setup lang="ts">
/**
 * Site identity card: the name the storefront calls itself and a one-line tagline.
 *
 * The name reaches the browser title's `og:site_name`, every logo's alt text and the structured
 * data; the tagline is the default meta description. Both fields are drafts of the page's save bar.
 */
import { toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSettingDraft } from '@/composables/useSettingDraft'
import type { SettingDraft } from '@/composables/useSettingDraft'
import SettingNotSeeded from '@/components/settings/SettingNotSeeded.vue'
import SettingOutcome from '@/components/settings/SettingOutcome.vue'
import SettingsSection from '@/components/settings/SettingsSection.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { SettingEnv, SettingKey } from '@/types/setting'
import type { Setting } from '@/types/setting'
import { SHORT_TEXT_MAX_LENGTH } from '@/lib/limits'

const { t } = useI18n()

/** Props for SiteIdentityCard. */
const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
}>()

/** The settings list as a ref, which is what the draft composable watches. */
const settings = toRef(props, 'settings')

/** One identity field of the card. */
interface IdentityField {
  /** Settings key the field edits. */
  key: SettingKey
  /** Environment variable the portal falls back to until the row exists. */
  env: string
  /** Suffix of the field's test ids. */
  testid: string
  /** Locale key of the label. */
  labelKey: string
  /** Locale key of the placeholder. */
  hintKey: string
  /** The field's draft state. */
  draft: SettingDraft
}

/** The two identity fields, each with its own draft. */
const FIELDS: IdentityField[] = [
  {
    key: SettingKey.SiteName,
    env: SettingEnv[SettingKey.SiteName],
    testid: 'name',
    labelKey: 'settings.siteIdentity.name.label',
    hintKey: 'settings.siteIdentity.name.hint',
    draft: useSettingDraft(settings, SettingKey.SiteName),
  },
  {
    key: SettingKey.SiteTagline,
    env: SettingEnv[SettingKey.SiteTagline],
    testid: 'tagline',
    labelKey: 'settings.siteIdentity.tagline.label',
    hintKey: 'settings.siteIdentity.tagline.hint',
    draft: useSettingDraft(settings, SettingKey.SiteTagline),
  },
]
</script>

<template>
  <SettingsSection
    :columns="2"
    :title="t('settings.siteIdentity.title')"
    :description="t('settings.siteIdentity.description')"
  >
    <template v-for="field in FIELDS" :key="field.key">
      <SettingNotSeeded v-if="!field.draft.setting.value" :setting-key="field.key" :env-name="field.env" />
      <UiStack v-else direction="row" align="end" wrap>
        <UiInput
          v-model="field.draft.value.value"
          :label="t(field.labelKey)"
          :placeholder="t(field.hintKey)"
          :maxlength="SHORT_TEXT_MAX_LENGTH"
          :data-testid="`site-identity-${field.testid}`"
          class="min-w-72 flex-1"
        />
        <SettingOutcome :outcome="field.draft.outcome.value" />
      </UiStack>
    </template>
  </SettingsSection>
</template>
