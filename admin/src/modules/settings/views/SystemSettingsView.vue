<script setup lang="ts">
/**
 * System settings view — key-value list of global settings.
 */
import { onMounted } from 'vue'
import { useSettingsStore } from '../stores/settingsStore'

const store = useSettingsStore()
onMounted(store.fetchSettings)
</script>

<template>
  <div>
    <h1 class="text-2xl font-bold text-gray-800 mb-6">System Settings</h1>
    <div v-if="store.loading" class="text-gray-500">Loading...</div>
    <div v-else-if="store.error" class="text-red-600">{{ store.error }}</div>
    <div v-else class="bg-white rounded-xl shadow overflow-hidden">
      <table class="w-full text-sm">
        <thead class="bg-gray-50 text-gray-600 uppercase text-xs">
          <tr>
            <th class="px-4 py-3 text-left">Key</th>
            <th class="px-4 py-3 text-left">Value</th>
            <th class="px-4 py-3 text-left">Description</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-100">
          <tr v-for="setting in store.settings" :key="setting.id" class="hover:bg-gray-50">
            <td class="px-4 py-3 font-mono text-gray-700">{{ setting.key }}</td>
            <td class="px-4 py-3 text-gray-800">{{ setting.value }}</td>
            <td class="px-4 py-3 text-gray-500">{{ setting.description ?? '—' }}</td>
          </tr>
          <tr v-if="store.settings.length === 0">
            <td colspan="3" class="px-4 py-6 text-center text-gray-400">No settings found.</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
