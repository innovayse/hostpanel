<script setup lang="ts">
/**
 * Renders a category section with a header and a responsive grid of integration cards.
 *
 * Header shows icon, category label, and count of active integrations.
 */
import { computed } from 'vue'
import IntegrationCard from './IntegrationCard.vue'
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
  <div>
    <!-- Section header -->
    <div class="flex items-center gap-2.5 mb-3">
      <span class="text-base leading-none">{{ icon }}</span>
      <h2 class="font-display font-semibold text-[0.9rem] text-text-primary">{{ label }}</h2>
      <span
        v-if="activeCount > 0"
        class="text-[0.65rem] font-semibold text-status-green bg-status-green/10 border border-status-green/20 rounded-full px-2 py-0.5"
      >
        {{ activeCount }} active
      </span>
      <span
        v-else
        class="text-[0.65rem] font-medium text-text-muted"
      >
        none active
      </span>
    </div>

    <!-- Cards grid -->
    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
      <IntegrationCard
        v-for="integration in integrations"
        :key="integration.slug"
        :integration="integration"
      />
    </div>
  </div>
</template>
