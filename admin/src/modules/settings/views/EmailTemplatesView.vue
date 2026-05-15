<script setup lang="ts">
/**
 * Email templates view — list of all notification templates.
 */
import { onMounted } from 'vue'
import { useSettingsStore } from '../stores/settingsStore'

const store = useSettingsStore()
onMounted(store.fetchEmailTemplates)
</script>

<template>
  <div>
    <h1 class="text-2xl font-bold text-gray-800 mb-6">Email Templates</h1>
    <div v-if="store.loading" class="text-gray-500">Loading...</div>
    <div v-else-if="store.error" class="text-red-600">{{ store.error }}</div>
    <div v-else class="bg-white rounded-xl shadow overflow-hidden">
      <table class="w-full text-sm">
        <thead class="bg-gray-50 text-gray-600 uppercase text-xs">
          <tr>
            <th class="px-4 py-3 text-left">ID</th>
            <th class="px-4 py-3 text-left">Name</th>
            <th class="px-4 py-3 text-left">Subject</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-100">
          <tr v-for="tmpl in store.emailTemplates" :key="tmpl.id" class="hover:bg-gray-50">
            <td class="px-4 py-3 text-gray-500">{{ tmpl.id }}</td>
            <td class="px-4 py-3 font-medium">{{ tmpl.name }}</td>
            <td class="px-4 py-3 text-gray-600">{{ tmpl.subject }}</td>
          </tr>
          <tr v-if="store.emailTemplates.length === 0">
            <td colspan="3" class="px-4 py-6 text-center text-gray-400">No templates found.</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
