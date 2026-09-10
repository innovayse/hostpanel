<script setup lang="ts">
/**
 * Site identity card — the name the storefront calls itself and a one-line
 * tagline.
 *
 * The name reaches the browser title's `og:site_name`, every logo's alt text and
 * the structured data; the tagline is the default meta description. Both used to
 * be the vendor's own name written into source, which a self-hosted operator
 * could not change from the panel.
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

/** The two identity fields, each saved on its own so a mistake in one cannot ride along with the other. */
const FIELDS = [
  { key: 'portal.site.name', labelKey: 'settings.siteIdentity.name.label', hintKey: 'settings.siteIdentity.name.hint', draft: useSettingDraft(settings, 'portal.site.name') },
  { key: 'portal.site.tagline', labelKey: 'settings.siteIdentity.tagline.label', hintKey: 'settings.siteIdentity.tagline.hint', draft: useSettingDraft(settings, 'portal.site.tagline') },
]
</script>

<template>
  <section class="bg-surface-card border border-border rounded-2xl p-6 mb-6">
    <h2 class="font-display text-lg font-bold text-text-primary">{{ t('settings.siteIdentity.title') }}</h2>
    <p class="text-sm text-text-secondary mt-1">{{ t('settings.siteIdentity.description') }}</p>

    <div class="mt-5 flex flex-col gap-5">
      <div v-for="field in FIELDS" :key="field.key">
        <!-- An API older than this panel has not seeded the row; say so rather than render a dead field. -->
        <div
          v-if="!field.draft.setting.value"
          class="rounded-xl border border-status-yellow/30 bg-status-yellow/10 p-4 text-sm text-status-yellow"
          v-html="t('settings.portalAppearance.notSeeded', { key: field.key, env: 'NUXT_PUBLIC_PORTAL_SITE_NAME' })"
        />
        <div v-else class="flex flex-wrap items-end gap-4">
          <div class="min-w-[280px] flex-1">
            <UiTextField
              v-model="field.draft.value.value"
              :label="t(field.labelKey)"
              :placeholder="t(field.hintKey)"
              :maxlength="200"
              @keyup.enter="field.draft.submit((id, value) => emit('save', id, value))"
            />
          </div>
          <SettingSaveButton
            :dirty="field.draft.dirty.value"
            :saving="saving"
            @click="field.draft.submit((id, value) => emit('save', id, value))"
          />
        </div>
      </div>
    </div>
  </section>
</template>
