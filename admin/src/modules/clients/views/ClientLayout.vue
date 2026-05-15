<script setup lang="ts">
/**
 * Layout wrapper for client detail pages.
 * Shows a compact header with client info and the inner sidebar alongside the active sub-page.
 */
import { onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useClientsStore } from '../stores/clientsStore'
import { CLIENT_STATUS_STYLES } from '../../../utils/constants'
import ClientInnerSidebar from '../components/ClientInnerSidebar.vue'

const route = useRoute()
const router = useRouter()
const store = useClientsStore()

/** Status badge style map. */
const statusStyles = CLIENT_STATUS_STYLES

/**
 * Loads the client data and currencies for the current route param.
 *
 * @param id - The client identifier from route params.
 * @returns Promise that resolves when both fetches complete.
 */
async function loadClient(id: string): Promise<void> {
  await store.fetchById(id)
  store.fetchCurrencies()
}

// Re-fetch when client ID changes in the route
watch(() => route.params.id, (newId) => {
  if (newId) {
    loadClient(String(newId))
  }
})

onMounted(() => {
  loadClient(String(route.params.id))
})
</script>

<template>
  <div class="flex flex-col h-full w-full">

    <!-- Loading -->
    <div v-if="store.loading && !store.current" class="flex items-center gap-3 text-text-secondary text-sm p-6">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading client…
    </div>

    <!-- Error -->
    <div v-else-if="store.error && !store.current" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 m-6">
      {{ store.error }}
    </div>

    <template v-else-if="store.current">
      <!-- Compact header -->
      <div class="flex items-center gap-3 px-4 sm:px-6 py-3 border-b border-border shrink-0">
        <!-- Back button -->
        <button
          class="w-8 h-8 flex items-center justify-center rounded-lg text-text-muted hover:text-text-primary hover:bg-white/[0.06] transition-colors"
          @click="router.push('/clients')"
        >
          <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <polyline points="15 18 9 12 15 6" />
          </svg>
        </button>

        <!-- Avatar -->
        <div class="flex items-center justify-center w-10 h-10 rounded-full bg-primary-500/10 text-primary-400 text-[0.85rem] font-bold shrink-0">
          {{ store.current.firstName.charAt(0) }}{{ store.current.lastName.charAt(0) }}
        </div>

        <!-- Name + status + company -->
        <div>
          <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">
            {{ store.current.firstName }} {{ store.current.lastName }}
          </h1>
          <div class="flex items-center gap-2 mt-1">
            <span
              class="inline-flex px-2 py-0.5 rounded-full text-[0.65rem] font-semibold border"
              :class="statusStyles[store.current.status] ?? statusStyles.Inactive"
            >
              {{ store.current.status }}
            </span>
            <span v-if="store.current.companyName" class="text-[0.75rem] text-text-secondary">
              {{ store.current.companyName }}
            </span>
          </div>
        </div>
      </div>

      <!-- Body: sidebar + content -->
      <div class="flex flex-1 min-h-0">
        <ClientInnerSidebar :client-id="String(route.params.id)" />
        <div class="flex-1 min-w-0 overflow-y-auto">
          <RouterView />
        </div>
      </div>
    </template>
  </div>
</template>
