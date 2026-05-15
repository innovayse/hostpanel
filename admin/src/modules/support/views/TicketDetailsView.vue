<script setup lang="ts">
/**
 * Ticket details view — single ticket info.
 */
import { onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useSupportStore } from '../stores/supportStore'

const route = useRoute()
const store = useSupportStore()
onMounted(() => store.fetchById(route.params.id as string))
</script>

<template>
  <div>
    <RouterLink to="/support" class="text-blue-600 hover:underline text-sm mb-4 inline-block">&larr; Back to tickets</RouterLink>
    <div v-if="store.loading" class="text-gray-500 mt-4">Loading...</div>
    <div v-else-if="store.error" class="text-red-600 mt-4">{{ store.error }}</div>
    <div v-else-if="store.current" class="bg-white rounded-xl shadow p-6 mt-4 max-w-lg">
      <h1 class="text-2xl font-bold text-gray-800 mb-4">{{ store.current.subject }}</h1>
      <dl class="space-y-3 text-sm">
        <div class="flex gap-4"><dt class="w-32 text-gray-500 font-medium">ID</dt><dd>{{ store.current.id }}</dd></div>
        <div class="flex gap-4"><dt class="w-32 text-gray-500 font-medium">Client ID</dt><dd>{{ store.current.clientId }}</dd></div>
        <div class="flex gap-4"><dt class="w-32 text-gray-500 font-medium">Priority</dt><dd>{{ store.current.priority }}</dd></div>
        <div class="flex gap-4"><dt class="w-32 text-gray-500 font-medium">Status</dt><dd>{{ store.current.status }}</dd></div>
        <div class="flex gap-4"><dt class="w-32 text-gray-500 font-medium">Created</dt><dd>{{ new Date(store.current.createdAt).toLocaleString() }}</dd></div>
      </dl>
    </div>
  </div>
</template>
