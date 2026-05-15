<script setup lang="ts">
/**
 * Invoices list view — displays all billing invoices.
 */
import { onMounted } from 'vue'
import { useBillingStore } from '../stores/billingStore'

const store = useBillingStore()

onMounted(store.fetchAll)
</script>

<template>
  <div>
    <h1 class="text-2xl font-bold text-gray-800 mb-6">Invoices</h1>
    <div v-if="store.loading" class="text-gray-500">Loading...</div>
    <div v-else-if="store.error" class="text-red-600">{{ store.error }}</div>
    <div v-else class="bg-white rounded-xl shadow overflow-hidden">
      <table class="w-full text-sm">
        <thead class="bg-gray-50 text-gray-600 uppercase text-xs">
          <tr>
            <th class="px-4 py-3 text-left">ID</th>
            <th class="px-4 py-3 text-left">Client ID</th>
            <th class="px-4 py-3 text-left">Total</th>
            <th class="px-4 py-3 text-left">Status</th>
            <th class="px-4 py-3 text-left">Due Date</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-100">
          <tr v-for="invoice in store.invoices" :key="invoice.id" class="hover:bg-gray-50">
            <td class="px-4 py-3 text-gray-500">{{ invoice.id }}</td>
            <td class="px-4 py-3 text-gray-600">{{ invoice.clientId }}</td>
            <td class="px-4 py-3 font-medium">${{ invoice.total.toFixed(2) }}</td>
            <td class="px-4 py-3">
              <span :class="{ 'bg-green-100 text-green-700': invoice.status === 'paid', 'bg-red-100 text-red-700': invoice.status === 'overdue', 'bg-yellow-100 text-yellow-700': invoice.status === 'unpaid', 'bg-gray-100 text-gray-600': invoice.status === 'cancelled' }" class="px-2 py-0.5 rounded-full text-xs font-medium">
                {{ invoice.status }}
              </span>
            </td>
            <td class="px-4 py-3 text-gray-500">{{ new Date(invoice.dueDate).toLocaleDateString() }}</td>
          </tr>
          <tr v-if="store.invoices.length === 0">
            <td colspan="5" class="px-4 py-6 text-center text-gray-400">No invoices found.</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
