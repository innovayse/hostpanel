<script setup lang="ts">
/**
 * Integration category page.
 *
 * Shows all integrations for the active category derived from the route param.
 * Falls back to 'payments' when accessed at /integrations.
 */
import { computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useIntegrationsStore } from '../stores/integrationsStore'
import IntegrationCard from '../components/IntegrationCard.vue'
import { INTEGRATION_CATEGORIES } from '../types/integration.meta'


const route = useRoute()
const store = useIntegrationsStore()

onMounted(store.fetchAll)

const ALL_CATEGORIES = [...INTEGRATION_CATEGORIES]

/** Active category key from route param, defaults to 'payments'. */
const activeCategory = computed<string>(() =>
  (route.params.category as string) || 'payments'
)

/** Label for the active category. */
const activeCategoryLabel = computed(() =>
  ALL_CATEGORIES.find(c => c.key === activeCategory.value)?.label ?? activeCategory.value
)

/** Integrations filtered to the active category. */
const filtered = computed(() =>
  store.integrations.filter(i => i.category === activeCategory.value)
)

/** Number of active integrations in this category. */
const activeCount = computed(() => filtered.value.filter(i => i.isEnabled).length)
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">
          {{ activeCategoryLabel }}
        </h1>
        <p class="text-[0.78rem] text-text-secondary mt-1">
          {{ filtered.length }} integration{{ filtered.length !== 1 ? 's' : '' }}
          <span v-if="activeCount > 0" class="ml-1 text-status-green">· {{ activeCount }} active</span>
        </p>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="store.loading && filtered.length === 0" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading…
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ store.error }}
    </div>

    <!-- Migration placeholder -->
    <div
      v-else-if="activeCategory === 'migration' && filtered.length === 0"
      class="bg-surface-card border border-border rounded-2xl flex flex-col items-center justify-center py-16 gap-3"
    >
      <div class="w-12 h-12 rounded-2xl bg-white/[0.04] border border-border flex items-center justify-center text-2xl">
        🔀
      </div>
      <p class="text-[0.875rem] font-medium text-text-secondary">Migration tools coming soon</p>
      <p class="text-[0.78rem] text-text-muted">Import data from WHMCS and other platforms</p>
    </div>

    <!-- Cards grid -->
    <div v-else-if="filtered.length > 0" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
      <IntegrationCard
        v-for="integration in filtered"
        :key="integration.slug"
        :integration="integration"
      />
    </div>

    <!-- Empty state -->
    <div
      v-else
      class="bg-surface-card border border-border rounded-2xl flex flex-col items-center justify-center py-16 gap-3"
    >
      <p class="text-[0.875rem] font-medium text-text-secondary">No integrations in this category</p>
    </div>

  </div>
</template>
