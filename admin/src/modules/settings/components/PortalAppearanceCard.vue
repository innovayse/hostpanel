<script setup lang="ts">
/**
 * Portal appearance card — picks the storefront template.
 *
 * A dropdown rather than the free-text field the settings table offers. The
 * portal falls back to its default on an unrecognised value, so a typo would
 * not break the site, but it would silently revert an operator's choice with no
 * indication why. Constraining the input removes the failure entirely.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { Setting } from '../../../types/models'

const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
  /** True while a save is in flight. */
  saving?: boolean
}>()

const emit = defineEmits<{ save: [id: number, value: string] }>()

const { t } = useI18n()

/** Templates the portal ships. Mirrors client/templates/types.ts. */
const TEMPLATES = [
  { value: 'aurora', labelKey: 'settings.portalAppearance.templates.aurora.label', hintKey: 'settings.portalAppearance.templates.aurora.hint' },
  { value: 'classic', labelKey: 'settings.portalAppearance.templates.classic.label', hintKey: 'settings.portalAppearance.templates.classic.hint' },
]

const KEY = 'portal.template'

const setting = computed(() => props.settings.find(s => s.key === KEY))
const draft = ref<string | null>(null)

/** The value shown in the dropdown: the operator's unsaved pick, or what is stored. */
const selected = computed({
  get: () => draft.value ?? setting.value?.value ?? 'aurora',
  set: (value: string) => { draft.value = value },
})

const dirty = computed(() => draft.value !== null && draft.value !== setting.value?.value)

/** Saves the pick. No-op when the setting row does not exist. */
function save() {
  if (!setting.value || !dirty.value) return
  emit('save', setting.value.id, selected.value)
}

// The draft is cleared when the stored value catches up, not when the save is
// requested. Clearing on emit discarded the operator's pick before anyone knew
// whether it had been written: a failed save silently snapped the dropdown back
// to the template already in use, with only an error banner to explain it.
watch(() => setting.value?.value, (stored) => {
  if (draft.value !== null && stored === draft.value) {
    draft.value = null
  }
})
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
      silently does nothing.
    -->
    <div
      v-if="!setting"
      class="mt-4 rounded-xl border border-status-yellow/30 bg-status-yellow/10 p-4 text-sm text-status-yellow"
      v-html="t('settings.portalAppearance.notSeeded', { key: KEY, env: 'NUXT_PUBLIC_PORTAL_TEMPLATE' })"
    />

    <div v-else class="mt-5 flex flex-wrap items-end gap-4">
      <label class="flex flex-col gap-1.5">
        <span class="text-sm font-medium text-text-secondary">{{ t('settings.portalAppearance.template') }}</span>
        <select
          v-model="selected"
          class="min-w-[220px] rounded-xl border border-border bg-surface-elevated px-3 py-2 text-sm text-text-primary focus:border-text-secondary focus:outline-none"
        >
          <option v-for="template in TEMPLATES" :key="template.value" :value="template.value">
            {{ t(template.labelKey) }}
          </option>
        </select>
      </label>

      <button
        type="button"
        class="rounded-xl bg-status-green/15 border border-status-green/40 px-4 py-2 text-sm font-semibold text-status-green transition-colors hover:bg-status-green/25 disabled:cursor-not-allowed disabled:opacity-40"
        :disabled="!dirty || saving"
        @click="save"
      >
        {{ saving ? t('common.saving') : t('common.save') }}
      </button>

      <p class="text-sm text-text-muted">
        {{ t(TEMPLATES.find(tpl => tpl.value === selected)?.hintKey ?? '') }}
      </p>
    </div>
  </section>
</template>
