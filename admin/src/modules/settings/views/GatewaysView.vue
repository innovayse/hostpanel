<script setup lang="ts">
/**
 * Payment gateways settings view.
 */
import { onMounted } from 'vue'
import { useSettingsStore } from '../stores/settingsStore'

const store = useSettingsStore()
onMounted(store.fetchGateways)
</script>

<template>
  <div>
    <h1 class="text-2xl font-bold text-gray-800 mb-6">Payment Gateways</h1>
    <div v-if="store.loading" class="text-gray-500">Loading...</div>
    <div v-else-if="store.error" class="text-red-600">{{ store.error }}</div>
    <div v-else class="bg-white rounded-xl shadow overflow-hidden">
      <table class="w-full text-sm">
        <thead class="bg-gray-50 text-gray-600 uppercase text-xs">
          <tr>
            <th class="px-4 py-3 text-left">ID</th>
            <th class="px-4 py-3 text-left">Name</th>
            <th class="px-4 py-3 text-left">Status</th>
            <th class="px-4 py-3 text-left">Display Order</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-100">
          <tr v-for="gw in store.gateways" :key="gw.id" class="hover:bg-gray-50">
            <td class="px-4 py-3 text-gray-500">{{ gw.id }}</td>
            <td class="px-4 py-3 font-medium">{{ gw.name }}</td>
            <td class="px-4 py-3">
              <span :class="gw.isEnabled ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'" class="px-2 py-0.5 rounded-full text-xs font-medium">{{ gw.isEnabled ? 'Enabled' : 'Disabled' }}</span>
            </td>
            <td class="px-4 py-3 text-gray-500">{{ gw.displayOrder }}</td>
          </tr>
          <tr v-if="store.gateways.length === 0">
            <td colspan="4" class="px-4 py-6 text-center text-gray-400">No gateways configured.</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
