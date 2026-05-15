<script setup lang="ts">
/**
 * Domains list view — all registered domains.
 */
import { onMounted } from 'vue'
import { useDomainsStore } from '../stores/domainsStore'

const store = useDomainsStore()
onMounted(store.fetchAll)
</script>

<template>
  <div>
    <h1 class="text-2xl font-bold text-gray-800 mb-6">Domains</h1>
    <div v-if="store.loading" class="text-gray-500">Loading...</div>
    <div v-else-if="store.error" class="text-red-600">{{ store.error }}</div>
    <div v-else class="bg-white rounded-xl shadow overflow-hidden">
      <table class="w-full text-sm">
        <thead class="bg-gray-50 text-gray-600 uppercase text-xs">
          <tr>
            <th class="px-4 py-3 text-left">ID</th>
            <th class="px-4 py-3 text-left">Client ID</th>
            <th class="px-4 py-3 text-left">Domain</th>
            <th class="px-4 py-3 text-left">Status</th>
            <th class="px-4 py-3 text-left">Expires</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-100">
          <tr v-for="domain in store.domains" :key="domain.id" class="hover:bg-gray-50">
            <td class="px-4 py-3 text-gray-500">{{ domain.id }}</td>
            <td class="px-4 py-3 text-gray-500">{{ domain.clientId }}</td>
            <td class="px-4 py-3 font-medium">{{ domain.name }}</td>
            <td class="px-4 py-3">
              <span :class="domain.status === 'active' ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700'" class="px-2 py-0.5 rounded-full text-xs font-medium">{{ domain.status }}</span>
            </td>
            <td class="px-4 py-3 text-gray-500">{{ new Date(domain.expiresAt).toLocaleDateString() }}</td>
          </tr>
          <tr v-if="store.domains.length === 0">
            <td colspan="5" class="px-4 py-6 text-center text-gray-400">No domains found.</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
