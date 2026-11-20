<script setup lang="ts">
/**
 * Tracking & chat card: the tag-manager container and the live-chat server this storefront talks
 * to.
 *
 * Every value here is empty on a fresh install and nothing loads until it is set; the tag
 * additionally waits for the visitor to accept all cookies. The API checks the shapes (a `GTM-` id
 * id, an `https://` origin) and the page banner shows its refusal.
 */
import { computed, toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useLocalizedSettingDrafts, SETTING_LOCALES } from '@/composables/useLocalizedSettingDrafts'
import { useSettingDraft } from '@/composables/useSettingDraft'
import SettingNotSeeded from '@/components/settings/SettingNotSeeded.vue'
import SettingOutcome from '@/components/settings/SettingOutcome.vue'
import SettingsSection from '@/components/settings/SettingsSection.vue'
import SettingsSubsection from '@/components/settings/SettingsSubsection.vue'
import SettingsTextField from '@/components/settings/SettingsTextField.vue'
import UiField from '@/components/ui/UiField.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLocalizedField from '@/components/ui/UiLocalizedField.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { MessageLanguage } from '@/locales/messages'
import { ChatProvider } from '@/types/chatprovider'
import { SettingEnv, SettingKey } from '@/types/setting'
import type { Setting } from '@/types/setting'
import { ANALYTICS_ID_MAX_LENGTH, CHAT_SETTING_MAX_LENGTH } from '@/lib/limits'

const { t } = useI18n()

/** Props for TrackingChatCard. */
const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
}>()

/** The settings list as a ref, which is what the draft composable watches. */
const settings = toRef(props, 'settings')

/** Draft of the tag-manager id. */
const gtm = useSettingDraft(settings, SettingKey.AnalyticsGtmId)

/** Draft of the chat provider. */
const provider = useSettingDraft(settings, SettingKey.ChatProvider)

/** Options of the provider dropdown. */
const providerOptions = computed(() => [
  { value: ChatProvider.Off as string, label: t('settings.trackingChat.provider.off') },
  { value: ChatProvider.Chatwoot as string, label: t('settings.trackingChat.provider.chatwoot') },
  { value: ChatProvider.Innochat as string, label: t('settings.trackingChat.provider.innochat') },
])

/** The chat server address, a draft of the page's save bar. */
const baseUrl = useSettingDraft(settings, SettingKey.ChatBaseUrl)

/** The settings row of the website token of each language: the default token is the English tab's. */
const TOKEN_KEYS: Record<MessageLanguage, SettingKey> = {
  [MessageLanguage.En]: SettingKey.ChatWebsiteToken,
  [MessageLanguage.Ru]: SettingKey.ChatWebsiteTokenRu,
  [MessageLanguage.Hy]: SettingKey.ChatWebsiteTokenHy,
  [MessageLanguage.Es]: SettingKey.ChatWebsiteTokenEs,
}

/** The website token, one draft per language. */
const token = useLocalizedSettingDrafts(settings, TOKEN_KEYS)

/** Placeholder of the token input of each language: the default token is explained, the others are optional. */
const tokenPlaceholders = computed<Record<MessageLanguage, string>>(() => ({
  [MessageLanguage.En]: t('settings.trackingChat.chat.tokenHint'),
  [MessageLanguage.Ru]: t('settings.trackingChat.chat.tokenLocaleHint'),
  [MessageLanguage.Hy]: t('settings.trackingChat.chat.tokenLocaleHint'),
  [MessageLanguage.Es]: t('settings.trackingChat.chat.tokenLocaleHint'),
}))

/** Test id suffix of the token input of each language; the default token keeps the bare `token`. */
const tokenTestids: Record<MessageLanguage, string> = {
  [MessageLanguage.En]: 'tracking-chat-token',
  [MessageLanguage.Ru]: 'tracking-chat-token-ru',
  [MessageLanguage.Hy]: 'tracking-chat-token-hy',
  [MessageLanguage.Es]: 'tracking-chat-token-es',
}
</script>

<template>
  <SettingsSection
    :columns="1"
    :title="t('settings.trackingChat.title')"
    :description="t('settings.trackingChat.description')"
  >
    <!-- Tag manager -->
    <SettingsSubsection :title="t('settings.trackingChat.gtm.title')">
      <SettingsTextField
        :draft="gtm"
        :setting-key="SettingKey.AnalyticsGtmId"
        :env-name="SettingEnv[SettingKey.AnalyticsGtmId]"
        :label="t('settings.trackingChat.gtm.label')"
        :placeholder="t('shared.examples.tagManagerId')"
        :maxlength="ANALYTICS_ID_MAX_LENGTH"
        :hint="t('settings.trackingChat.gtm.hint')"
        testid="tracking-gtm"
      />
    </SettingsSubsection>

    <!-- Live chat -->
    <SettingsSubsection :title="t('settings.trackingChat.chat.title')">
      <UiStack v-if="provider.setting.value" gap="xs">
        <UiField :label="t('settings.trackingChat.provider.label')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="provider.value.value"
              :options="providerOptions"
              :placeholder="t('settings.trackingChat.provider.label')"
              data-testid="tracking-chat-provider"
            />
          </template>
        </UiField>
        <SettingOutcome :outcome="provider.outcome.value" />
      </UiStack>

      <SettingsTextField
        :draft="baseUrl"
        :setting-key="SettingKey.ChatBaseUrl"
        :env-name="SettingEnv[SettingKey.ChatBaseUrl]"
        :label="t('settings.trackingChat.chat.baseUrl')"
        :placeholder="t('settings.trackingChat.chat.baseUrlHint')"
        :maxlength="CHAT_SETTING_MAX_LENGTH"
        testid="tracking-chat-base-url"
      />

      <UiLocalizedField
        :label="t('settings.trackingChat.chat.token')"
        :locales="SETTING_LOCALES"
        :filled="token.filled.value"
        :errors="token.errors.value"
        testid="tracking-chat-token-tab"
      >
        <template #default="{ locale }">
          <SettingNotSeeded
            v-if="!token.drafts[locale].setting.value"
            :setting-key="TOKEN_KEYS[locale]"
            :env-name="SettingEnv[TOKEN_KEYS[locale]]"
          />
          <UiStack v-else gap="xs">
            <UiInput
              v-model="token.drafts[locale].value.value"
              :placeholder="tokenPlaceholders[locale]"
              :maxlength="CHAT_SETTING_MAX_LENGTH"
              :data-testid="tokenTestids[locale]"
            />
            <SettingOutcome :outcome="token.drafts[locale].outcome.value?.saved ? token.drafts[locale].outcome.value : null" />
          </UiStack>
        </template>
      </UiLocalizedField>
      <UiText tone="muted" size="sm" class="col-span-full">
        {{ t('settings.trackingChat.chat.hint') }}
      </UiText>
    </SettingsSubsection>
  </SettingsSection>
</template>
