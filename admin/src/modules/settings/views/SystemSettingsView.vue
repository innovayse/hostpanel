<script setup lang="ts">
/**
 * System settings view — key-value list of global settings, with a dedicated
 * control for the storefront template.
 */
import { onMounted, ref } from 'vue'
import { useSettingsStore } from '../stores/settingsStore'
import PortalAppearanceCard from '../components/PortalAppearanceCard.vue'
import type { Setting } from '../../../types/models'

const store = useSettingsStore()
const saving = ref(false)

onMounted(store.fetchSettings)

/**
 * Persists one setting.
 *
 * @param id - Setting ID.
 * @param value - New value.
 */
async function onSave(id: number, value: string): Promise<boolean> {
  saving.value = true
  try {
    await store.updateSetting(id, value)
    return true
  } catch {
    // store.error carries the message for the banner below. The outcome is
    // returned rather than swallowed, because the caller has to know whether it
    // may drop the operator's unsaved text.
    return false
  } finally {
    saving.value = false
  }
}

/**
 * Unsaved edits, keyed by setting id.
 *
 * The table used to be read-only, which left every seeded key — the storefront's
 * contact details, footer widgets and app launcher among them — changeable only
 * through the API. Editing here is per row rather than a bulk form so a mistake
 * in one value cannot be saved along with the others.
 */
const drafts = ref<Record<number, string>>({})

/**
 * Current text for a row: the operator's unsaved edit, or the stored value.
 *
 * @param setting - The settings row.
 */
function draftOf(setting: Setting): string {
  return drafts.value[setting.id] ?? setting.value ?? ''
}

/**
 * Whether a row has an edit worth saving.
 *
 * @param setting - The settings row.
 */
function isDirty(setting: Setting): boolean {
  return setting.id in drafts.value && drafts.value[setting.id] !== (setting.value ?? '')
}

/**
 * Saves one row and drops its draft, so the input falls back to the stored value.
 *
 * @param setting - The settings row.
 */
async function saveRow(setting: Setting) {
  if (!isDirty(setting)) return

  // Only on success. This used to drop the draft either way, so a failed save
  // replaced whatever the operator had typed with the value already stored —
  // leaving an error banner and no way back to the text that caused it.
  const draft = drafts.value[setting.id]
  // `isDirty` above already established the key is present; this only narrows it.
  if (draft === undefined) return

  if (await onSave(setting.id, draft)) {
    delete drafts.value[setting.id]
  }
}
</script>

<template>
  <div>
    <!--
      This screen predates the admin design tokens and was still on bg-white /
      text-gray-800, which put a near-black heading on the dark shell. Moved onto
      the same tokens the rest of the panel uses.
    -->
    <h1 class="font-display text-[1.75rem] font-bold text-text-primary tracking-tight leading-none mb-6">
      System Settings
    </h1>

    <div v-if="store.loading" class="text-text-secondary">Loading...</div>
    <template v-else>
      <div
        v-if="store.error"
        class="mb-6 rounded-xl border border-status-red/30 bg-status-red/10 px-4 py-3 text-status-red"
      >
        {{ store.error }}
      </div>

      <PortalAppearanceCard :settings="store.settings" :saving="saving" @save="onSave" />

      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden overflow-x-auto">
        <table class="w-full text-sm">
          <thead class="bg-surface-elevated text-text-secondary uppercase text-xs">
            <tr>
              <th class="px-4 py-3 text-left font-semibold">Key</th>
              <th class="px-4 py-3 text-left font-semibold">Value</th>
              <th class="px-4 py-3 text-left font-semibold">Description</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-border">
            <tr v-for="setting in store.settings" :key="setting.id" class="hover:bg-surface-elevated">
              <td class="px-4 py-3 font-mono text-text-primary">{{ setting.key }}</td>
              <td class="px-4 py-3">
                <div class="flex items-center gap-2">
                  <input
                    :value="draftOf(setting)"
                    type="text"
                    class="min-w-[200px] flex-1 rounded-lg border border-border bg-surface-elevated px-3 py-1.5 text-sm text-text-primary focus:border-text-secondary focus:outline-none"
                    @input="drafts[setting.id] = ($event.target as HTMLInputElement).value"
                    @keyup.enter="saveRow(setting)"
                  >
                  <button
                    v-if="isDirty(setting)"
                    type="button"
                    :disabled="saving"
                    class="shrink-0 rounded-lg bg-text-primary px-3 py-1.5 text-sm font-medium text-surface-card disabled:opacity-50"
                    @click="saveRow(setting)"
                  >
                    {{ saving ? 'Saving…' : 'Save' }}
                  </button>
                </div>
              </td>
              <td class="px-4 py-3 text-text-secondary">{{ setting.description ?? '—' }}</td>
            </tr>
            <tr v-if="store.settings.length === 0">
              <td colspan="3" class="px-4 py-6 text-center text-text-muted">No settings found.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>
  </div>
</template>
