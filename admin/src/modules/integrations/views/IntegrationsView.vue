<script setup lang="ts">
/**
 * Main Apps & Integrations page.
 *
 * Renders all integration categories as section cards.
 * Email and Fraud Protection sections are shown side-by-side.
 */
import { computed, onMounted } from 'vue'
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

/** Categories rendered as full-width sections (1 per row). */
const fullWidthCategories = computed(() =>
  INTEGRATION_CATEGORIES.filter(c => !['email', 'fraud'].includes(c.key))
)

/** Categories rendered side-by-side in a 2-column grid. */
const halfWidthCategories = computed(() =>
  INTEGRATION_CATEGORIES.filter(c => ['email', 'fraud'].includes(c.key))
)
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 max-w-6xl w-full">

    <!-- Header -->
    <div class="mb-7">
      <h1 class="font-display text-[1.75rem] font-bold text-text-primary tracking-tight leading-none mb-1.5">
        Apps & Integrations
      </h1>
      <p class="text-sm text-text-secondary">Manage your third-party service connections</p>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading integrations…
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ store.error }}
    </div>

    <template v-else>
      <!-- Full-width sections -->
      <div class="flex flex-col gap-4 mb-4">
        <IntegrationSection
          v-for="cat in fullWidthCategories"
          :key="cat.key"
          :icon="cat.icon"
          :label="cat.label"
          :integrations="byCategory(cat.key)"
        />
      </div>

      <!-- Side-by-side: email + fraud -->
      <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
        <IntegrationSection
          v-for="cat in halfWidthCategories"
          :key="cat.key"
          :icon="cat.icon"
          :label="cat.label"
          :integrations="byCategory(cat.key)"
        />
      </div>
    </template>

  </div>
</template>
