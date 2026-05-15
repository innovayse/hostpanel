<script setup lang="ts">
/**
 * Renders a category section card containing all integrations for that category.
 *
 * Header shows icon, category label, and count of active integrations.
 * Body is a responsive grid of IntegrationRow components.
 */
import { computed } from 'vue'
import IntegrationRow from './IntegrationRow.vue'
import type { IntegrationDto } from '../types/integration.types'

/** Props for IntegrationSection. */
const props = defineProps<{
  /** Emoji icon for the category. */
  icon: string
  /** Human-readable category label. */
  label: string
  /** Integrations that belong to this category. */
  integrations: IntegrationDto[]
}>()

/** Number of enabled integrations in this section. */
const activeCount = computed(() =>
  props.integrations.filter(i => i.isEnabled).length
)
</script>

<template>
  <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">

    <!-- Section header -->
    <div class="flex items-center gap-2.5 px-5 py-3.5 border-b border-border">
      <span class="text-base leading-none">{{ icon }}</span>
      <span class="font-display font-semibold text-[0.875rem] text-text-primary">{{ label }}</span>
      <span
        v-if="activeCount > 0"
        class="ml-auto text-[0.68rem] font-semibold text-status-green bg-status-green/10 border border-status-green/20 rounded-full px-2 py-0.5"
      >
        {{ activeCount }} active
      </span>
      <span
        v-else
        class="ml-auto text-[0.68rem] font-medium text-text-muted"
      >
        none active
      </span>
    </div>

    <!-- Integration rows grid -->
    <div class="grid divide-y divide-border" :class="integrations.length === 1 ? 'grid-cols-1' : integrations.length === 2 ? 'grid-cols-1 sm:grid-cols-2 sm:divide-y-0 sm:divide-x' : 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 sm:divide-y-0 sm:divide-x'">
      <IntegrationRow
        v-for="integration in integrations"
        :key="integration.slug"
        :integration="integration"
      />
    </div>

  </div>
</template>
