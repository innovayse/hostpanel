<script setup lang="ts">
/**
 * Tickets list view — all support tickets.
 */
import { onMounted } from 'vue'
import { useSupportStore } from '../stores/supportStore'

const store = useSupportStore()
onMounted(store.fetchAll)
</script>

<template>
  <div>
    <h1 class="text-2xl font-bold text-gray-800 mb-6">Support Tickets</h1>
    <div v-if="store.loading" class="text-gray-500">Loading...</div>
    <div v-else-if="store.error" class="text-red-600">{{ store.error }}</div>
    <div v-else class="bg-white rounded-xl shadow overflow-hidden">
      <table class="w-full text-sm">
        <thead class="bg-gray-50 text-gray-600 uppercase text-xs">
          <tr>
            <th class="px-4 py-3 text-left">ID</th>
            <th class="px-4 py-3 text-left">Subject</th>
            <th class="px-4 py-3 text-left">Client ID</th>
            <th class="px-4 py-3 text-left">Priority</th>
            <th class="px-4 py-3 text-left">Status</th>
            <th class="px-4 py-3 text-left">Created</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-100">
          <tr v-for="ticket in store.tickets" :key="ticket.id" class="hover:bg-gray-50">
            <td class="px-4 py-3 text-gray-500">{{ ticket.id }}</td>
            <td class="px-4 py-3 font-medium">
              <RouterLink :to="`/support/${ticket.id}`" class="text-blue-600 hover:underline">{{ ticket.subject }}</RouterLink>
            </td>
            <td class="px-4 py-3 text-gray-500">{{ ticket.clientId }}</td>
            <td class="px-4 py-3 text-gray-600">{{ ticket.priority }}</td>
            <td class="px-4 py-3">
              <span :class="ticket.status === 'open' ? 'bg-yellow-100 text-yellow-700' : ticket.status === 'closed' ? 'bg-gray-100 text-gray-500' : 'bg-blue-100 text-blue-700'" class="px-2 py-0.5 rounded-full text-xs font-medium">{{ ticket.status }}</span>
            </td>
            <td class="px-4 py-3 text-gray-500">{{ new Date(ticket.createdAt).toLocaleDateString() }}</td>
          </tr>
          <tr v-if="store.tickets.length === 0">
            <td colspan="6" class="px-4 py-6 text-center text-gray-400">No tickets found.</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
