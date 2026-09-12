<script setup lang="ts">
/**
 * Typography card — the heading and body typefaces the storefront uses.
 *
 * Two native selects over the four faces the storefront ships (all self-hosted; nothing
 * here or on the storefront loads from a font CDN), each option rendered in its own face,
 * and a three-language preview underneath so an operator sees Armenian, Russian and
 * English in the pair before saving. Empty means "the template's own", which is what every
 * install has until the operator chooses.
 */
import { toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSettingDraft } from '../composables/useSettingDraft'
import SettingSaveButton from './SettingSaveButton.vue'
import { BRAND_FONT_OPTIONS, previewStack } from '../../../utils/brandFonts'
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

const heading = useSettingDraft(settings, 'portal.brand.font_heading')
const body = useSettingDraft(settings, 'portal.brand.font_body')

/** The two selects, saved separately like every other control on the page. */
const FIELDS = [
  { key: 'portal.brand.font_heading', labelKey: 'settings.typography.heading', draft: heading },
  { key: 'portal.brand.font_body', labelKey: 'settings.typography.body', draft: body },
]

/** Sample lines in the three storefront languages. */
const SAMPLES = [
  'Ձեր ամբողջ թվային ենթակառուցվածքը',
  'Вся ваша цифровая инфраструктура',
  'Your whole digital infrastructure',
]
</script>

<template>
  <section class="bg-surface-card border border-border rounded-2xl p-6 mb-6">
    <h2 class="font-display text-lg font-bold text-text-primary">{{ t('settings.typography.title') }}</h2>
    <p class="text-sm text-text-secondary mt-1">{{ t('settings.typography.description') }}</p>

    <div class="mt-5 grid gap-4 sm:grid-cols-2">
      <div v-for="field in FIELDS" :key="field.key">
        <div
          v-if="!field.draft.setting.value"
          class="rounded-xl border border-status-yellow/30 bg-status-yellow/10 p-4 text-sm text-status-yellow"
          v-html="t('settings.portalAppearance.notSeeded', { key: field.key, env: 'NUXT_PUBLIC_PORTAL_BRAND_FONT_HEADING' })"
        />
        <template v-else>
          <label class="block text-sm font-medium text-text-secondary mb-1.5">{{ t(field.labelKey) }}</label>
          <div class="flex items-center gap-3">
            <select
              v-model="field.draft.value.value"
              class="min-w-[200px] flex-1 rounded-xl border border-border bg-surface-elevated px-3 py-2 text-sm text-text-primary focus:border-text-secondary focus:outline-none"
              :style="{ fontFamily: previewStack(field.draft.value.value) || undefined }"
            >
              <option value="">{{ t('settings.typography.templateDefault') }}</option>
              <option
                v-for="option in BRAND_FONT_OPTIONS"
                :key="option.value"
                :value="option.value"
                :style="{ fontFamily: option.stack }"
              >
                {{ t(option.labelKey) }}
              </option>
            </select>
            <SettingSaveButton
              :dirty="field.draft.dirty.value"
              :saving="saving"
              @click="field.draft.submit((id, value) => emit('save', id, value))"
            />
          </div>
        </template>
      </div>
    </div>

    <!-- The pair as the storefront will set it: a heading in the heading face, a line in the body face. -->
    <div class="mt-5 rounded-xl border border-border bg-surface-elevated p-4">
      <p
        v-for="sample in SAMPLES"
        :key="sample"
        class="mb-2 last:mb-0"
      >
        <span class="block text-lg font-bold text-text-primary" :style="{ fontFamily: previewStack(heading.value.value) || undefined }">{{ sample }}</span>
        <span class="block text-sm text-text-secondary" :style="{ fontFamily: previewStack(body.value.value) || undefined }">{{ t('settings.typography.sampleBody') }}</span>
      </p>
    </div>
  </section>
</template>
