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
import { useSettingDraft } from '@/composables/useSettingDraft'
import SettingNotSeeded from '@/components/settings/SettingNotSeeded.vue'
import SettingOutcome from '@/components/settings/SettingOutcome.vue'
import SettingSaveButton from '@/components/settings/SettingSaveButton.vue'
import SettingsSection from '@/components/settings/SettingsSection.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { ChatProvider } from '@/types/chatprovider'
import { SettingEnv, SettingKey } from '@/types/setting'
import type { Setting, SettingSave } from '@/types/setting'
import { ANALYTICS_ID_MAX_LENGTH, CHAT_SETTING_MAX_LENGTH } from '@/lib/limits'

const { t } = useI18n()

/** Props for TrackingChatCard. */
const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
  /** Persists one row and answers how it went; each control shows its own outcome. */
  save: SettingSave
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

/** The text fields, each saved on its own. */
const TEXT_FIELDS = [
  { key: SettingKey.ChatBaseUrl, testid: 'base-url', labelKey: 'settings.trackingChat.chat.baseUrl', hintKey: 'settings.trackingChat.chat.baseUrlHint', draft: useSettingDraft(settings, SettingKey.ChatBaseUrl) },
  { key: SettingKey.ChatWebsiteToken, testid: 'token', labelKey: 'settings.trackingChat.chat.token', hintKey: 'settings.trackingChat.chat.tokenHint', draft: useSettingDraft(settings, SettingKey.ChatWebsiteToken) },
  { key: SettingKey.ChatWebsiteTokenRu, testid: 'token-ru', labelKey: 'settings.trackingChat.chat.tokenRu', hintKey: 'settings.trackingChat.chat.tokenLocaleHint', draft: useSettingDraft(settings, SettingKey.ChatWebsiteTokenRu) },
  { key: SettingKey.ChatWebsiteTokenHy, testid: 'token-hy', labelKey: 'settings.trackingChat.chat.tokenHy', hintKey: 'settings.trackingChat.chat.tokenLocaleHint', draft: useSettingDraft(settings, SettingKey.ChatWebsiteTokenHy) },
]
</script>

<template>
  <SettingsSection
    :title="t('settings.trackingChat.title')"
    :description="t('settings.trackingChat.description')"
  >
    <!-- Tag manager -->
    <UiStack gap="xs">
      <SettingNotSeeded v-if="!gtm.setting.value" :setting-key="SettingKey.AnalyticsGtmId" :env-name="SettingEnv[SettingKey.AnalyticsGtmId]" />
      <UiStack v-else direction="row" align="end" wrap>
        <UiInput
          v-model="gtm.value.value"
          :label="t('settings.trackingChat.gtm.label')"
          :placeholder="t('shared.examples.tagManagerId')"
          :maxlength="ANALYTICS_ID_MAX_LENGTH"
          data-testid="tracking-gtm"
          class="min-w-64 flex-1"
          @keyup.enter="gtm.submit(save)"
        />
        <SettingSaveButton
          :dirty="gtm.dirty.value"
          :saving="gtm.saving.value"
          data-testid="tracking-gtm-save"
          @click="gtm.submit(save)"
        />
        <SettingOutcome :outcome="gtm.outcome.value" />
      </UiStack>
      <UiText tone="muted" size="sm">
        {{ t('settings.trackingChat.gtm.hint') }}
      </UiText>
    </UiStack>

    <!-- Live chat -->
    <UiStack gap="sm" class="border-t border-border pt-4">
      <UiHeading :level="3" variant="card">
        {{ t('settings.trackingChat.chat.title') }}
      </UiHeading>

      <UiStack v-if="provider.setting.value" direction="row" align="end" wrap>
        <UiText tone="secondary" size="md">
          {{ t('settings.trackingChat.provider.label') }}
        </UiText>
        <UiSelect
          v-model="provider.value.value"
          :options="providerOptions"
          :placeholder="t('settings.trackingChat.provider.label')"
          data-testid="tracking-chat-provider"
          class="min-w-56"
        />
        <SettingSaveButton
          :dirty="provider.dirty.value"
          :saving="provider.saving.value"
          data-testid="tracking-chat-provider-save"
          @click="provider.submit(save)"
        />
        <SettingOutcome :outcome="provider.outcome.value" />
      </UiStack>

      <UiGrid :columns="2">
        <template v-for="field in TEXT_FIELDS" :key="field.key">
          <SettingNotSeeded v-if="!field.draft.setting.value" :setting-key="field.key" :env-name="SettingEnv[field.key]" />
          <UiStack v-else direction="row" align="end">
            <!-- The label makes the input sit in a wrapper, so the growing classes belong on this box. -->
            <div class="min-w-0 flex-1">
              <UiInput
                v-model="field.draft.value.value"
                :label="t(field.labelKey)"
                :placeholder="t(field.hintKey)"
                :maxlength="CHAT_SETTING_MAX_LENGTH"
                :data-testid="`tracking-chat-${field.testid}`"
                @keyup.enter="field.draft.submit(save)"
              />
            </div>
            <SettingSaveButton
              :dirty="field.draft.dirty.value"
              :saving="field.draft.saving.value"
              :data-testid="`tracking-chat-${field.testid}-save`"
              @click="field.draft.submit(save)"
            />
            <SettingOutcome :outcome="field.draft.outcome.value" />
          </UiStack>
        </template>
      </UiGrid>
      <UiText tone="muted" size="sm">
        {{ t('settings.trackingChat.chat.hint') }}
      </UiText>
    </UiStack>
  </SettingsSection>
</template>
