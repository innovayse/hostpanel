<script setup lang="ts">
/**
 * System settings view — key-value list of global settings, with a dedicated
 * control for the storefront template.
 */
import { onMounted, ref } from 'vue'
import { useSettingsStore } from '../stores/settingsStore'
import PortalAppearanceCard from '../components/PortalAppearanceCard.vue'

const store = useSettingsStore()
const saving = ref(false)

onMounted(store.fetchSettings)

/**
 * Persists one setting.
 *
 * @param id - Setting ID.
 * @param value - New value.
 */
async function onSave(id: number, value: string) {
  saving.value = true
  try {
    await store.updateSetting(id, value)
  } catch {
    // store.error carries the message for the banner below.
  } finally {
    saving.value = false
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
              <td class="px-4 py-3 text-text-primary">{{ setting.value }}</td>
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
