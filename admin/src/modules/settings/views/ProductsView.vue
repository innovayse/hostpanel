<script setup lang="ts">
/**
 * Products settings view — list of all products/plans.
 */
import { onMounted } from 'vue'
import { useSettingsStore } from '../stores/settingsStore'

const store = useSettingsStore()
onMounted(store.fetchProducts)
</script>

<template>
  <div>
    <h1 class="text-2xl font-bold text-gray-800 mb-6">Products</h1>
    <div v-if="store.loading" class="text-gray-500">Loading...</div>
    <div v-else-if="store.error" class="text-red-600">{{ store.error }}</div>
    <div v-else class="bg-white rounded-xl shadow overflow-hidden">
      <table class="w-full text-sm">
        <thead class="bg-gray-50 text-gray-600 uppercase text-xs">
          <tr>
            <th class="px-4 py-3 text-left">ID</th>
            <th class="px-4 py-3 text-left">Name</th>
            <th class="px-4 py-3 text-left">Type</th>
            <th class="px-4 py-3 text-left">Price</th>
            <th class="px-4 py-3 text-left">Billing Cycle</th>
            <th class="px-4 py-3 text-left">Status</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-100">
          <tr v-for="product in store.products" :key="product.id" class="hover:bg-gray-50">
            <td class="px-4 py-3 text-gray-500">{{ product.id }}</td>
            <td class="px-4 py-3 font-medium">{{ product.name }}</td>
            <td class="px-4 py-3 text-gray-600">{{ product.type }}</td>
            <td class="px-4 py-3">${{ product.price.toFixed(2) }}</td>
            <td class="px-4 py-3 text-gray-600">{{ product.billingCycle }}</td>
            <td class="px-4 py-3">
              <span :class="product.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'" class="px-2 py-0.5 rounded-full text-xs font-medium">{{ product.isActive ? 'Active' : 'Inactive' }}</span>
            </td>
          </tr>
          <tr v-if="store.products.length === 0">
            <td colspan="6" class="px-4 py-6 text-center text-gray-400">No products found.</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
