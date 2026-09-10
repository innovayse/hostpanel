<script setup lang="ts">
/**
 * Tracking & chat card — the tag-manager container and the live-chat server this
 * storefront talks to.
 *
 * Every value here is empty on a fresh install and nothing loads until it is set; the tag
 * additionally waits for the visitor to accept all cookies. The API checks the shapes (a
 * `GTM-…` id, an `https://` origin) and the page banner shows its refusal.
 */
import { toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSettingDraft } from '../composables/useSettingDraft'
import SettingSaveButton from './SettingSaveButton.vue'
import UiTextField from '../../../components/ui/UiTextField.vue'
import type { Setting } from '../../../types/setting'

const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
  /** True while a save is in flight. */
  saving?: boolean
}>()

const emit = defineEmits<{ save: [id: number, value: string] }>()

const { t } = useI18n()

const settings = toRef(props, 'settings')

/** Chat providers the storefront's loader understands. Mirrors plugins/live-chat.ts. */
const PROVIDERS = [
  { value: '', labelKey: 'settings.trackingChat.provider.off' },
  { value: 'chatwoot', labelKey: 'settings.trackingChat.provider.chatwoot' },
  { value: 'innochat', labelKey: 'settings.trackingChat.provider.innochat' },
]

const gtm = useSettingDraft(settings, 'portal.analytics.gtm_id')
const provider = useSettingDraft(settings, 'portal.chat.provider')

/** The text fields, each saved on its own. */
const TEXT_FIELDS = [
  { key: 'portal.chat.base_url', labelKey: 'settings.trackingChat.chat.baseUrl', hintKey: 'settings.trackingChat.chat.baseUrlHint', draft: useSettingDraft(settings, 'portal.chat.base_url') },
  { key: 'portal.chat.website_token', labelKey: 'settings.trackingChat.chat.token', hintKey: 'settings.trackingChat.chat.tokenHint', draft: useSettingDraft(settings, 'portal.chat.website_token') },
  { key: 'portal.chat.website_token.ru', labelKey: 'settings.trackingChat.chat.tokenRu', hintKey: 'settings.trackingChat.chat.tokenLocaleHint', draft: useSettingDraft(settings, 'portal.chat.website_token.ru') },
  { key: 'portal.chat.website_token.hy', labelKey: 'settings.trackingChat.chat.tokenHy', hintKey: 'settings.trackingChat.chat.tokenLocaleHint', draft: useSettingDraft(settings, 'portal.chat.website_token.hy') },
]
</script>

<template>
  <section class="bg-surface-card border border-border rounded-2xl p-6 mb-6">
    <h2 class="font-display text-lg font-bold text-text-primary">{{ t('settings.trackingChat.title') }}</h2>
    <p class="text-sm text-text-secondary mt-1">{{ t('settings.trackingChat.description') }}</p>

    <!-- Tag manager -->
    <div class="mt-5">
      <div
        v-if="!gtm.setting.value"
        class="rounded-xl border border-status-yellow/30 bg-status-yellow/10 p-4 text-sm text-status-yellow"
        v-html="t('settings.portalAppearance.notSeeded', { key: 'portal.analytics.gtm_id', env: 'NUXT_PUBLIC_PORTAL_GTM_ID' })"
      />
      <div v-else class="flex flex-wrap items-end gap-4">
        <div class="min-w-[260px] flex-1">
          <UiTextField
            v-model="gtm.value.value"
            :label="t('settings.trackingChat.gtm.label')"
            placeholder="GTM-XXXXXXX"
            :maxlength="20"
            @keyup.enter="gtm.submit((id, value) => emit('save', id, value))"
          />
        </div>
        <SettingSaveButton :dirty="gtm.dirty.value" :saving="saving" @click="gtm.submit((id, value) => emit('save', id, value))" />
      </div>
      <p class="mt-1 text-xs text-text-muted">{{ t('settings.trackingChat.gtm.hint') }}</p>
    </div>

    <!-- Live chat -->
    <div class="mt-6 border-t border-border pt-5">
      <h3 class="text-sm font-medium text-text-secondary">{{ t('settings.trackingChat.chat.title') }}</h3>

      <div v-if="provider.setting.value" class="mt-3 flex flex-wrap items-center gap-4">
        <label class="flex flex-col gap-1.5">
          <span class="text-sm font-medium text-text-secondary">{{ t('settings.trackingChat.provider.label') }}</span>
          <select
            v-model="provider.value.value"
            class="min-w-[220px] rounded-xl border border-border bg-surface-elevated px-3 py-2 text-sm text-text-primary focus:border-text-secondary focus:outline-none"
          >
            <option v-for="p in PROVIDERS" :key="p.value" :value="p.value">{{ t(p.labelKey) }}</option>
          </select>
        </label>
        <SettingSaveButton :dirty="provider.dirty.value" :saving="saving" @click="provider.submit((id, value) => emit('save', id, value))" />
      </div>

      <div class="mt-4 grid gap-4 sm:grid-cols-2">
        <div v-for="field in TEXT_FIELDS" :key="field.key">
          <div
            v-if="!field.draft.setting.value"
            class="rounded-xl border border-status-yellow/30 bg-status-yellow/10 p-4 text-sm text-status-yellow"
            v-html="t('settings.portalAppearance.notSeeded', { key: field.key, env: 'NUXT_PUBLIC_PORTAL_CHAT_BASE_URL' })"
          />
          <div v-else class="flex items-end gap-3">
            <div class="min-w-0 flex-1">
              <UiTextField
                v-model="field.draft.value.value"
                :label="t(field.labelKey)"
                :placeholder="t(field.hintKey)"
                :maxlength="300"
                @keyup.enter="field.draft.submit((id, value) => emit('save', id, value))"
              />
            </div>
            <SettingSaveButton :dirty="field.draft.dirty.value" :saving="saving" @click="field.draft.submit((id, value) => emit('save', id, value))" />
          </div>
        </div>
      </div>
      <p class="mt-2 text-xs text-text-muted">{{ t('settings.trackingChat.chat.hint') }}</p>
    </div>
  </section>
</template>
