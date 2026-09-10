<script setup lang="ts">
/**
 * Portal appearance card — the storefront template, the colour mode a visitor
 * starts in, and whether visitors may switch it.
 *
 * Constrained controls rather than the free-text fields the settings table
 * offers. The API refuses a value outside each key's vocabulary, and the portal
 * falls back to its default on anything it does not recognise, so a typo would
 * not break the site — but it would silently revert an operator's choice with no
 * indication why. A dropdown and a radio group remove the failure entirely.
 */
import { computed, toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSettingDraft } from '../composables/useSettingDraft'
import SettingSaveButton from './SettingSaveButton.vue'
import UiToggleSwitch from '../../../components/ui/UiToggleSwitch.vue'
import type { Setting } from '../../../types/setting'

const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
  /** True while a save is in flight. */
  saving?: boolean
}>()

const emit = defineEmits<{ save: [id: number, value: string] }>()

const { t } = useI18n()

/** Templates the portal ships. Mirrors client/templates/types.ts and PortalSettingKeys.TemplateNames. */
const TEMPLATES = [
  { value: 'aurora', labelKey: 'settings.portalAppearance.templates.aurora.label', hintKey: 'settings.portalAppearance.templates.aurora.hint' },
  { value: 'nova', labelKey: 'settings.portalAppearance.templates.nova.label', hintKey: 'settings.portalAppearance.templates.nova.hint' },
  { value: 'classic', labelKey: 'settings.portalAppearance.templates.classic.label', hintKey: 'settings.portalAppearance.templates.classic.hint' },
]

/** Colour modes an operator can set as the default. Mirrors PortalSettingKeys.ThemeModes. */
const THEME_MODES = [
  { value: 'light', labelKey: 'settings.portalAppearance.themeModes.light' },
  { value: 'dark', labelKey: 'settings.portalAppearance.themeModes.dark' },
  { value: 'system', labelKey: 'settings.portalAppearance.themeModes.system' },
]

const TEMPLATE_KEY = 'portal.template'
const THEME_KEY = 'portal.theme.default'
const TOGGLE_KEY = 'portal.theme.user_toggle'

const settings = toRef(props, 'settings')

const templateDraft = useSettingDraft(settings, TEMPLATE_KEY, 'aurora')
const themeDraft = useSettingDraft(settings, THEME_KEY, 'dark')
const toggleDraft = useSettingDraft(settings, TOGGLE_KEY, 'true')

/** The switch's view of the boolean setting: only the literal `false` is off. */
const toggleEnabled = computed({
  get: () => toggleDraft.value.value !== 'false',
  set: (on: boolean) => { toggleDraft.value.value = on ? 'true' : 'false' },
})

/** Keys the seeder has not created yet, so the operator knows why a control is missing. */
const missingKeys = computed(() => [
  [TEMPLATE_KEY, templateDraft.setting.value],
  [THEME_KEY, themeDraft.setting.value],
  [TOGGLE_KEY, toggleDraft.setting.value],
].filter(([, row]) => !row).map(([key]) => key as string))
</script>

<template>
  <section class="bg-surface-card border border-border rounded-2xl p-6 mb-6">
    <h2 class="font-display text-lg font-bold text-text-primary">{{ t('settings.portalAppearance.title') }}</h2>
    <p class="text-sm text-text-secondary mt-1">
      {{ t('settings.portalAppearance.description') }}
    </p>

    <!--
      The backend has no create endpoint for settings, so an unseeded key cannot
      be added from the admin panel. Say so rather than showing a control that
      silently does nothing. Seeding runs on API start, so this is only ever an
      API older than this panel.
    -->
    <div
      v-for="key in missingKeys"
      :key="key"
      class="mt-4 rounded-xl border border-status-yellow/30 bg-status-yellow/10 p-4 text-sm text-status-yellow"
      v-html="t('settings.portalAppearance.notSeeded', { key, env: 'NUXT_PUBLIC_PORTAL_TEMPLATE' })"
    />

    <!-- Template -->
    <div v-if="templateDraft.setting.value" class="mt-5 flex flex-wrap items-end gap-4">
      <label class="flex flex-col gap-1.5">
        <span class="text-sm font-medium text-text-secondary">{{ t('settings.portalAppearance.template') }}</span>
        <select
          v-model="templateDraft.value.value"
          class="min-w-[220px] rounded-xl border border-border bg-surface-elevated px-3 py-2 text-sm text-text-primary focus:border-text-secondary focus:outline-none"
        >
          <option v-for="tpl in TEMPLATES" :key="tpl.value" :value="tpl.value">
            {{ t(tpl.labelKey) }}
          </option>
        </select>
      </label>

      <SettingSaveButton
        :dirty="templateDraft.dirty.value"
        :saving="saving"
        @click="templateDraft.submit((id, value) => emit('save', id, value))"
      />

      <p class="text-sm text-text-muted">
        {{ t(TEMPLATES.find(tpl => tpl.value === templateDraft.value.value)?.hintKey ?? '') }}
      </p>
    </div>

    <!-- Colour mode -->
    <div v-if="themeDraft.setting.value" class="mt-6 border-t border-border pt-5">
      <h3 class="text-sm font-medium text-text-secondary">{{ t('settings.portalAppearance.themeDefault') }}</h3>
      <p class="text-xs text-text-muted mt-0.5">{{ t('settings.portalAppearance.themeDefaultHint') }}</p>

      <div class="mt-3 flex flex-wrap items-center gap-4">
        <div class="flex flex-wrap gap-2">
          <label
            v-for="mode in THEME_MODES"
            :key="mode.value"
            class="flex cursor-pointer items-center gap-2 rounded-xl border px-3 py-2 text-sm transition-colors"
            :class="themeDraft.value.value === mode.value
              ? 'border-text-secondary bg-surface-elevated text-text-primary'
              : 'border-border text-text-secondary hover:border-text-secondary/50'"
          >
            <input
              v-model="themeDraft.value.value"
              type="radio"
              name="portal-theme-default"
              :value="mode.value"
              class="accent-status-green"
            >
            {{ t(mode.labelKey) }}
          </label>
        </div>

        <SettingSaveButton
          :dirty="themeDraft.dirty.value"
          :saving="saving"
          @click="themeDraft.submit((id, value) => emit('save', id, value))"
        />
      </div>
    </div>

    <!-- Visitor toggle -->
    <div v-if="toggleDraft.setting.value" class="mt-5 flex flex-wrap items-center gap-4">
      <div class="flex items-center gap-3 text-sm text-text-primary">
        <UiToggleSwitch v-model="toggleEnabled" />
        <span>
          {{ t('settings.portalAppearance.userToggle') }}
          <span class="block text-xs text-text-muted">{{ t('settings.portalAppearance.userToggleHint') }}</span>
        </span>
      </div>

      <SettingSaveButton
        :dirty="toggleDraft.dirty.value"
        :saving="saving"
        @click="toggleDraft.submit((id, value) => emit('save', id, value))"
      />
    </div>
  </section>
</template>
