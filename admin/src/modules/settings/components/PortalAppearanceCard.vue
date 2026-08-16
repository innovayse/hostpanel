<script setup lang="ts">
/**
 * Portal appearance card — picks the storefront template.
 *
 * A dropdown rather than the free-text field the settings table offers. The
 * portal falls back to its default on an unrecognised value, so a typo would
 * not break the site, but it would silently revert an operator's choice with no
 * indication why. Constraining the input removes the failure entirely.
 */
import { computed, ref } from 'vue'
import type { Setting } from '../../../types/models'

const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
  /** True while a save is in flight. */
  saving?: boolean
}>()

const emit = defineEmits<{ save: [id: number, value: string] }>()

/** Templates the portal ships. Mirrors client/templates/types.ts. */
const TEMPLATES = [
  { value: 'aurora', label: 'Aurora', hint: 'Current default — dark/light, Armenian typography' },
  { value: 'classic', label: 'Classic', hint: 'The original storefront design' },
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
  draft.value = null
}
</script>

<template>
  <section class="bg-surface-card border border-border rounded-2xl p-6 mb-6">
    <h2 class="font-display text-lg font-bold text-text-primary">Portal appearance</h2>
    <p class="text-sm text-text-secondary mt-1">
      Which template the public storefront renders. Changes take effect on the next page load.
    </p>

    <!--
      The backend has no create endpoint for settings, so an unseeded key cannot
      be added from the admin panel. Say so rather than showing a control that
      silently does nothing.
    -->
    <div
      v-if="!setting"
      class="mt-4 rounded-xl border border-status-yellow/30 bg-status-yellow/10 p-4 text-sm text-status-yellow"
    >
      The <code class="font-mono">{{ KEY }}</code> setting has not been seeded yet, so it cannot be
      changed here. Until it exists the portal uses its
      <code class="font-mono">NUXT_PUBLIC_PORTAL_TEMPLATE</code> environment variable.
    </div>

    <div v-else class="mt-5 flex flex-wrap items-end gap-4">
      <label class="flex flex-col gap-1.5">
        <span class="text-sm font-medium text-text-secondary">Template</span>
        <select
          v-model="selected"
          class="min-w-[220px] rounded-xl border border-border bg-surface-elevated px-3 py-2 text-sm text-text-primary focus:border-text-secondary focus:outline-none"
        >
          <option v-for="template in TEMPLATES" :key="template.value" :value="template.value">
            {{ template.label }}
          </option>
        </select>
      </label>

      <button
        type="button"
        class="rounded-xl bg-status-green/15 border border-status-green/40 px-4 py-2 text-sm font-semibold text-status-green transition-colors hover:bg-status-green/25 disabled:cursor-not-allowed disabled:opacity-40"
        :disabled="!dirty || saving"
        @click="save"
      >
        {{ saving ? 'Saving…' : 'Save' }}
      </button>

      <p class="text-sm text-text-muted">
        {{ TEMPLATES.find(t => t.value === selected)?.hint }}
      </p>
    </div>
  </section>
</template>
