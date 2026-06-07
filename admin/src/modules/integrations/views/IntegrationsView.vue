<script setup lang="ts">
/**
 * Main Apps & Integrations page.
 *
 * Renders all integration categories as sections with card grids.
 */
import { onMounted } from 'vue'
import { useIntegrationsStore } from '../stores/integrationsStore'
import IntegrationSection from '../components/IntegrationSection.vue'
import { INTEGRATION_CATEGORIES } from '../types/integration.meta'

const store = useIntegrationsStore()

onMounted(store.fetchAll)

/**
 * Returns integrations filtered by category key.
 *
 * @param category - Category key to filter by.
 * @returns Filtered array of IntegrationDto.
 */
function byCategory(category: string) {
  return store.integrations.filter(i => i.category === category)
}
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="mb-8">
      <h1 class="font-display text-[1.75rem] font-bold text-text-primary tracking-tight leading-none mb-1.5">
        Apps & Integrations
      </h1>
      <p class="text-sm text-text-secondary">Manage your third-party service connections and plugins</p>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading integrations...
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ store.error }}
    </div>

    <!-- Category sections -->
    <div v-else class="flex flex-col gap-8">
      <IntegrationSection
        v-for="cat in INTEGRATION_CATEGORIES"
        :key="cat.key"
        :icon="cat.icon"
        :label="cat.label"
        :integrations="byCategory(cat.key)"
      />
    </div>

  </div>
</template>
